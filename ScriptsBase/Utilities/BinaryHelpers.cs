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
    private const string AssumedSelfSignedCertificateName = "SelfSigned";

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
            // -S flag is needed to not remove too much (so only debug symbols are removed)
            // It might be safe to experiment in the future with removing the S flag, but it doesn't save that many
            // more megabytes per Thrive release
            startInfo.ArgumentList.Add("-urS");
        }

        startInfo.ArgumentList.Add(file);

        var result = await ProcessRunHelpers.RunProcessAsync(startInfo, cancellationToken, true);

        if (result.ExitCode != 0)
        {
            ColourConsole.WriteErrorLine($"Failed to run 'strip' command (is it installed?): {result.FullOutput}");
            throw new Exception($"Strip command failed, {result.ExitCode}");
        }
    }

    /// <summary>
    ///   Performs Mac code signing on a binary file
    /// </summary>
    /// <param name="filePath">File to sign</param>
    /// <param name="entitlementsFile">What entitlements to use when signing</param>
    /// <param name="signingCertificate">Certificate name or null for self-signed</param>
    /// <param name="cancellationToken">Cancellation</param>
    /// <returns>True on success</returns>
    public static async Task<bool> SignFileForMac(string filePath, string entitlementsFile, string? signingCertificate,
        CancellationToken cancellationToken)
    {
        ColourConsole.WriteNormalLine($"Signing {filePath}");

        var startInfo = new ProcessStartInfo("xcrun");
        startInfo.ArgumentList.Add("codesign");
        startInfo.ArgumentList.Add("--force");
        startInfo.ArgumentList.Add("--verbose");
        startInfo.ArgumentList.Add("--timestamp");

        startInfo.ArgumentList.Add("--sign");

        AddCodesignName(startInfo, signingCertificate);

        startInfo.ArgumentList.Add("--options=runtime");
        startInfo.ArgumentList.Add("--entitlements");
        startInfo.ArgumentList.Add(entitlementsFile);
        startInfo.ArgumentList.Add(filePath);

        var result = await ProcessRunHelpers.RunProcessAsync(startInfo, cancellationToken, false);

        if (result.ExitCode != 0)
        {
            ColourConsole.WriteErrorLine($"Running codesign on '{filePath}' failed. " +
                "Are xcode tools installed and do you have the right certificates installed / " +
                "self-signed certificate created? A self signed certificate might also be expired.");
            return false;
        }

        ColourConsole.WriteDebugLine("Code signing succeeded");

        return true;
    }

    public static void AddCodesignName(ProcessStartInfo startInfo, string? signingCertificate)
    {
        if (!string.IsNullOrEmpty(signingCertificate))
        {
            startInfo.ArgumentList.Add(signingCertificate);
        }
        else
        {
            startInfo.ArgumentList.Add(AssumedSelfSignedCertificateName);
        }
    }
}
