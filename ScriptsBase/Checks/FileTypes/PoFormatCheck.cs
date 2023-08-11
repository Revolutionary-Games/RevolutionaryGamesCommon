namespace ScriptsBase.Checks.FileTypes;

using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
///   Checks that the base file structure etc. is fine. There's a separate check for the content making sense
///   <see cref="PoContentCheck"/>
/// </summary>
public class PoFormatCheck : LineByLineFileChecker
{
    public static readonly Regex FuzzyTranslationRegex = new("^#, fuzzy");
    private static readonly Regex TrailingSpace = new(@"(?<=\S)[\t ]+$");

    // Unused now with the used proper gettext parser
    // private static readonly Regex MsgIdRegex = new(@"^msgid ""(.*)""$");
    // private static readonly Regex MsgStrRegex = new(@"^msgstr ""(.*)""$");
    // private static readonly Regex PlainQuotedMessage = new(@"^""(.*)""");
    // private static readonly Regex GettextHeaderName = new(@"^([\w-]+):\s+");

    private bool isEnglish;

    public PoFormatCheck() : base(".po")
    {
    }

    public override IAsyncEnumerable<string> Handle(string path)
    {
        isEnglish = path.EndsWith("en.po");

        return base.Handle(path);
    }

    protected override IEnumerable<string> CheckLine(string line, int lineNumber)
    {
        if (isEnglish && FuzzyTranslationRegex.IsMatch(line))
        {
            yield return FormatErrorLineHelper(lineNumber,
                "has fuzzy (marked needing changes) translation, not allowed for en");
        }

        // Could only check in headers, but checking everywhere just adds only 200 milliseconds to total runtime so
        // all lines are checked
        // Note: that measurement was in the ruby version of the script, this C# version is probably not much slower
        if (TrailingSpace.IsMatch(line))
        {
            yield return FormatErrorLineHelper(lineNumber, "has trailing space");
        }

        // TODO: should the duplicate msgid and blank msgid checking be added here
    }
}
