using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using FeedParser.Models;
using FeedParser.Shared.Models;
using FeedParser.Utilities;
using SharedBase.Utilities;
using SmartFormat;

namespace FeedParser.Services;

public static class FeedParser
{
    /// <summary>
    ///   Just parses content for a feed and does nothing else
    /// </summary>
    /// <param name="feed">The feed to use to process the data</param>
    /// <param name="rawContent">The feed content</param>
    /// <param name="modifiedDocument">Way to get out the processed feed data</param>
    /// <returns>Parsed items</returns>
    public static List<ParsedFeedItem> ParseContent(this IFeed feed, string rawContent, out XDocument modifiedDocument)
    {
        var feedItems = new List<ParsedFeedItem>();

        modifiedDocument = XDocument.Parse(rawContent);

        var preprocessingActions = feed.PreprocessingActions;

        // Github style feed
        LookForFeedItemsInXMLTree(feed, modifiedDocument.Descendants().Where(e => e.Name.LocalName == "entry"),
            feedItems, preprocessingActions);

        // Discourse style feed
        if (feedItems.Count < 1)
        {
            LookForFeedItemsInXMLTree(feed, modifiedDocument.Descendants().Where(e => e.Name.LocalName == "item"),
                feedItems, preprocessingActions);
        }

        return feedItems;
    }

    /// <summary>
    ///   Processes raw feed content into the feed and stores it in <see cref="IFeed.LatestContent"/> if the final
    ///   content (hash) changed
    /// </summary>
    /// <param name="feed">The feed to use to process the data and store the result in</param>
    /// <param name="rawContent">
    ///   The raw retrieved content. Should not be the error content if a feed returned non success status code.
    /// </param>
    /// <returns>List of feed items</returns>
    public static IEnumerable<ParsedFeedItem> ProcessContent(this IFeed feed, string rawContent)
    {
        var feedItems = feed.ParseContent(rawContent, out var document);

        int newContentHash = feedItems.Count * 4483;

        foreach (var item in feedItems)
        {
            newContentHash ^= item.GetHashCode();
        }

        // Detect if the document changed and update our data only in that case (or if we have no data)
        if (newContentHash == feed.LatestContentHash && feed.LatestContent != null)
        {
            // If we are a html feed with no html content, we should process that
            if (string.IsNullOrEmpty(feed.HtmlFeedVersionSuffix) || feed.HtmlLatestContent != null)
                return feedItems;
        }

        // Write the clean document back out
        using var stream = new MemoryStream();

        // For some reason XMLWriter fails in writing out the Discourse feed data...
        // Might now work with the flush here
        using var writer = new StreamWriter(stream, Encoding.UTF8);
        document.Save(writer, SaveOptions.DisableFormatting);
        writer.Flush();

        stream.Position = 0;
        var reader = new StreamReader(stream, Encoding.UTF8);
        var finalContent = reader.ReadToEnd();

        if (string.IsNullOrWhiteSpace(finalContent))
            throw new Exception("XML writer failed to write anything");

        feed.LatestContent = finalContent;
        feed.ContentUpdatedAt = DateTime.UtcNow;
        feed.LatestContentHash = newContentHash;

        UpdateHtmlFeedContent(feed, feedItems);

        return feedItems;
    }

