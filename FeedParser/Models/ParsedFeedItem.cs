namespace FeedParser.Models;

using System;
using System.Linq;
using System.Web;

public class ParsedFeedItem
{
    public ParsedFeedItem(string id, string link, string title, string author)
    {
        Title = title;
        Author = author;
        Id = id;
        Link = link;
    }

    public string Id { get; set; }

    public string Link { get; set; }
    public string Title { get; set; }
    public string? Summary { get; set; }
    public string Author { get; set; }

    public DateTime PublishedAt { get; set; }

    public string? OriginalFeed { get; set; }

    public object GetFormatterData(string currentFeed)
    {
        return new
        {
            Id,
            Link,
            Title = HttpUtility.HtmlEncode(Title),
            Summary,
            PublishedAt,
            Author = HttpUtility.HtmlEncode(Author),
            AuthorFirstWord = HttpUtility.HtmlEncode(Author.Split(' ').First()),
            FeedName = HttpUtility.HtmlEncode(currentFeed),
            OriginalFeedName = HttpUtility.HtmlEncode(OriginalFeed),
        };
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode() ^ Title.GetHashCode() ^ Author.GetHashCode() ^ PublishedAt.GetHashCode();
    }
}
