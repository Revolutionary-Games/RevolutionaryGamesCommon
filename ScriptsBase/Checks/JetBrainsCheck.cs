namespace ScriptsBase.Checks;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using SharedBase.Utilities;
using Utilities;

/// <summary>
///   Base for all JetBrains tools based checks
/// </summary>
public abstract class JetBrainsCheck : CodeCheck
{
    public const string JET_BRAINS_CACHE = ".jetbrains-cache";

    // TODO: should we capture output?
    public const bool JET_BRAINS_CAPTURE_OUTPUT = false;

    public static bool ShouldRunJetBrainsCheck(IEnumerable<string>? filesToCheck, CodeCheckRun runLogging)
    {
        if (filesToCheck == null)
            return true;

        if (OnlyChangedFileDetector.IncludesChangesToFileType(".cs", filesToCheck))
            return true;

        // TODO: probably should check anyway if changes to .csproj files

        runLogging.OutputInfoWithMutex("No changes to be checked for .cs files");
        return false;
    }

    public override async Task Run(CodeCheckRun runData, CancellationToken cancellationToken)
    {
        if (!ShouldRunJetBrainsCheck(runData.OnlyCheckFiles, runData))
            return;

        if (runData.SolutionFile == null)
            throw new ArgumentException("No solution file specified");

        await runData.CheckDotnetTools();

        var mutex = runData.BuildMutex;

        await mutex.WaitAsync(cancellationToken);
        try
        {
            await RunJetBrainsTool(runData, cancellationToken);
        }
        finally
        {
            mutex.Release();
        }
    }

    protected abstract Task RunJetBrainsTool(CodeCheckRun runData, CancellationToken cancellationToken);

    protected static void AddJetbrainsToolRunIncludes(CodeCheckRun runData, ProcessStartInfo startInfo)
    {
        if (runData.OnlyCheckFiles != null)
        {
            var formattedIncludes = string.Join(';', runData.OnlyCheckFiles);
            startInfo.ArgumentList.Add($"--include={formattedIncludes}");
        }
    }

    protected static void ReportRunFailure(ProcessRunHelpers.ProcessResult result, string tool, CodeCheckRun runData)
    {
        // ReSharper disable HeuristicUnreachableCode
#pragma warning disable CS0162
        if (JET_BRAINS_CAPTURE_OUTPUT)
            runData.OutputTextWithMutex(result.FullOutput);
#pragma warning restore CS0162

        // ReSharper restore HeuristicUnreachableCode

        runData.ReportError($"Failed to run JetBrains {tool}");
    }
}
