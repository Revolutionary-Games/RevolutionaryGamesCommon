namespace ScriptsBase.Checks.FileTypes;

using System.Collections.Generic;
using System.Text.RegularExpressions;

public class GitConflictMarkerCheck : LineByLineFileChecker
{
    public static readonly Regex GitMergeConflictMarkers = new(@"^<{7}\s+\S+\s*$");

    public GitConflictMarkerCheck() : base(string.Empty)
    {
    }

    public override bool HandlesFile(string file)
    {
        // Ignore binary files, we don't want to check for conflict markers there
        if (file.EndsWith(".dylib") || file.EndsWith(".dll") || file.EndsWith(".so"))
            return false;

        return base.HandlesFile(file);
    }

    protected override IEnumerable<string> CheckLine(string line, int lineNumber)
    {
        if (GitMergeConflictMarkers.IsMatch(line))
        {
            yield return FormatErrorLineHelper(lineNumber, "contains a merge conflict marker");
        }
    }
}
