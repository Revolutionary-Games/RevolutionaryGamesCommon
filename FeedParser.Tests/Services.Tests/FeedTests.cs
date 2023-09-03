namespace FeedParser.Tests.Services.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using AngleSharp.Html.Parser;
using FeedParser.Services;
using Models;
using Shared.Models;
using Xunit;

public class FeedTests
{
    [Fact]
    public static void Feed_TitleReplaceTextParts()
    {
        var feed = new DummyFeed("test")
        {
            PreprocessingActions = new List<FeedPreprocessingAction>
            {
                // Regexes as strings
                // ReSharper disable StringLiteralTypo
                new(PreprocessingActionTarget.Title, @"[\w-_]+\scommented", "New comment"),
                new(PreprocessingActionTarget.Title, @"[\w-_]+\sclosed an issue", "Issue closed"),
                new(PreprocessingActionTarget.Title, @"[\w-_]+\sopened a pull request", "New pull request"),
                new(PreprocessingActionTarget.Title, @"[\w-_]+\sforked .+ from", "New fork of"),
                new(PreprocessingActionTarget.Title, @"[\w-_]+\spushed", "New commits"),
                new(PreprocessingActionTarget.Title, @"[\w-_]+\sopened an issue",
                    "New issue"),
                new(PreprocessingActionTarget.Summary,
                    @"data-(hydro|ga|)-click[\w\-]*=""[^""]*", string.Empty),
                new(PreprocessingActionTarget.Summary, "<svg .*>.*</svg>", string.Empty),

                // ReSharper restore StringLiteralTypo
            },
        };

        var items = feed.ProcessContent(ExampleFeedData.TestGithubFeedContent).ToList();

        Assert.NotEqual(0, feed.LatestContentHash);

        Assert.NotEmpty(items);
        Assert.NotNull(feed.ContentUpdatedAt);
        Assert.NotNull(feed.LatestContent);
        Assert.NotEmpty(feed.LatestContent!);
        Assert.Equal(7, items.Count);

        Assert.Equal("User2", items[0].Author);
        Assert.Equal("tag:github.com,2008:CreateEvent/22041400336", items[0].Id);
        Assert.Equal("User2 created a branch improve_chemoreceptor_visuals in Revolutionary-Games/Thrive",
            items[0].Title);
        Assert.Equal("https://github.com/Revolutionary-Games/Thrive/compare/improve_chemoreceptor_visuals",
            items[0].Link);
        Assert.NotNull(items[0].Summary);
        Assert.NotEmpty(items[0].Summary!);
        Assert.Equal(DateTime.Parse("2022-05-29T12:12:03Z").ToUniversalTime(), items[0].PublishedAt);

        Assert.Equal("User3", items[1].Author);
        Assert.Equal("tag:github.com,2008:PushEvent/22041370895", items[1].Id);
        Assert.Equal("New commits to art_gallery in Revolutionary-Games/Thrive", items[1].Title);
        Assert.Equal("https://github.com/Revolutionary-Games/Thrive/compare/2fc3f4d82c...dd5375c068", items[1].Link);
        Assert.NotNull(items[1].Summary);
        Assert.NotEmpty(items[1].Summary!);

        Assert.Equal("revolutionary-translation-bot", items[2].Author);
        Assert.Equal("tag:github.com,2008:PullRequestEvent/22037544822", items[2].Id);
        Assert.Equal("New pull request in Revolutionary-Games/Thrive", items[2].Title);
        Assert.Equal("https://github.com/Revolutionary-Games/Thrive/pull/3375", items[2].Link);
        Assert.NotNull(items[2].Summary);
        Assert.NotEmpty(items[2].Summary!);

        Assert.Equal("User4", items[3].Author);
        Assert.Equal("tag:github.com,2008:PullRequestReviewCommentEvent/22036523558", items[3].Id);
        Assert.Equal("New comment on pull request Revolutionary-Games/Thrive#3374", items[3].Title);
        Assert.Equal("https://github.com/Revolutionary-Games/Thrive/pull/3374#discussion_r884158410", items[3].Link);
        Assert.NotNull(items[3].Summary);
        Assert.NotEmpty(items[3].Summary!);

        Assert.Equal("User4", items[4].Author);
        Assert.Equal("tag:github.com,2008:CommitCommentEvent/22034699503", items[4].Id);
        Assert.Equal("New comment on commit Revolutionary-Games/Thrive@2fc3f4d82c", items[4].Title);
        Assert.Equal(
            "https://github.com/Revolutionary-Games/Thrive/commit/2fc3f4d82c4de0e6d906a91f62f8d9f2b832c62c#r74792494",
            items[4].Link);
        Assert.NotNull(items[4].Summary);
        Assert.NotEmpty(items[4].Summary!);

        Assert.Equal("User4", items[5].Author);
        Assert.Equal("tag:github.com,2008:IssuesEvent/22033408761", items[5].Id);
        Assert.Equal("New issue in Revolutionary-Games/Thrive", items[5].Title);
        Assert.Equal("https://github.com/Revolutionary-Games/Thrive/issues/3373", items[5].Link);
        Assert.NotNull(items[5].Summary);
        Assert.NotEmpty(items[5].Summary!);

        Assert.Equal("User4", items[6].Author);
        Assert.Equal("tag:github.com,2008:PullRequestEvent/22023431903", items[6].Id);
        Assert.Equal("User4 merged a pull request in Revolutionary-Games/Thrive", items[6].Title);
        Assert.Equal("https://github.com/Revolutionary-Games/Thrive/pull/3339", items[6].Link);
        Assert.NotNull(items[6].Summary);
        Assert.NotEmpty(items[6].Summary!);
    }

