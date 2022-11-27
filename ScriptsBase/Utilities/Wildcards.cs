namespace ScriptsBase.Utilities;

using System.Text.RegularExpressions;

public static class Wildcards
{
    /// <summary>
    ///   Converts a wildcard pattern to a regex
    /// </summary>
    /// <param name="wildcardPattern">The wildcard to convert</param>
    /// <returns>The resulting regex</returns>
    public static Regex ConvertToRegex(string wildcardPattern)
    {
        var regexPattern = Regex.Escape(wildcardPattern).Replace("\\?", ".").Replace("\\*", ".*");

        return new Regex($"^{regexPattern}$");
    }
}
