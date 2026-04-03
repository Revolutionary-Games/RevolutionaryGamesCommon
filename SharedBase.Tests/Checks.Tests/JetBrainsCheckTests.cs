namespace SharedBase.Tests.Checks.Tests;

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ScriptsBase.Checks;
using Xunit;

public class JetBrainsCheckTests
{
    [Fact]
    public void JetBrainsChecksUseNoBuildOnWindows()
    {
        using var runData = new CodeCheckRun();
        var startInfo = new ProcessStartInfo("dotnet");

        TestJetBrainsCheck.ConfigureBuildMode(runData, startInfo, true);

        Assert.Contains("--no-build", startInfo.ArgumentList);
        Assert.DoesNotContain("--build", startInfo.ArgumentList);
    }

    [Fact]
    public void JetBrainsChecksUseBuildOnNonWindows()
    {
        using var runData = new CodeCheckRun();
        var startInfo = new ProcessStartInfo("dotnet");

        TestJetBrainsCheck.ConfigureBuildMode(runData, startInfo, false);

        Assert.Contains("--build", startInfo.ArgumentList);
        Assert.DoesNotContain("--no-build", startInfo.ArgumentList);
    }

    private sealed class TestJetBrainsCheck : JetBrainsCheck
    {
        public static void ConfigureBuildMode(CodeCheckRun runData, ProcessStartInfo startInfo, bool isWindows)
        {
            AddJetbrainsToolRunBuildMode(runData, startInfo, isWindows);
        }

        protected override Task RunJetBrainsTool(CodeCheckRun runData, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
