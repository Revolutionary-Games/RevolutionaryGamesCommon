namespace ScriptsBase.Utilities;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Models;
using SharedBase.Utilities;

public static class Compression
{
    public static string Get7Zip(bool log = true)
    {
        string lookFor = "7za";

        if (OperatingSystem.IsWindows())
        {
            lookFor = "7za.exe";
        }

        var result = ExecutableFinder.Which(lookFor);

        if (result == null)
        {
            if (log)
            {
                ColourConsole.WriteErrorLine("7-zip is a needed tool, but it was not found in PATH. " +
                    "Please install p7zip package, or 7-zip (on Windows)");
            }

            throw new Exception("7-zip not found");
        }

        return result;
    }

    public static async Task GzipToTarget(string sourceFile, string targetFile, CancellationToken cancellationToken)
    {
        if (targetFile.EndsWith(".gz"))
            throw new ArgumentException("Target should end in .gz");

        await using var reader = File.OpenRead(sourceFile);

        await using var fileWriter = File.OpenWrite(targetFile);
        await using var gzWriter = new GZipStream(fileWriter, CompressionLevel.Optimal);

        var buffer = new byte[1024 * 16];

        Task? writeTask = null;

        while (true)
        {
            var read = await reader.ReadAsync(buffer, cancellationToken);

            if (writeTask != null)
                await writeTask;

            if (read == 0)
                break;

            writeTask = gzWriter.WriteAsync(buffer, 0, read, cancellationToken);
        }
    }

    public static Task CompressFolder(string baseFolder, string folder, string archiveFile,
        CompressionType compressionType, CancellationToken cancellationToken, bool measureTime = false)
    {
        if (compressionType != CompressionType.P7Zip)
            throw new NotImplementedException("unimplemented compression type");

        var startInfo = new ProcessStartInfo(Get7Zip())
        {
            CreateNoWindow = true,
            WorkingDirectory = baseFolder,
        };
        startInfo.ArgumentList.Add("a");
        startInfo.ArgumentList.Add("-mx=9");
        startInfo.ArgumentList.Add("-ms=on");
        startInfo.ArgumentList.Add(Path.GetFullPath(archiveFile));
        startInfo.ArgumentList.Add(folder);

        return RunCompressionTool(startInfo, measureTime, cancellationToken);
    }

    public static Task CompressMultipleItems(string baseFolder, IEnumerable<string> toCompress, string archiveFile,
        CompressionType compressionType, IReadOnlyCollection<string> ignorePatterns,
        CancellationToken cancellationToken, bool measureTime = false)
    {
        if (compressionType != CompressionType.P7Zip)
            throw new NotImplementedException("unimplemented compression type");

        var startInfo = new ProcessStartInfo(Get7Zip())
        {
            CreateNoWindow = true,
            WorkingDirectory = baseFolder,
        };
        startInfo.ArgumentList.Add("a");
        startInfo.ArgumentList.Add("-mx=9");
        startInfo.ArgumentList.Add("-ms=on");
        startInfo.ArgumentList.Add(Path.GetFullPath(archiveFile));

        foreach (var item in toCompress)
        {
            startInfo.ArgumentList.Add(item);
        }

        foreach (var ignore in ignorePatterns)
        {
            // TODO: do we need to convert "/" to "\" for Windows?
            startInfo.ArgumentList.Add($"-xr!{ignore}");
        }

        return RunCompressionTool(startInfo, measureTime, cancellationToken);
    }

    private static async Task RunCompressionTool(ProcessStartInfo startInfo, bool measure,
        CancellationToken cancellationToken)
    {
        var elapsed = Stopwatch.StartNew();

        var result = await ProcessRunHelpers.RunProcessAsync(startInfo, cancellationToken, true);

        if (result.ExitCode != 0)
        {
            throw new Exception($"Running 7-zip failed (exit: {result.ExitCode}): {result.FullOutput}");
        }

        if (measure)
            ColourConsole.WriteDebugLine($"Compressing took {elapsed.Elapsed}");
    }
}
