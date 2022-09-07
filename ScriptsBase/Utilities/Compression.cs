namespace ScriptsBase.Utilities;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Models;
using SharpCompress.Archives;
using SharpCompress.Archives.Tar;
using SharpCompress.Writers.Tar;

public static class Compression
{
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

    public static Task CompressFolder(string folder, string archiveFile, CompressionType compressionType,
        bool measureTime = false)
    {
        var task = new Task(() =>
        {
            using var archive = TarArchive.Create();
            AddFilesRecursivelyWithPrefix(archive, folder, new List<Regex>());

            SaveWithCompression(archive, compressionType, archiveFile, measureTime);
        });

        task.Start();

        return task;
    }

    public static Task CompressMultipleItems(IEnumerable<string> toCompress, string archiveFile,
        CompressionType compressionType, IReadOnlyCollection<Regex> ignore, bool measureTime = false)
    {
        var task = new Task(() =>
        {
            using var archive = TarArchive.Create();

            foreach (var item in toCompress)
            {
                if (Directory.Exists(item))
                {
                    AddFilesRecursivelyWithPrefix(archive, item, ignore);
                }
                else
                {
                    archive.AddEntry(item, item);
                }
            }

            SaveWithCompression(archive, compressionType, archiveFile, measureTime);
        });

        task.Start();

        return task;
    }

    private static void AddFilesRecursivelyWithPrefix(IWritableArchive archive, string folder,
        IReadOnlyCollection<Regex> ignore, string pattern = "*.*",
        bool ignoreHidden = true)
    {
        foreach (var subFolder in Directory.EnumerateDirectories(folder, pattern))
        {
            if (ignoreHidden && Path.GetFileName(subFolder).StartsWith("."))
                continue;

            if (ignore.Any(r => r.IsMatch(subFolder)))
                continue;

            AddFilesRecursivelyWithPrefix(archive, subFolder, ignore, pattern, ignoreHidden);
        }

        foreach (var file in Directory.EnumerateFiles(folder, pattern, SearchOption.TopDirectoryOnly))
        {
            if (ignoreHidden && Path.GetFileName(file).StartsWith("."))
                continue;

            if (ignore.Any(r => r.IsMatch(file)))
                continue;

            archive.AddEntry(file, file);
        }
    }

    private static void SaveWithCompression(IWritableArchive archive, CompressionType compressionType,
        string archiveFile, bool measure)
    {
        var elapsed = Stopwatch.StartNew();

        SharpCompress.Common.CompressionType tarCompression;

        switch (compressionType)
        {
            case CompressionType.TarLZip:
                tarCompression = SharpCompress.Common.CompressionType.LZip;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(compressionType), compressionType, null);
        }

        archive.SaveTo(archiveFile, new TarWriterOptions(tarCompression, true));

        if (measure)
            ColourConsole.WriteDebugLine($"Compressing {archiveFile} took {elapsed.Elapsed}");
    }
}
