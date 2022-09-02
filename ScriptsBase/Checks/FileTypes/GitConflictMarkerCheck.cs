namespace ScriptsBase.Checks.FileTypes;

using System.Collections.Generic;
using System.Text.RegularExpressions;

public class GitConflictMarkerCheck : LineByLineFileChecker
{
    public static readonly Regex GitMergeConflictMarkers = new(@"^<{7}\s+\S+\s*$");

    public GitConflictMarkerCheck() : base(string.Empty)
    {
    }

    protected override IEnumerable<string> CheckLine(string line, int lineNumber)
    {
        if (GitMergeConflictMarkers.IsMatch(line))
        {
            yield return FormatErrorLineHelper(lineNumber, "contains a merge conflict marker");
        }
    }
}
