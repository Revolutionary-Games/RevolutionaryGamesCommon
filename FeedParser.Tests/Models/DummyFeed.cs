namespace FeedParser.Tests.Models;

using System;
using System.Collections.Generic;
using FeedParser.Models;
using Shared.Models;

public class DummyFeed : IFeed
{
    public DummyFeed(string name)
    {
        Name = name;
    }

    public string Name { get; }
    public int MaxItems { get; set; } = int.MaxValue;
    public string? LatestContent { get; set; }
    public DateTime? ContentUpdatedAt { get; set; }
    public List<FeedPreprocessingAction>? PreprocessingActions { get; set; }
    public string? HtmlFeedItemEntryTemplate { get; set; }
    public string? HtmlFeedVersionSuffix { get; set; }
    public string? HtmlLatestContent { get; set; }
    public int MaxItemLength { get; set; } = int.MaxValue;
    public int LatestContentHash { get; set; }
}
