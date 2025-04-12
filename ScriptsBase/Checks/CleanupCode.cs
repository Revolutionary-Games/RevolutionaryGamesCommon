namespace ScriptsBase.Checks;

using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using SharedBase.Utilities;

public class CleanupCode : JetBrainsCheck
{
    public const string FULL_NO_XML_PROFILE = "full_no_xml";

    public string CleanUpProfile { get; set; } = FULL_NO_XML_PROFILE;

    protected override async Task RunJetBrainsTool(CodeCheckRun runData, CancellationToken cancellationToken)
    {
        var oldDiff = await GitRunHelpers.Diff("./", cancellationToken, true, true, true);

        var startInfo = new ProcessStartInfo("dotnet");
        startInfo.ArgumentList.Add("tool");
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("jb");
        startInfo.ArgumentList.Add("cleanupcode");
        startInfo.ArgumentList.Add(runData.SolutionFile!);
        startInfo.ArgumentList.Add($"--profile={CleanUpProfile}");
        startInfo.ArgumentList.Add($"--caches-home={JET_BRAINS_CACHE}");

        // TODO: check if the cleanupcode tool still doesn't run in parallel, and if so we should try to run multiple
        // tool instances at once (for example 4 at once)

        AddJetbrainsToolRunIncludes(runData, startInfo);

        AddJetbrainsToolRunExcludes(runData.ExtraIgnoredJetbrainsCleanUpWildcards, startInfo);

        var result = await ProcessRunHelpers.RunProcessAsync(startInfo, cancellationToken, JET_BRAINS_CAPTURE_OUTPUT);

        if (result.ExitCode != 0)
        {
            ReportRunFailure(result, "cleanupcode", runData);
            return;
        }

        var newDiff = await GitRunHelpers.Diff("./", cancellationToken, true, true, true);

        if (newDiff != oldDiff)
        {
            runData.ReportError("Code cleanup performed changes, please stage / check them before committing");
        }
        else
        {
            runData.OutputTextWithMutex("cleanupcode didn't detect any problems");
        }
    }
}
