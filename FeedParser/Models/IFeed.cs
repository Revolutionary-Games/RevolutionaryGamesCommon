namespace FeedParser.Models;

using System;
using System.Collections.Generic;
using Shared.Models;

public interface IFeed
{
    public string Name { get; }

    /// <summary>
    ///   Max items to show in the feed results
    /// </summary>
    public int MaxItems { get; }

    public string? LatestContent { get; set; }

    public DateTime? ContentUpdatedAt { set; }

    public List<FeedPreprocessingAction>? PreprocessingActions { get; }

    /// <summary>
    ///   If set to non-empty value a HTML mapped version of the feed data is available when queried with
    ///   <see cref="HtmlFeedVersionSuffix"/>
    /// </summary>
    public string? HtmlFeedItemEntryTemplate { get; }

    /// <summary>
    ///   The Html version suffix, if empty then the default is html and to get the raw version another .suffix
    ///   needs to be used.
    /// </summary>
    public string? HtmlFeedVersionSuffix { get; }

    /// <summary>
    ///   HTML content created based on the feed items
    /// </summary>
    public string? HtmlLatestContent { get; set; }

    /// <summary>
    ///   Max length of an item in the feed, too long items will be truncated
    /// </summary>
    public int MaxItemLength { get; }

    /// <summary>
    ///   Hash of the latest feed items to detect when the actual feed content has changed
    /// </summary>
    public int LatestContentHash { get; set; }
}
