namespace ScriptsBase.Utilities;

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharedBase.Utilities;

/// <summary>
///   Utilities related to executable binary files
/// </summary>
public static class BinaryHelpers
{
    public const string ThriveMacMainExecutable = "Thrive.app/Contents/MacOS/Thrive";
    private const string AssumedSelfSignedCertificateName = "SelfSigned";

    public static async Task Strip(string file, CancellationToken cancellationToken)
    {
        ColourConsole.WriteNormalLine($"Stripping {file}");

        var startInfo = new ProcessStartInfo("strip");

        if (!OperatingSystem.IsMacOS())
        {
            // It seems pretty random when the stripped binaries are accepted by the crash dumping to detect the symbol
            // file to use when stack-walking, for now this makes as small builds as official Godot
            startInfo.ArgumentList.Add("--keep-section=.hash");
            startInfo.ArgumentList.Add("--keep-section=.gnu.hash");
        }
        else
        {
            // Mac strip command isn't the gnu version, so it needs special handling
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

    /// <summary>
    ///   Runs the notarization tool on an app and applies the signature (ticket). Must be run on a .zip or .dmg file.
    /// </summary>
    /// <param name="pathToApp">Path to the .app folder</param>
    /// <param name="teamId">Team ID to use when signing</param>
    /// <param name="appleId">Apple developer account email address</param>
    /// <param name="appleAppPassword">App-specific password for the account</param>
    /// <param name="cancellationToken">Cancellation</param>
    /// <param name="staple">If false won't staple automatically</param>
    /// <returns>True on success</returns>
    public static async Task<bool> NotarizeFile(string pathToApp, string teamId, string appleId,
        string appleAppPassword, CancellationToken cancellationToken, bool staple = true)
    {
        if (!File.Exists(pathToApp) || Directory.Exists(pathToApp))
            throw new ArgumentException("File to notarize doesn't exist", nameof(pathToApp));

        ColourConsole.WriteNormalLine($"Notarizing {pathToApp}");

        var startInfo = new ProcessStartInfo("xcrun");
        startInfo.ArgumentList.Add("notarytool");
        startInfo.ArgumentList.Add("submit");
        startInfo.ArgumentList.Add(pathToApp);
        startInfo.ArgumentList.Add("--apple-id");
        startInfo.ArgumentList.Add(appleId);
        startInfo.ArgumentList.Add("--password");
        startInfo.ArgumentList.Add(appleAppPassword);
        startInfo.ArgumentList.Add("--team-id");
        startInfo.ArgumentList.Add(teamId);
        startInfo.ArgumentList.Add("--wait");

        var result = await ProcessRunHelpers.RunProcessAsync(startInfo, cancellationToken, false);

        if (result.ExitCode != 0)
        {
            ColourConsole.WriteErrorLine($"Running notarization on '{pathToApp}' failed. " +
                "Are xcode tools installed and do you have the right account credentials entered?");
            return false;
        }

        if (!staple)
        {
            ColourConsole.WriteInfoLine("Notarization succeeded, but not stapling the ticket.");
            return true;
        }

        ColourConsole.WriteInfoLine("Notarization succeeded, stapling the ticket.");

        var isZip = pathToApp.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);

        if (isZip)
        {
            ColourConsole.WriteInfoLine("This needs to unzip and re-zip the file to apply notarization");

            // Extract the file name and directory
            var fileName = Path.GetFileNameWithoutExtension(pathToApp);
            var directory = Path.GetDirectoryName(pathToApp) ?? string.Empty;

            var notarizedZip = await ExtractAndNotarizeAppZip(pathToApp, cancellationToken, directory, fileName);

            if (string.IsNullOrEmpty(notarizedZip))
            {
                ColourConsole.WriteErrorLine("Failed to extract and re-zip the file");
                return false;
            }

            // Replace the original zip with the new one
            File.Delete(pathToApp);
            File.Move(notarizedZip, pathToApp);
        }
        else
        {
            ColourConsole.WriteDebugLine("Doing a raw staple");

            if (!await StapleFileTicket(pathToApp, cancellationToken))
            {
                ColourConsole.WriteErrorLine("Stapling ticket failed");
                return false;
            }
        }

        ColourConsole.WriteSuccessLine("Notarization completed");
        return true;
    }

    public static async Task<bool> StapleFileTicket(string pathToFile, CancellationToken cancellation)
    {
        var startInfo = new ProcessStartInfo("xcrun");
        startInfo.ArgumentList.Add("stapler");
        startInfo.ArgumentList.Add("staple");
        startInfo.ArgumentList.Add(pathToFile);

        var result = await ProcessRunHelpers.RunProcessAsync(startInfo, cancellation, false);

        if (result.ExitCode != 0)
        {
            ColourConsole.WriteErrorLine($"Running stapler on '{pathToFile}' failed. " +
                "Are xcode tools installed and was the file notarized already?");
            return false;
        }

        ColourConsole.WriteDebugLine("Stapling ticket succeeded");
        return true;
    }

    public static async Task<bool> SignThriveAppMac(string folder, string basePathToThrive, string entitlements, 
        string? signingKey, CancellationToken cancellationToken)
    {
        var main = Path.Join(basePathToThrive, ThriveMacMainExecutable);

        ColourConsole.WriteInfoLine("Signing all parts of the Mac build");
        ColourConsole.WriteNormalLine("This may take a while as there are many items");

        foreach (var item in Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories))
        {
            // Skip stuff that shouldn't be signed
            // TODO: would it offer any extra security if the .pck file was signed as well?
            if (item.EndsWith(".txt") || item.EndsWith(".pck") || item.EndsWith(".md") || item.EndsWith(".7z"))
            {
                continue;
            }

            // The main executable must be signed last
            if (item.EndsWith(ThriveMacMainExecutable))
                continue;

            if (!await SignFileForMac(item, entitlements, signingKey, cancellationToken))
            {
                ColourConsole.WriteErrorLine($"Failed to sign part of Mac build: {item}");
                return false;
            }
        }

        ColourConsole.WriteSuccessLine("Successfully signed individual parts");

        // Sign the main file last
        if (!await SignFileForMac(main, entitlements, signingKey, cancellationToken))
        {
            ColourConsole.WriteErrorLine("Failed to sign main file of Mac build");
            return false;
        }

        ColourConsole.WriteSuccessLine("Signed the main file");
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

    private static async Task<string?> ExtractAndNotarizeAppZip(string pathToApp, CancellationToken cancellationToken,
        string directory, string fileName)
    {
        // Create a temporary extraction directory
        var extractDir = Path.Combine(directory, $"{fileName}_extracted");

        if (Directory.Exists(extractDir))
        {
            ColourConsole.WriteWarningLine($"Deleting existing extraction directory: {extractDir}");
            Directory.Delete(extractDir, true);
        }

        Directory.CreateDirectory(extractDir);

        string notarizedZip;
        try
        {
            // Unzip the file
            var unzipProcess = new ProcessStartInfo("unzip");
            unzipProcess.ArgumentList.Add(pathToApp);
            unzipProcess.ArgumentList.Add("-d");
            unzipProcess.ArgumentList.Add(extractDir);

            ColourConsole.WriteNormalLine("Unzipping the app to apply the ticket...");
            var unzipResult = await ProcessRunHelpers.RunProcessAsync(unzipProcess, cancellationToken, true);

            if (unzipResult.ExitCode != 0)
            {
                ColourConsole.WriteErrorLine($"Failed to unzip '{pathToApp}': {unzipResult.FullOutput}");
                return null;
            }

            // Find the .app directory in the extracted contents
            var appPath = Directory.GetDirectories(extractDir, "*.app", SearchOption.AllDirectories)
                .FirstOrDefault();

            if (appPath == null)
            {
                ColourConsole.WriteErrorLine("Could not find .app directory in the extracted .zip");
                return null;
            }

            // Staple the ticket to the .app
            if (!await StapleFileTicket(appPath, cancellationToken))
            {
                ColourConsole.WriteErrorLine("Stapling ticket to extracted .app failed");
                return null;
            }

            // Re-zip with maximum compression level (-9)
            notarizedZip = Path.Combine(directory, $"{fileName}_notarized.zip");

            var zipProcess = new ProcessStartInfo("zip")
            {
                WorkingDirectory = extractDir,
            };

            zipProcess.ArgumentList.Add("-9");
            zipProcess.ArgumentList.Add("-r");
            zipProcess.ArgumentList.Add(Path.Combine("..", Path.GetFileName(notarizedZip)));

            // Zip all items back up
            foreach (var entry in Directory.GetFileSystemEntries(extractDir, "*", SearchOption.TopDirectoryOnly))
            {
                zipProcess.ArgumentList.Add(Path.GetFileName(entry));
            }

            ColourConsole.WriteNormalLine("Re-zipping the app...");
            var zipResult = await ProcessRunHelpers.RunProcessAsync(zipProcess, cancellationToken, true);

            if (zipResult.ExitCode != 0)
            {
                ColourConsole.WriteErrorLine($"Failed to re-zip the app: {zipResult.FullOutput}");
                return null;
            }

            ColourConsole.WriteNormalLine($"Successfully created notarized zip: {notarizedZip}");
        }
        finally
        {
            try
            {
                // Clean up the extraction directory
                if (Directory.Exists(extractDir))
                    Directory.Delete(extractDir, true);
            }
            catch (Exception e)
            {
                ColourConsole.WriteWarningLine($"Failed to clean up extraction directory: {e.Message}");
            }
        }

        return notarizedZip;
    }


}
