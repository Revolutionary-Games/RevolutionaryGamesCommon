namespace SharedBase.Utilities;

public static class StringExtensions
{
    // TODO: change this to the utf-8 truncate character (though this might maybe need to be context specific
    // or at least the launcher fonts needs to be checked to make sure that they have the right character)
    public const string TruncateText = "...";

    public static string Truncate(this string? str, int length = 30)
    {
        if (str == null)
            return string.Empty;

        if (str.Length <= length)
        {
            return str;
        }

        return str.Substring(0, length - TruncateText.Length) + TruncateText;
    }

    public static string TruncateWithoutEllipsis(this string? str, int length)
    {
        if (str == null)
            return string.Empty;

        if (str.Length <= length)
        {
            return str;
        }

        return str.Substring(0, length);
    }
}