    private static void LookForFeedItemsInXMLTree(IFeed feed, IEnumerable<XElement> elements,
        List<ParsedFeedItem> parsedFeedItems, List<FeedPreprocessingAction>? preprocessingActions)
    {
        if (parsedFeedItems.Count >= feed.MaxItems)
            return;

        foreach (var element in elements)
        {
            if (preprocessingActions is { Count: > 0 })
                RunPreprocessingActions(element, preprocessingActions);

            var id = element.Descendants().FirstOrDefault(p => p.Name.LocalName is "id" or "guid")
                ?.Value;

            // Can't handle entries with no id
            if (id == null)
                continue;

            var link = "Link is missing";

            var linkElement = element.Descendants().FirstOrDefault(p => p.Name.LocalName == "link");

            if (linkElement != null)
            {
                link = linkElement.Attribute("href")?.Value ?? linkElement.Value;
            }

            var title = element.Descendants().FirstOrDefault(p => p.Name.LocalName == "title")?.Value ??
                "Unknown title";

            var authorNode = element.Descendants()
                .FirstOrDefault(p => p.Name.LocalName == "author" || p.Name.LocalName.Contains("creator"));

            if (authorNode is { HasElements: true })
            {
                authorNode = authorNode.Descendants().FirstOrDefault(e => e.Name.LocalName == "name");
            }

            var author = authorNode?.Value ?? "Unknown author";

            var parsed = new ParsedFeedItem(id, EnsureNoDangerousContent(link), EnsureNoDangerousContent(title),
                EnsureNoDangerousContent(author))
            {
                Summary = EnsureNoDangerousContentMaybeNull(
                    element.Descendants().FirstOrDefault(p => p.Name.LocalName == "summary")?.Value ??
                    element.Descendants().FirstOrDefault(p => p.Name.LocalName == "description")?.Value ??
                    element.Descendants().FirstOrDefault(p => p.Name.LocalName == "content")?.Value),
                OriginalFeed = feed.Name,
            };

            var published = element.Descendants().FirstOrDefault(p => p.Name.LocalName is "published" or "pubDate")
                ?.Value;

            if (published != null && DateTime.TryParse(published, out var parsedTime))
            {
                parsed.PublishedAt = parsedTime.ToUniversalTime();
            }

            if (parsed.Summary != null && parsed.Summary.Length > feed.MaxItemLength)
            {
                // Maybe this kind of heuristic will be good enough here...
                if (parsed.Summary.Contains("</"))
                {
                    parsed.Summary = parsed.Summary.HtmlTruncate(feed.MaxItemLength);
                }
                else
                {
                    parsed.Summary = parsed.Summary.Truncate(feed.MaxItemLength);
                }
            }

            parsedFeedItems.Add(parsed);

            if (parsedFeedItems.Count >= feed.MaxItems)
                break;
        }
    }

    private static string CreateHtmlFeedContent(IFeed feed, IEnumerable<ParsedFeedItem> items, string template)
    {
        var builder = new StringBuilder();

        foreach (var item in items)
        {
            builder.Append(Smart.Format(template, item.GetFormatterData(feed.Name)));
        }

        return builder.ToString();
    }

    private static void UpdateHtmlFeedContent(IFeed feed, IEnumerable<ParsedFeedItem> items)
    {
        if (!string.IsNullOrEmpty(feed.HtmlFeedItemEntryTemplate))
        {
            feed.HtmlLatestContent = CreateHtmlFeedContent(feed, items, feed.HtmlFeedItemEntryTemplate);
        }
        else
        {
            feed.HtmlLatestContent = null;
        }
    }

    private static void RunPreprocessingActions(XContainer feedEntry, IEnumerable<FeedPreprocessingAction> actions)
    {
        var regexTimeout = TimeSpan.FromSeconds(5);

        foreach (var action in actions)
        {
            if (action.Target == PreprocessingActionTarget.Title)
            {
                var title = feedEntry.Descendants().FirstOrDefault(e => e.Name.LocalName == "title");

                if (title != null)
                {
                    title.Value = Regex.Replace(title.Value, action.ToFind, action.Replacer, RegexOptions.IgnoreCase,
                        regexTimeout);
                }
            }
            else if (action.Target == PreprocessingActionTarget.Summary)
            {
                var content = feedEntry.Descendants().FirstOrDefault(e => e.Name.LocalName == "content");

                if (content != null)
                {
                    content.Value = Regex.Replace(content.Value, action.ToFind, action.Replacer,
                        RegexOptions.IgnoreCase, regexTimeout);
                }

                content = feedEntry.Descendants().FirstOrDefault(e => e.Name.LocalName == "description");

                if (content != null)
                {
                    content.Value = Regex.Replace(content.Value, action.ToFind, action.Replacer,
                        RegexOptions.IgnoreCase, regexTimeout);
                }

                content = feedEntry.Descendants().FirstOrDefault(e => e.Name.LocalName == "summary");

                if (content != null)
                {
                    content.Value = Regex.Replace(content.Value, action.ToFind, action.Replacer,
                        RegexOptions.IgnoreCase, regexTimeout);
                }
            }
            else
            {
                throw new ArgumentException("Unknown feed preprocessing action");
            }
        }
    }

    private static string EnsureNoDangerousContent(string content)
    {
        if (string.IsNullOrEmpty(content))
            return string.Empty;

        return EnsureNoDangerousContentMaybeNull(content)!;
    }

    private static string? EnsureNoDangerousContentMaybeNull(string? content)
    {
        if (string.IsNullOrEmpty(content))
            return null;

        return content.Replace("<script>", "&lt;script&gt;");
    }
}
