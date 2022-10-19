namespace SharedBase.Utilities;

using System;

public static class UriExtensions
{
    /// <summary>
    ///   Returns the parts of the Uri before the query part of the Url
    /// </summary>
    /// <param name="uri">The uri to get without the query parameters part</param>
    /// <returns>The Uri until the first "?" character</returns>
    public static string WithoutQuery(this Uri uri)
    {
        return uri.ToString().UriWithoutQuery();
    }

    public static string UriWithoutQuery(this string uri)
    {
        var separator = uri.IndexOf('?');

        if (separator >= 0)
        {
            return uri.Substring(0, separator);
        }

        return uri;
    }
}
