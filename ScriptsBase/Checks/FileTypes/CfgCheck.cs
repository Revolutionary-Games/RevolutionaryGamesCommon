namespace ScriptsBase.Checks.FileTypes;

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public class CfgCheck : LineByLineFileChecker
{
    private static readonly Regex CfgVersionLine = new(@"[/_]version=""([\d.]+)""");

    private readonly string requiredVersion;

    private bool seenVersionNumber;

    public CfgCheck(string requiredVersion) : base(".cfg")
    {
        this.requiredVersion = requiredVersion;
    }

    public override async IAsyncEnumerable<string> Handle(string path)
    {
        if (requiredVersion.Count(c => c == '.') != 3)
        {
            yield return $"Game version number should always specify all 4 parts of the version (version is " +
                $"instead: {requiredVersion}). " +
                "This error is not from this file but passed in from project version file.";

            // No point in checking against bad data
            yield break;
        }

        seenVersionNumber = false;

        await foreach (var result in base.Handle(path))
        {
            yield return result;
        }

        if (!seenVersionNumber)
            yield return "No line specifying version numbers was found";
    }

    protected override IEnumerable<string> CheckLine(string line, int lineNumber)
    {
        var match = CfgVersionLine.Match(line);

        if (match.Success)
        {
            // Ignore false positives from Mac export properties
            if (line.Contains("xcode/") || line.Contains("macos_version"))
                yield break;

            seenVersionNumber = true;

            var value = match.Groups[1].Value;

            if (value != requiredVersion)
            {
                yield return FormatErrorLineHelper(lineNumber,
                    $"has incorrect version. {value} is not equal to {requiredVersion}");
            }
        }
    }
}
