namespace ScriptsBase.Checks.FileTypes;

using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
///   Checker for "project.godot" files
/// </summary>
public class ProjectGodotCheck : LineByLineFileChecker
{
    private static readonly Regex ProjectVersionLine = new(@"config\/version=""([\d.]+)""");

    private readonly string requiredVersion;

    private bool seenVersionNumber;

    public ProjectGodotCheck(string requiredVersion) : base(".godot")
    {
        this.requiredVersion = requiredVersion;
    }

    public override async IAsyncEnumerable<string> Handle(string path)
    {
        seenVersionNumber = false;

        await foreach (var result in base.Handle(path))
        {
            yield return result;
        }

        if (!seenVersionNumber)
            yield return "No line specifying version number was found";
    }

    protected override IEnumerable<string> CheckLine(string line, int lineNumber)
    {
        var match = ProjectVersionLine.Match(line);

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
