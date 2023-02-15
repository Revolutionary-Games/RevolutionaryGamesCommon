namespace ScriptsBase.Checks.FileTypes;

using System.Collections.Generic;
using System.Text.RegularExpressions;

public class TscnCheck : LineByLineFileChecker
{
    /// <summary>
    ///   Pretty generous, so can't detect like small models with only a few vertices, as text etc. is on a single line
    /// </summary>
    private const int SCENE_EMBEDDED_LENGTH_HEURISTIC = 920;

    private const int NODE_NAME_UPPERCASE_REQUIRED_LENGTH = 25;
    private const int NODE_NAME_UPPERCASE_ACRONYM_ALLOWED_LENGTH = 4;

    private static readonly Regex NodeNameRegex = new(@"\[node\s+name=""([^""]+)""");

    private static readonly Regex EmbeddedFontSignature = new(@"sub_resource type=""DynamicFont""");

    private static readonly Regex Whitespace = new(@"\s");
    private static readonly Regex StartsWithUppercaseLetter = new(@"^[A-Z]");

    public TscnCheck() : base(".tscn")
    {
    }

    protected override IEnumerable<string> CheckLine(string line, int lineNumber)
    {
        if (line.Length > SCENE_EMBEDDED_LENGTH_HEURISTIC)
        {
            yield return FormatErrorLineHelper(lineNumber, "probably has an embedded resource. " +
                $"Length {line.Length} is over heuristic value of {SCENE_EMBEDDED_LENGTH_HEURISTIC}");
        }

        if (EmbeddedFontSignature.IsMatch(line))
        {
            yield return FormatErrorLineHelper(lineNumber, "contains an embedded font. " +
                "Don't embed fonts in scenes, instead place font resources in a separate .tres");
        }

        var match = NodeNameRegex.Match(line);

        if (match.Success)
        {
            var name = match.Groups[1].Value;

            if (Whitespace.IsMatch(name))
            {
                yield return FormatErrorLineHelper(lineNumber, $"contains a name ({name}) that has a space.");
            }

            if (name.Contains('_'))
            {
                yield return FormatErrorLineHelper(lineNumber, $"contains a name ({name}) that has an underscore.");
            }

            // Single word names can be without upper case letters so we use a length heuristic here
            if (name.Length > NODE_NAME_UPPERCASE_REQUIRED_LENGTH && name.ToLowerInvariant() == name)
            {
                yield return FormatErrorLineHelper(lineNumber,
                    $"contains a name ({name}) that has no capital letters.");
            }

            // Short acronyms are ignored here if the name starts with a capital
            // TODO: in the future might only want to allow node names that start with a lowercase letter
            if (name.ToUpperInvariant() == name && (name.Length > NODE_NAME_UPPERCASE_ACRONYM_ALLOWED_LENGTH ||
                    !StartsWithUppercaseLetter.IsMatch(name)))
            {
                yield return FormatErrorLineHelper(lineNumber,
                    $"contains a name ({name}) that doesn't contain lowercase letters (and isn't a short acronym).");
            }
        }
    }
}
