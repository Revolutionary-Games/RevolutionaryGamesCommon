namespace ScriptsBase.ToolBases;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Models;
using SharedBase.Utilities;
using Translation;
using Utilities;

/// <summary>
///   Base class for handling updating localization files
/// </summary>
/// <typeparam name="T">The type of the options class</typeparam>
public abstract class LocalizationUpdateBase<T>
    where T : LocalizationOptionsBase
{
    protected readonly T options;

    private readonly IReadOnlyCollection<Regex> untranslatablePatterns = new[]
    {
        new Regex(@"^[+-.]*\d[\d.-]+$", RegexOptions.Compiled),
        new Regex(@"^\s+$", RegexOptions.Compiled),
    };

    private readonly Dictionary<string, string> alreadyFoundTools = new();

    protected LocalizationUpdateBase(T options)
    {
        this.options = options;
    }

    /// <summary>
    ///   The locales to process
    /// </summary>
    protected abstract IReadOnlyList<string> Locales { get; }

    protected abstract string LocaleFolder { get; }

    /// <summary>
    ///   Weblate disagrees with gettext tools regarding where to wrap, so we have to disable it
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     https://github.com/WeblateOrg/weblate/issues/6350
    ///     https://github.com/Revolutionary-Games/Thrive/issues/2679
    ///   </para>
    /// </remarks>
    protected virtual IEnumerable<string> LineWrapSettings => new[] { "--no-wrap" };

    protected virtual string TranslationTemplateFileName => $"messages{PotSuffix}";

    protected virtual string TranslationTemplateFile => Path.Join(LocaleFolder, TranslationTemplateFileName);

    protected string PotSuffix => string.IsNullOrEmpty(options.PotSuffix) ? ".pot" : options.PotSuffix;
    protected string PoSuffix => string.IsNullOrEmpty(options.PoSuffix) ? ".po" : options.PoSuffix;

    protected abstract IEnumerable<string> PathsToExtractFrom { get; }

    protected abstract string ProjectName { get; }
    protected abstract string ProjectOrganization { get; }
    protected virtual string GeneratedBy => "Thrive.Scripts";

    protected virtual string GeneratedByVersion =>
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown version";

    public async Task<bool> Run(CancellationToken cancellationToken)
    {
        if (!options.Quiet)
            ColourConsole.WriteInfoLine($"Extracting translations template {TranslationTemplateFileName}");

        if (!await RunTemplateUpdate(cancellationToken))
            return false;

        if (!File.Exists(TranslationTemplateFile))
        {
            ColourConsole.WriteErrorLine(
                $"Failed to create the expected translations template at: {TranslationTemplateFile}");
            return false;
        }

        if (!options.Quiet)
        {
            ColourConsole.WriteSuccessLine("Template updated");
            ColourConsole.WriteInfoLine($"Updating individual languages ({PoSuffix} files)");
        }

        foreach (var locale in Locales)
        {
            if (!await UpdateLocale(locale, cancellationToken))
            {
                ColourConsole.WriteErrorLine($"Failed to process locale: {locale}");
                return false;
            }
        }

        await PostProcessTranslations(cancellationToken);

        if (!options.Quiet)
            ColourConsole.WriteSuccessLine("Done updating locales");
        return true;
    }

    protected virtual string GetTranslationFileNameForLocale(string locale)
    {
        return $"{locale}{PoSuffix}";
    }

    protected virtual Task<bool> UpdateLocale(string locale, CancellationToken cancellationToken)
    {
        var target = Path.Join(LocaleFolder, GetTranslationFileNameForLocale(locale));

        if (File.Exists(target))
        {
            if (!options.Quiet)
                ColourConsole.WriteNormalLine($"Updating locale {locale}");

            return RunTranslationUpdate(locale, target, cancellationToken);
        }

        // TODO: this is untested
        if (!options.Quiet)
            ColourConsole.WriteNormalLine($"Creating locale {locale}");
        return RunTranslationCreate(locale, target, cancellationToken);
    }

    protected abstract Task<bool> RunTranslationCreate(string locale, string targetFile,
        CancellationToken cancellationToken);

    protected abstract Task<bool> RunTranslationUpdate(string locale, string targetFile,
        CancellationToken cancellationToken);

    protected abstract List<TranslationExtractorBase> GetTranslationExtractors();

    protected virtual Task<bool> PostProcessTranslations(CancellationToken cancellationToken)
    {
        return Task.FromResult(true);
    }

    protected string? FindTranslationTool(string name)
    {
        if (alreadyFoundTools.TryGetValue(name, out var alreadyFound))
            return alreadyFound;

        var path = ExecutableFinder.Which(name);

        if (path == null)
        {
            ColourConsole.WriteErrorLine(
                $"Failed to find translation tool '{name}'. Please install it and make sure it is in PATH.");
        }
        else
        {
            alreadyFoundTools[name] = path;
        }

        return path;
    }

    protected void AddLineWrapSettings(ProcessStartInfo startInfo)
    {
        foreach (var setting in LineWrapSettings)
        {
            startInfo.ArgumentList.Add(setting);
        }
    }

    protected async Task<bool> RunTranslationTool(ProcessStartInfo startInfo, CancellationToken cancellationToken)
    {
        var result = await ProcessRunHelpers.RunProcessAsync(startInfo, cancellationToken, true);

        if (result.ExitCode != 0)
        {
            ColourConsole.WriteErrorLine(
                $"Failed to run a localization tool (exit: {result.ExitCode}): {result.FullOutput}");
            return false;
        }

        return true;
    }

    private async Task<bool> RunTemplateUpdate(CancellationToken cancellationToken)
    {
        var extractors = GetTranslationExtractors();

        var rawTranslations = new List<ExtractedTranslation>();

        try
        {
            foreach (var basePath in PathsToExtractFrom)
            {
                foreach (var file in Directory.EnumerateFiles(basePath, "*.*", SearchOption.AllDirectories))
                {
                    // TODO: an exclude list if some sub folders shouldn't be processed?

                    string preparedPath;
                    if (OperatingSystem.IsWindows())
                    {
                        preparedPath = file.Replace('\\', '/');
                    }
                    else
                    {
                        preparedPath = file;
                    }

                    foreach (var extractor in extractors)
                    {
                        if (extractor.HandlesFile(preparedPath))
                        {
                            await foreach (var extracted in extractor.Handle(preparedPath, cancellationToken))
                            {
                                rawTranslations.Add(extracted);
                            }
                        }
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
            }
        }
        catch (Exception e)
        {
            ColourConsole.WriteErrorLine($"Failed to run text extraction: {e}");
            return false;
        }

        if (rawTranslations.Count < 1)
        {
            ColourConsole.WriteErrorLine("No translations to extract found");
            return false;
        }

        // Group all keys
        var groups = rawTranslations.GroupBy(t => t.TranslationKey);

        // Filtering for things we don't want to translate
        groups = groups.Where(g => IsFineToTranslate(g.Key));

        // TODO: do we want to alphabetically sort the translation keys or not? That could have some benefits.

        await WritePotFile(groups, cancellationToken);

        return true;
    }

    private bool IsFineToTranslate(string text)
    {
        return untranslatablePatterns.All(p => !p.IsMatch(text));
    }

    private async Task WritePotFile(IEnumerable<IGrouping<string, ExtractedTranslation>> groups,
        CancellationToken cancellationToken)
    {
        await using var file = File.Open(TranslationTemplateFile, FileMode.Create, FileAccess.Write);
        await using var writer = new StreamWriter(file, new UTF8Encoding(false));

        var now = DateTime.Now;
        var year = now.Year;

        // This is to get the syntax exactly right (the same as the previous generator)
        var trailingPart = now.ToString("zzz", CultureInfo.InvariantCulture).Replace(":", string.Empty);
        var date = now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + trailingPart;

        // TODO: test that this works on windows (line endings)

        var builder = new StringBuilder();

        // Write the file header
        builder.Append($"# Translations template for {ProjectName}.\n");
        builder.Append($"# Copyright (C) {year} {ProjectOrganization}\n");
        builder.Append($"# This file is distributed under the same license as the {ProjectName} project.\n");
        builder.Append($"# FIRST AUTHOR <EMAIL@ADDRESS>, {year}.\n");
        builder.Append("#\n");
        builder.Append("#, fuzzy\n");
        builder.Append("msgid \"\"\n");
        builder.Append("msgstr \"\"\n");

        // Intentionally the Thrive version is not put here as that would be one more thing changing quite often
        // in this file
        builder.Append($"\"Project-Id-Version: {ProjectName} VERSION\\n\"\n");
        builder.Append("\"Report-Msgid-Bugs-To: EMAIL@ADDRESS\\n\"\n");
        builder.Append($"\"POT-Creation-Date: {date}\\n\"\n");
        builder.Append("\"PO-Revision-Date: YEAR-MO-DA HO:MI+ZONE\\n\"\n");
        builder.Append("\"Last-Translator: FULL NAME <EMAIL@ADDRESS>\\n\"\n");
        builder.Append("\"Language-Team: LANGUAGE <LL@li.org>\\n\"\n");
        builder.Append("\"MIME-Version: 1.0\\n\"\n");
        builder.Append("\"Content-Type: text/plain; charset=utf-8\\n\"\n");
        builder.Append("\"Content-Transfer-Encoding: 8bit\\n\"\n");
        builder.Append($"\"Generated-By: {GeneratedBy} {GeneratedByVersion}\\n\"\n");
        builder.Append('\n');

        await writer.WriteAsync(builder, cancellationToken);

        // Need to convert the source paths to be relative to the template file
        // We do that simply by counting how deep the locales folder is, this way we know then how to get from there
        // to the root of the repo and then all translation references are related to that path
        string pathPrefix = string.Empty;
        int depth = LocaleFolder.Count(c => c == '/') + 1;

        for (int i = 0; i < depth; ++i)
            pathPrefix += "../";

        var alreadyUsedSourceLocations = new HashSet<string>();

        foreach (var group in groups)
        {
            builder.Clear();
            alreadyUsedSourceLocations.Clear();

            // Where this text is referenced
            foreach (var groupData in group)
            {
                // Only add unique locations
                if (alreadyUsedSourceLocations.Add(groupData.SourceLocation))
                {
                    builder.Append($"#: {pathPrefix}{groupData.SourceLocation}");
                    builder.Append('\n');
                }
            }

            // The text itself
            builder.Append($"msgid \"{group.Key}\"");
            builder.Append('\n');

            // Empty content for the template file
            builder.Append("msgstr \"\"");
            builder.Append('\n');

            builder.Append('\n');

            await writer.WriteAsync(builder, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Extra blank line at the end of the file
        await writer.WriteAsync('\n');
    }
}
