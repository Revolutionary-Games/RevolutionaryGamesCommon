namespace ScriptsBase.Utilities;

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using SharedBase.Utilities;

/// <summary>
///   Utilities related to executable binary files
/// </summary>
public static class BinaryHelpers
{
    public static async Task Strip(string file, CancellationToken cancellationToken)
    {
        ColourConsole.WriteNormalLine($"Stripping {file}");

        var startInfo = new ProcessStartInfo("strip");

        if (!OperatingSystem.IsMacOS())
        {
            // Seems pretty random when the stripped binaries are accepted by the crash dumping to detect the symbol
            // file to use when stack-walking, for now this makes as small builds as official Godot
            startInfo.ArgumentList.Add("--keep-section=.hash");
            startInfo.ArgumentList.Add("--keep-section=.gnu.hash");
        }
        else
        {
            // Mac strip command isn't the gnu version so it needs special handling
            // Keep undefined and dynamically referenced symbols and remove all debug symbols
            // If not specifying the -S flag then the strip command seems to remove more (but maybe too much)
            startInfo.ArgumentList.Add("-ur");

            // startInfo.ArgumentList.Add("-urS");
        }

        startInfo.ArgumentList.Add(file);

        var result = await ProcessRunHelpers.RunProcessAsync(startInfo, cancellationToken, true);

        if (result.ExitCode != 0)
        {
            ColourConsole.WriteErrorLine($"Failed to run 'strip' command (is it installed?): {result.FullOutput}");
            throw new Exception($"Strip command failed, {result.ExitCode}");
        }
    }
}
