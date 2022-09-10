namespace ScriptsBase.Checks;

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using SharedBase.Utilities;

/// <summary>
///   Checks that the project compiles without errors
/// </summary>
public class CompileCheck : CodeCheck
{
    public override async Task Run(CodeCheckRun runData, CancellationToken cancellationToken)
    {
        if (runData.SolutionFile == null)
            throw new ArgumentException("Run data has no solution file configured");

        var startInfo = new ProcessStartInfo("dotnet");
        startInfo.ArgumentList.Add("build");
        startInfo.ArgumentList.Add(runData.SolutionFile);

        // Our files are locked on Windows when scripts are executing so we can't do a full rebuild
        // TODO: find a better workaround. See: https://github.com/Revolutionary-Games/Thrive/issues/3766
        if (OperatingSystem.IsWindows())
        {
            runData.OutputWarningWithMutex(
                "NOTE: on Windows this compile check can't rebuild due to the scripts being in use currently. " +
                "As such you should manually rebuild with warnings enabled to make sure there aren't warnings");
            runData.OutputInfoWithMutex("See: https://github.com/Revolutionary-Games/Thrive/issues/3766");

            startInfo.ArgumentList.Add("/t:Build");
        }
        else
        {
            startInfo.ArgumentList.Add("/t:Clean,Build");
        }

        startInfo.ArgumentList.Add("/warnaserror");

        var mutex = runData.BuildMutex;

        await mutex.WaitAsync(cancellationToken);
        try
        {
            runData.OutputInfoWithMutex("Building with warnings");
            var result = await ProcessRunHelpers.RunProcessAsync(startInfo, cancellationToken);

            if (result.ExitCode == 0)
            {
                runData.OutputTextWithMutex("Build finished with no warnings");
                return;
            }

            runData.OutputInfoWithMutex("Build output from dotnet:");
            runData.OutputTextWithMutex(result.FullOutput);
            runData.ReportError("\nBuild generated warnings or errors.");
        }
        finally
        {
            mutex.Release();
        }
    }
}
