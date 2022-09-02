namespace ScriptsBase.Checks.FileTypes;

using System.Collections.Generic;
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
        seenVersionNumber = false;

        await foreach (var result in base.Handle(path))
            yield return result;

        if (!seenVersionNumber)
            yield return "No line specifying version numbers was found";
    }

    protected override IEnumerable<string> CheckLine(string line, int lineNumber)
    {
        var match = CfgVersionLine.Match(line);

        if (match.Success)
        {
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
