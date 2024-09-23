namespace SharedBase.Utilities;

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using SHA3.Net;

public class FileUtilities
{
    /// <summary>
    ///   Calculates the size of all files in a folder recursively
    /// </summary>
    /// <param name="path">The folder to calculate size for</param>
    /// <returns>The size in bytes</returns>
    [UnsupportedOSPlatform("browser")]
    public static long CalculateFolderSize(string path)
    {
        if (!Directory.Exists(path))
            return 0;

        long size = 0;

        foreach (var file in Directory.EnumerateFiles(path))
        {
            size += new FileInfo(file).Length;
        }

        foreach (var folder in Directory.EnumerateDirectories(path))
        {
            size += CalculateFolderSize(folder);
        }

        return size;
    }

    /// <summary>
    ///   Opens a folder in the current platform's default viewer (explorer.exe, a Linux file browser etc.)
    /// </summary>
    /// <param name="folder">The folder to open</param>
    [UnsupportedOSPlatform("browser")]
    public static void OpenFolderInPlatformSpecificViewer(string folder)
    {
        folder = folder.Replace('/', Path.DirectorySeparatorChar);

        if (!folder.EndsWith(Path.DirectorySeparatorChar))
            folder += Path.DirectorySeparatorChar;

        Process.Start(new ProcessStartInfo
        {
            FileName = folder,
            UseShellExecute = true,
            Verb = "open",
        });
    }

    /// <summary>
    ///   Opens a folder or file in the current platform's default program
    /// </summary>
    /// <param name="fileOrFolder">The folder or file to open</param>
    [UnsupportedOSPlatform("browser")]
    public static void OpenFileOrFolderInDefaultProgram(string fileOrFolder)
    {
        if (!fileOrFolder.StartsWith("file://"))
            fileOrFolder = $"file://{Path.GetFullPath(fileOrFolder)}";

        Process.Start(new ProcessStartInfo
        {
            FileName = fileOrFolder,
            UseShellExecute = true,
            Verb = "open",
        });
    }

    /// <summary>
    ///   Finds the first subfolder in folder
    /// </summary>
    /// <param name="folder">The folder to look in. Needs to exist</param>
    /// <returns>The path to the first subfolder or null</returns>
    [UnsupportedOSPlatform("browser")]
    public static string? FindFirstSubFolder(string folder)
    {
        foreach (var subFolder in Directory.EnumerateDirectories(folder))
        {
            return subFolder;
        }

        return null;
    }

    public static async Task<byte[]> CalculateSha256OfFile(string file, CancellationToken cancellationToken)
    {
        await using var reader = File.Open(file, FileMode.Open, FileAccess.Read);

        return await SHA256.Create().ComputeHashAsync(reader, cancellationToken);
    }

    public static async Task<byte[]> CalculateSha3OfFile(string file, CancellationToken cancellationToken)
    {
        await using var reader = File.OpenRead(file);

        var sha3 = Sha3.Sha3256();
        return await sha3.ComputeHashAsync(reader, cancellationToken);
    }

    public static string HashToHex(byte[] hash)
    {
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    ///   Copies a file if the target doesn't exist or its hash is different.
    /// </summary>
    /// <param name="sourceFile">File to copy from</param>
    /// <param name="target">Target where to put the file (full path)</param>
    /// <param name="cancellationToken">Cancellation for this operation</param>
    /// <returns>True if copied, false if the target existed and hash was the same</returns>
    [UnsupportedOSPlatform("browser")]
    public static async Task<bool> CopyIfHashIsDifferent(string sourceFile, string target,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(target))
        {
            File.Copy(sourceFile, target, true);
            return true;
        }

        // Probably no harm in using a more expensive hash than sha256 here as this isn't used that much
        var originalHash = await CalculateSha3OfFile(sourceFile, cancellationToken);
        var targetHash = await CalculateSha3OfFile(target, cancellationToken);

        // Need to do a manual comparison
        bool equal = originalHash.Length == targetHash.Length;

        if (equal)
        {
            for (int i = 0; i < originalHash.Length; ++i)
            {
                if (originalHash[i] != targetHash[i])
                {
                    equal = false;
                    break;
                }
            }
        }

        if (equal)
        {
            // Same hash, so no need to copy
            return false;
        }

        File.Copy(sourceFile, target, true);
        return true;
    }

    /// <summary>
    ///   Moves a file if the target doesn't exist or its hash is different.
    /// </summary>
    /// <param name="sourceFile">File to copy from</param>
    /// <param name="target">Target where to put the file (full path)</param>
    /// <param name="cancellationToken">Cancellation for this operation</param>
    /// <returns>True if copied, false if the target existed and hash was the same</returns>
    [UnsupportedOSPlatform("browser")]
    public static async Task<bool> MoveIfHashIsDifferent(string sourceFile, string target,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(target))
        {
            File.Move(sourceFile, target, true);
            return true;
        }

        // Probably no harm in using a more expensive hash than sha256 here as this isn't used that much
        var originalHash = await CalculateSha3OfFile(sourceFile, cancellationToken);
        var targetHash = await CalculateSha3OfFile(target, cancellationToken);

        // Need to do a manual comparison
        bool equal = originalHash.Length == targetHash.Length;

        if (equal)
        {
            for (int i = 0; i < originalHash.Length; ++i)
            {
                if (originalHash[i] != targetHash[i])
                {
                    equal = false;
                    break;
                }
            }
        }

        if (equal)
        {
            // Same hash, so no need to copy
            return false;
        }

        File.Move(sourceFile, target, true);
        return true;
    }
}