    [Fact]
    public static void Feed_ParseDiscourseContent()
    {
        var feed = new DummyFeed("test");

        var items = feed.ProcessContent(ExampleFeedData.TestDiscourseFeedContent).ToList();

        Assert.NotEqual(0, feed.LatestContentHash);

        Assert.NotEmpty(items);
        Assert.NotNull(feed.ContentUpdatedAt);
        Assert.NotNull(feed.LatestContent);
        Assert.NotEmpty(feed.LatestContent!);
        Assert.Single(items);

        Assert.Equal("system", items[0].Author);
        Assert.Equal("community.revolutionarygamesstudio.com-topic-4722", items[0].Id);
        Assert.Equal("Progress Update 04/30/2022 | Patch 0.5.8.1", items[0].Title);
        Assert.Equal("https://community.revolutionarygamesstudio.com/t/progress-update-04-30-2022-patch-0-5-8-1/4722",
            items[0].Link);
        Assert.NotNull(items[0].Summary);
        Assert.NotEmpty(items[0].Summary!);
        Assert.Equal(DateTime.Parse("2022-04-30T17:10:02Z").ToUniversalTime(), items[0].PublishedAt);
    }

    [Fact]
    public static void Feed_RemapToHtmlCorrectResult()
    {
        var feed = new DummyFeed("test")
        {
            // LineLengthCheckDisable
            HtmlFeedItemEntryTemplate = @"<div class=""custom-feed-item-class feed-{FeedName}"">
<span class=""custom-feed-icon-{OriginalFeedName}""></span>
<span class=""custom-feed-title""><span class=""custom-feed-title-main"">
<a class=""custom-feed-title-link"" href=""{Link}"">{Title}</a>
</span><span class=""custom-feed-by""> by
<span class=""custom-feed-author"">{AuthorFirstWord}</span></span><span class=""custom-feed-at""> at <span class=""custom-feed-time"">{PublishedAt:yyyy-dd-MM HH.mm}</span></span>
</span><br><span class=""custom-feed-content"">{Summary}<br><a class=""custom-feed-item-url"" href=""{Link}"">Read it here</a></span></div>
</div>",

            // LineLengthCheckEnable
        };

        feed.ProcessContent(ExampleFeedData.RemapToHtmlInput);

        Assert.Equal(ExampleFeedData.RemapToHtmlOutput, feed.HtmlLatestContent);

        // Check that it is valid HTML
        var parser = new HtmlParser();
        parser.ParseDocument(feed.HtmlLatestContent!);
    }
}
