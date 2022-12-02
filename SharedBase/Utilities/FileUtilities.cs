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
}
