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
        startInfo.ArgumentList.Add("/t:Clean,Build");
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
