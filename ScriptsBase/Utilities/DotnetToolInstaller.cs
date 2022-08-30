namespace ScriptsBase.Utilities;

using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using SharedBase.Utilities;

public static class DotnetToolInstaller
{
    public static async Task<bool> InstallDotnetTools()
    {
        var startInfo = new ProcessStartInfo("dotnet");
        startInfo.ArgumentList.Add("tool");
        startInfo.ArgumentList.Add("restore");

        var result = await ProcessRunHelpers.RunProcessAsync(startInfo, CancellationToken.None, false);

        return result.ExitCode == 0;
    }
}
