namespace ScriptsBase.Checks;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileTypes;
using Karambolo.PO;
using SharedBase.Utilities;
using Utilities;

/// <summary>
///   Checks that localization files are up to date. Structure and content by themselves are handled by
///   <see cref="PoFormatCheck"/> and <see cref="PoContentCheck"/>
/// </summary>
public class LocalizationCheckBase : CodeCheck
{
    public const string LOCALE_TEMP_SUFFIX = ".temp_check";

    protected bool issuesFound;

    private readonly POParser parser = CreateParser();

    public static POParser CreateParser()
    {
        return new POParser(new POParserSettings
        {
            PreserveHeadersOrder = true,
            StringDecodingOptions = new POStringDecodingOptions
            {
                KeepKeyStringsPlatformIndependent = true,
                KeepTranslationStringsPlatformIndependent = true,
            },
        });
    }

    public override async Task Run(CodeCheckRun runData, CancellationToken cancellationToken)
    {
        var poFiles = EnumerateAllPoFiles("./").Where(f => !runData.IsFileIgnored(f)).ToList();

        if (poFiles.Count < 2)
            throw new Exception("Less than two translation files found (.po)");

        issuesFound = false;

        try
        {
            CreateDuplicatesOfFiles(poFiles, cancellationToken);

            // Run a script to write updates to the temp files
            var startInfo = GetLocalizationUpdateCommand();

            var result = await ProcessRunHelpers.RunProcessAsync(startInfo, cancellationToken, true);

            if (result.ExitCode != 0)
            {
                runData.ReportError("Failed to run localization generation script " +
                    $"to check if current files are up to date: {result.FullOutput}");
                return;
            }

            // And now compare the temp files with the real ones
            foreach (var original in poFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var updated = TempCheckName(original);

                if (!ParsePoFile(original, runData, out var originalData))
                    break;

                if (!ParsePoFile(updated, runData, out var updatedData))
                    break;

                var originalHeaderOrder = originalData!.Headers.Keys.ToList();
                var updatedHeaderOrder = updatedData!.Headers.Keys.ToList();

                if (!originalHeaderOrder.SequenceEqual(updatedHeaderOrder))
                {
                    var originalOrder = string.Join(", ", originalHeaderOrder);
                    var updatedOrder = string.Join(", ", updatedHeaderOrder);

                    runData.OutputTextWithMutex($"Headers are in wrong order in {original}");

                    // TODO: should we say original order (like in the ruby version) or updated order is the right one?
                    runData.ReportError(
                        $"Header order should be: {updatedOrder}, but it is: {originalOrder}, in file {original}");

                    issuesFound = true;
                }

                if (originalData.Count != updatedData.Count)
                {
                    runData.ReportError($"{original} should have {updatedData.Count} translation keys in it, " +
                        $"but it has {originalData.Count}");

                    issuesFound = true;
                }

                cancellationToken.ThrowIfCancellationRequested();

                using var originalEnumerator = originalData.GetEnumerator();
                using var updatedEnumerator = updatedData.GetEnumerator();

                bool hasOriginalItem = true;
                bool hasUpdatedItem = true;

                while (true)
                {
                    if (hasOriginalItem)
                        hasOriginalItem = originalEnumerator.MoveNext() && originalEnumerator.Current != null;

                    if (hasUpdatedItem)
                        hasUpdatedItem = updatedEnumerator.MoveNext() && updatedEnumerator.Current != null;

                    // Quit once both files have ended
                    if (!hasOriginalItem && !hasUpdatedItem)
                        break;

                    if (hasOriginalItem && hasOriginalItem == hasUpdatedItem)
                    {
                        if (!originalEnumerator.Current!.Key.Equals(updatedEnumerator.Current!.Key))
                        {
                            runData.ReportError(
                                $"Original (committed) file has msgid: {originalEnumerator.Current.Key.Id}, " +
                                $"while it should have {updatedEnumerator.Current.Key.Id} at this point");
                        }
                        else
                        {
                            // Everything is fine
                            continue;
                        }
                    }
                    else if (!hasOriginalItem)
                    {
                        runData.ReportError("Original (committed) file is missing msgid: " +
                            $"{updatedEnumerator.Current!.Key.Id} as it has already ended");
                    }
                    else if (!hasUpdatedItem)
                    {
                        runData.ReportError(
                            $"Original (committed) file has msgid: {originalEnumerator.Current!.Key.Id}, " +
                            "while it should have ended already");
                    }

                    runData.OutputTextWithMutex(
                        $"Error was detected when comparing {original}, with freshly updated: {updated}");
                    issuesFound = true;
                    break;
                }
            }
        }
        finally
        {
            // Don't leave any temp files hanging around even if we are canceled
            DeleteDuplicatesOfFiles(poFiles);
        }
    }

    protected virtual ProcessStartInfo GetLocalizationUpdateCommand()
    {
        var startInfo = new ProcessStartInfo("dotnet");
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add("Scripts");
        startInfo.ArgumentList.Add("--");
        startInfo.ArgumentList.Add("localization");

        return startInfo;
    }

    private static IEnumerable<string> EnumerateAllPoFiles(string start)
    {
        return Directory.EnumerateFiles(start, "*.po", SearchOption.AllDirectories);
    }

    private static void CreateDuplicatesOfFiles(IEnumerable<string> files, CancellationToken cancellationToken)
    {
        foreach (var file in files)
        {
            var name = TempCheckName(file);

            File.Copy(file, name);

            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private static void DeleteDuplicatesOfFiles(IEnumerable<string> files, bool printErrors = true)
    {
        foreach (var file in files)
        {
            var name = TempCheckName(file);

            if (File.Exists(name))
            {
                try
                {
                    File.Delete(name);
                }
                catch (Exception e)
                {
                    if (printErrors)
                        ColourConsole.WriteErrorLine($"Failed to delete temporary file ({name}): {e}");
                }
            }
        }
    }

    private static string TempCheckName(string file)
    {
        return $"{file}{LOCALE_TEMP_SUFFIX}";
    }

    private bool ParsePoFile(string file, CodeCheckRun runData, out POCatalog? data)
    {
        using var reader = File.OpenText(file);

        var result = parser.Parse(reader);

        if (!result.Success)
        {
            data = null;
            var errors = string.Join(", ", result.Diagnostics.Select(d => d.ToString()));
            runData.ReportError($"PO parsing failed ({file}): {errors}");

            return false;
        }

        data = result.Catalog;
        return true;
    }
}
