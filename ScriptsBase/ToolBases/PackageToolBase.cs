namespace ScriptsBase.ToolBases;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Models;
using SharedBase.Utilities;
using Utilities;

/// <summary>
///   Base class for handling project packaging
/// </summary>
/// <typeparam name="T">The type of the options class</typeparam>
public abstract class PackageToolBase<T>
    where T : PackageOptionsBase
{
    protected readonly T options;

    private readonly List<string> reprintMessages = new();

    protected PackageToolBase(T options)
    {
        this.options = options;
    }

    protected abstract IReadOnlyCollection<PackagePlatform> ValidPlatforms { get; }

    /// <summary>
    ///   Platforms to select if nothing is selected by the user explicitly
    /// </summary>
    protected abstract IEnumerable<PackagePlatform> DefaultPlatforms { get; }

    protected CompressionType CompressionType { get; set; } = CompressionType.P7Zip;

    protected abstract IEnumerable<string> SourceFilesToPackage { get; }

    protected string CompressedSourceName => $"source{CompressionType.CompressedExtension()}";

    protected string CompressedSourceLocation => Path.Join(options.OutputFolder, CompressedSourceName);

    public async Task<bool> Run(CancellationToken cancellationToken)
    {
        if (options.Platforms.Count < 1)
        {
            ColourConsole.WriteNormalLine("Using default platforms for packaging");
            options.Platforms = DefaultPlatforms.ToList();
        }

        if (options.Platforms.Count < 1)
        {
            ColourConsole.WriteErrorLine("No platforms to export for selected");
            return false;
        }

        if (options.Retries < 0)
        {
            ColourConsole.WriteErrorLine("Retries needs to be a non-negative number");
            return false;
        }

        Directory.CreateDirectory(options.OutputFolder);

        if (!await OnBeforeStartExport(cancellationToken))
        {
            ColourConsole.WriteErrorLine("Failed to run before export operation");
            return false;
        }

        ColourConsole.WriteInfoLine($"Starting package for platforms: {string.Join(", ", options.Platforms)}");

        if (options.SourceCode == true)
        {
            ColourConsole.WriteNormalLine("Package includes source code");
            if (!await CompressSourceCode(cancellationToken))
            {
                ColourConsole.WriteErrorLine("Failed to compress source code");
                return false;
            }
        }

        foreach (var platform in options.Platforms)
        {
            ColourConsole.WriteInfoLine($"Starting package for: {platform}");

            try
            {
                if (!await PackageForPlatform(cancellationToken, platform))
                    return false;
            }
            catch (Exception e)
            {
                ColourConsole.WriteErrorLine($"Error while packaging: {e}");
                return false;
            }
        }

        if (!await OnAfterExport(cancellationToken))
        {
            ColourConsole.WriteErrorLine("After package operations failed");
            return false;
        }

        PrintReprints();
        ColourConsole.WriteSuccessLine("Packaging succeeded");
        return true;
    }

    /// <summary>
    ///   Messages that will be printed again after running everything
    /// </summary>
    /// <param name="message">The message to print, this will be checked to make sure it isn't a duplicate</param>
    public void AddReprintMessage(string message)
    {
        if (reprintMessages.Contains(message))
            return;

        reprintMessages.Add(message);
    }

    protected abstract string GetFolderNameForExport(PackagePlatform platform);

    protected virtual string GetCompressedExtensionForPlatform(PackagePlatform platform)
    {
        return CompressionType.CompressedExtension();
    }

    protected abstract Task<bool> Export(PackagePlatform platform, string folder, CancellationToken cancellationToken);

    /// <summary>
    ///   The files that will be copied to the packaged folders
    /// </summary>
    protected abstract IEnumerable<FileToPackage> GetFilesToPackage();

    protected virtual Task<bool> CopyExtraFiles(PackagePlatform platform, string folder,
        CancellationToken cancellationToken)
    {
        foreach (var fileToPackage in GetFilesToPackage().Where(f => f.IsForPlatform(platform)))
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromResult(false);

            var target = Path.Join(folder, fileToPackage.PackagePathAndName);

            var baseFolder = Path.GetDirectoryName(target) ??
                throw new Exception("Failed to get base folder from copy target");

            try
            {
                Directory.CreateDirectory(baseFolder);

                ColourConsole.WriteDebugLine($"Copying {fileToPackage.OriginalFile} to {target}");

                File.Copy(fileToPackage.OriginalFile, target, true);
            }
            catch (Exception e)
            {
                ColourConsole.WriteErrorLine($"Failed to copy {fileToPackage.OriginalFile} to {target}: {e}");
                return Task.FromResult(false);
            }
        }

        if (options.SourceCode == true)
        {
            ColourConsole.WriteDebugLine($"Copying source zip to {folder}");
            CopyHelpers.CopyToFolder(CompressedSourceLocation, folder);
        }
        else
        {
            ColourConsole.WriteNormalLine("Package doesn't include source code");
        }

        return Task.FromResult(true);
    }

    protected virtual async Task<bool> CompressSourceCode(CancellationToken cancellationToken)
    {
        ColourConsole.WriteNormalLine("Compressing source code...");
        try
        {
            // TODO: check that the ignores work on Windows
            var task = Compression.CompressMultipleItems("./", SourceFilesToPackage, CompressedSourceLocation,
                CompressionType, new List<string> { new("bin/"), new("obj/"), new(".*") }, cancellationToken,
                ColourConsole.DebugPrintingEnabled);
            await task.WaitAsync(cancellationToken);
        }
        catch (Exception e)
        {
            ColourConsole.WriteErrorLine($"Source compression failed: {e}");
            return false;
        }

        ColourConsole.WriteNormalLine("Source code prepared");

        return true;
    }

    protected virtual async Task<bool> Compress(PackagePlatform platform, string folder, string archiveFile,
        CancellationToken cancellationToken)
    {
        ColourConsole.WriteInfoLine($"Compressing {archiveFile}");
        try
        {
            var task = Compression.CompressFolder(Path.GetDirectoryName(folder) ?? string.Empty,
                Path.GetFileName(folder), archiveFile, CompressionType, cancellationToken,
                ColourConsole.DebugPrintingEnabled);
            await task.WaitAsync(cancellationToken);
        }
        catch (Exception e)
        {
            ColourConsole.WriteErrorLine($"Compression failed: {e}");
            return false;
        }

        return true;
    }

    protected virtual Task<bool> OnBeforeStartExport(CancellationToken cancellationToken)
    {
        return Task.FromResult(true);
    }

    protected virtual Task<bool> OnPostProcessExportedFolder(PackagePlatform platform, string folder,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(true);
    }

    protected virtual Task<bool> PrepareToExport(PackagePlatform platform, CancellationToken cancellationToken)
    {
        return Task.FromResult(true);
    }

    protected virtual Task<bool> OnPostFolderHandled(PackagePlatform platform, string folderOrArchive,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(true);
    }

    protected virtual Task<bool> OnAfterExport(CancellationToken cancellationToken)
    {
        return Task.FromResult(true);
    }

    private async Task<bool> PackageForPlatform(CancellationToken cancellationToken, PackagePlatform platform)
    {
        if (!await PrepareToExport(platform, cancellationToken))
        {
            ColourConsole.WriteErrorLine($"Failed to prepare to export to platform: {platform}");
            return false;
        }

        var folder = Path.Join(options.OutputFolder, GetFolderNameForExport(platform));

        Directory.CreateDirectory(folder);

        bool succeeded = false;

        for (int i = 0; i < options.Retries + 1; ++i)
        {
            if (!await Export(platform, folder, cancellationToken) || !Directory.Exists(folder))
            {
                if (i < options.Retries)
                {
                    ColourConsole.WriteNormalLine($"Failed to export {platform} to {folder}, will retry");

                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                }

                continue;
            }

            succeeded = true;
            break;
        }

        if (!succeeded)
        {
            ColourConsole.WriteErrorLine($"Export for platform {platform} failed too many times");
            return false;
        }

        if (!await CopyExtraFiles(platform, folder, cancellationToken))
        {
            ColourConsole.WriteErrorLine($"Failed to copy extra files to {folder}");
            return false;
        }

        if (!await OnPostProcessExportedFolder(platform, folder, cancellationToken))
        {
            ColourConsole.WriteErrorLine($"Failed to post process folder: {folder}");
            return false;
        }

        string folderOrArchive;

        if (options.Compress)
        {
            var zipFile = Path.Join(options.OutputFolder,
                $"{GetFolderNameForExport(platform)}{GetCompressedExtensionForPlatform(platform)}");

            if (options.CleanZips && File.Exists(zipFile))
                File.Delete(zipFile);

            if (!await Compress(platform, folder, zipFile, cancellationToken) || !File.Exists(zipFile))
            {
                ColourConsole.WriteErrorLine($"Failed to compress {platform} to {zipFile}");
                return false;
            }

            string hash =
                FileUtilities.HashToHex(await FileUtilities.CalculateSha3OfFile(zipFile, cancellationToken));

            var message1 = $"Created {platform} archive: {zipFile}";
            var message2 = $"SHA3: {hash}";

            AddReprintMessage(string.Empty);
            AddReprintMessage(message1);
            AddReprintMessage(message2);

            ColourConsole.WriteSuccessLine(message1);
            ColourConsole.WriteNormalLine(message2);

            folderOrArchive = zipFile;
        }
        else
        {
            ColourConsole.WriteNormalLine("Skipping compressing");

            var message1 = $"Created {platform} at {folder}";

            AddReprintMessage(string.Empty);
            AddReprintMessage(message1);

            ColourConsole.WriteSuccessLine(message1);

            folderOrArchive = folder;
        }

        if (!await OnPostFolderHandled(platform, folderOrArchive, cancellationToken))
        {
            ColourConsole.WriteErrorLine(
                $"Failed to run post processing on created folder/archive for platform: {platform}");
            return false;
        }

        ColourConsole.WriteSuccessLine($"{platform} package succeeded");
        ColourConsole.WriteNormalLine(string.Empty);
        return true;
    }

    private void PrintReprints()
    {
        if (reprintMessages.Count < 1)
            return;

        ColourConsole.WriteInfoLine("Reprint messages:");

        foreach (var message in reprintMessages)
        {
            ColourConsole.WriteNormalLine(message);
        }
    }
}
