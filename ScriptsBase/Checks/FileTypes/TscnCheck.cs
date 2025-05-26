namespace ScriptsBase.Checks.FileTypes;

using System.Collections.Generic;
using System.Text.RegularExpressions;

public class TscnCheck : LineByLineFileChecker
{
    public const string TAB_CONTROL_TYPE = "Tabs";

    public static readonly Regex GodotNodeRegex = new(@"^\[node\sname=""([^""]+)""\s(?:type=""([^""]+)"")?",
        RegexOptions.Compiled);

    /// <summary>
    ///   Pretty generous, so can't detect like small models with only a few vertices, as text etc. is on a single line
    /// </summary>
    private const int SCENE_EMBEDDED_LENGTH_HEURISTIC = 920;

    private const string SCENE_NODE_LIST_INDICATOR = "node_paths=PackedStringArray";
    private const int SCENE_NODE_LIST_MAX_LENGTH = 5000;

    private const int NODE_NAME_UPPERCASE_REQUIRED_LENGTH = 25;
    private const int NODE_NAME_UPPERCASE_ACRONYM_ALLOWED_LENGTH = 4;

    private const string AssetsFolder = "assets/";

    private static readonly Regex NameWithUnderScoreSuffixNumber = new(@"_\d+$", RegexOptions.Compiled);

    private static readonly Regex EmbeddedFontSignature =
        new(@"sub_resource type=""DynamicFont""", RegexOptions.Compiled);

    private static readonly Regex Whitespace = new(@"\s", RegexOptions.Compiled);
    private static readonly Regex StartsWithUppercaseLetter = new("^[A-Z]", RegexOptions.Compiled);

    private readonly bool allowUnderlineNumberSuffixInAssets;

    /// <summary>
    ///   Create new .tscn file check
    /// </summary>
    /// <param name="allowUnderlineNumberSuffixInAssets">
    ///   When true assets are allowed to contain node names like "Armature_001", this is to help with imported scenes
    ///   conforming to the requirements.
    /// </param>
    public TscnCheck(bool allowUnderlineNumberSuffixInAssets = true) : base(".tscn")
    {
        this.allowUnderlineNumberSuffixInAssets = allowUnderlineNumberSuffixInAssets;
    }

    protected override IEnumerable<string> CheckLine(string line, int lineNumber)
    {
        if (line.Length > SCENE_EMBEDDED_LENGTH_HEURISTIC)
        {
            // Allow export node lists to be longer for big scenes that need a ton of export variables
            if (!line.Contains(SCENE_NODE_LIST_INDICATOR) || line.Length > SCENE_NODE_LIST_MAX_LENGTH)
            {
                if (line.Contains(SCENE_NODE_LIST_INDICATOR))
                {
                    yield return FormatErrorLineHelper(lineNumber, "has an unusual length. " +
                        $"Length {line.Length} is over heuristic value of {SCENE_NODE_LIST_MAX_LENGTH}");
                }
                else
                {
                    yield return FormatErrorLineHelper(lineNumber, "probably has an embedded resource. " +
                        $"Length {line.Length} is over heuristic value of {SCENE_EMBEDDED_LENGTH_HEURISTIC}");
                }
            }
        }

        if (EmbeddedFontSignature.IsMatch(line))
        {
            yield return FormatErrorLineHelper(lineNumber, "contains an embedded font. " +
                "Don't embed fonts in scenes, instead place font resources in a separate .ttf file and create a " +
                "label settings resource");
        }

        var match = GodotNodeRegex.Match(line);

        if (match.Success)
        {
            var name = match.Groups[1].Value;

            if (Whitespace.IsMatch(name))
            {
                yield return FormatErrorLineHelper(lineNumber, $"contains a name ({name}) that has a space.");
            }

            if (match.Groups.Count > 2)
            {
                var type = match.Groups[2].Value;

                if (type == TAB_CONTROL_TYPE)
                {
                    // Ignore name requirements on tabs control names
                    yield break;
                }
            }

            if (name.Contains('_'))
            {
                if (!NameWithUnderScoreSuffixNumber.IsMatch(name) || !allowUnderlineNumberSuffixInAssets ||
                    !currentFile.StartsWith(AssetsFolder))
                {
                    yield return FormatErrorLineHelper(lineNumber,
                        $"contains a name ({name}) that has an underscore.");
                }
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
