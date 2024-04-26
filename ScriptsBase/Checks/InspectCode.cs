namespace ScriptsBase.Checks;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Sarif;
using SharedBase.Utilities;

public class InspectCode : JetBrainsCheck
{
    public const string InspectResultFile = "inspect_results.json";

    private string? lastLoadedFileForReporting;
    private string lastLoadedFileReportingData = string.Empty;

    private bool showFullFilePathsForErrors = true;

    /// <summary>
    ///   Regex version of <see cref="InspectCodeIgnoredFilesWildcards"/> these must be kept up to date
    /// </summary>
    public static IEnumerable<Regex> InspectCodeIgnoredFiles { get; } = new List<Regex>
    {
        new(@".*\.min\.css"),
        new(@".*\.dll"),
    };

    /// <summary>
    ///   Wildcard version of <see cref="InspectCodeIgnoredFiles"/>
    /// </summary>
    public static IEnumerable<string> InspectCodeIgnoredFilesWildcards { get; } = new List<string>
    {
        new("*.min.css"),
        new("*.dll"),
    };

    public void DisableFullPathPrinting()
    {
        showFullFilePathsForErrors = false;
    }

    protected override async Task RunJetBrainsTool(CodeCheckRun runData, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("dotnet");
        startInfo.ArgumentList.Add("tool");
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("jb");
        startInfo.ArgumentList.Add("inspectcode");
        startInfo.ArgumentList.Add(runData.SolutionFile!);
        startInfo.ArgumentList.Add("-f=sarif");
        startInfo.ArgumentList.Add($"-o={InspectResultFile}");
        startInfo.ArgumentList.Add("--build");
        startInfo.ArgumentList.Add($"--caches-home={JET_BRAINS_CACHE}");

        AddJetbrainsToolRunIncludes(runData, startInfo);

        // TODO: add files not specified to be checked in CodeCheckRun if any files are specified as not checked
        // if that's a good idea
        AddJetbrainsToolRunExcludes(
            InspectCodeIgnoredFilesWildcards.Concat(runData.ExtraIgnoredJetbrainsInspectWildcards), startInfo);

        var result = await ProcessRunHelpers.RunProcessAsync(startInfo, cancellationToken, JET_BRAINS_CAPTURE_OUTPUT);

        if (result.ExitCode != 0)
        {
            ReportRunFailure(result, "inspectcode", runData);
            return;
        }

        if (!File.Exists(InspectResultFile))
        {
            runData.ReportError("JetBrains inspectcode didn't create expected results file");
            return;
        }

        bool issuesFound = false;

        var log = SarifLog.Load(InspectResultFile);

        foreach (var sarifResult in log.Results())
        {
            if (sarifResult.Level < FailureLevel.Warning)
                continue;

            var rule = sarifResult.GetRule();
            var text = sarifResult.GetMessageText(rule);

            if (IsAlwaysIgnoredJetBrainsIssue(sarifResult.RuleId, text))
                continue;

            bool locationFound = false;

            foreach (var location in sarifResult.Locations)
            {
                if (location.PhysicalLocation == null)
                    continue;

                if (!issuesFound)
                {
                    runData.ReportError("Code inspection detected issues:");
                    issuesFound = true;
                    locationFound = true;
                }

                if (!location.PhysicalLocation.ArtifactLocation.TryReconstructAbsoluteUri(
                        sarifResult.Run.OriginalUriBaseIds, out var resolvedUri))
                {
                    runData.OutputWarningWithMutex(
                        $"Failed to resolve error absolute URI: {location.PhysicalLocation.ArtifactLocation.Uri}");
                    resolvedUri = location.PhysicalLocation.ArtifactLocation.Uri;
                }

                var file = resolvedUri.ToString();
                file = RemovePotentialFilePrefix(file);

                string reportFile;

                if (showFullFilePathsForErrors)
                {
                    reportFile = file;
                }
                else
                {
                    reportFile = RemovePotentialFilePrefix(location.PhysicalLocation.ArtifactLocation.Uri.ToString());
                }

                runData.OutputErrorWithMutex(
                    $"{reportFile}:{location.PhysicalLocation.Region.StartLine} {text} type: {sarifResult.RuleId}");

                // TODO: determine if the offset counts "\r\n" as one or two characters
                var offsetStart = location.PhysicalLocation.Region.CharOffset;
                var length = location.PhysicalLocation.Region.CharLength;

                PrepareFileForReporting(file);

                if (length < 1 || offsetStart < 0 || lastLoadedFileReportingData.Length <= offsetStart + length)
                {
                    runData.OutputWarningWithMutex(
                        $"Offset ({offsetStart}) specified in previous error could not be read in the file");
                }
                else
                {
                    // TODO: could probably display the entire line with a second line underline highlighting the error
                    //  to make the context clearer
                    var code = lastLoadedFileReportingData.Substring(offsetStart, length);

                    runData.OutputTextWithMutex(
                        $"Offending code (offset {offsetStart}) for previous message: '{code}'");
                }
            }

            if (!locationFound)
            {
                if (!issuesFound)
                {
                    runData.ReportError("Code inspection detected issues:");
                    issuesFound = true;
                }

                runData.OutputErrorWithMutex($"Problem with no physical location detected: {text}");
                runData.OutputTextWithMutex(sarifResult.ToString() ?? "SARIF to string failed");
            }

            runData.OutputTextWithMutex(rule.FullDescription.Text);

            try
            {
                if (rule.HelpUri != null)
                {
                    runData.OutputTextWithMutex(rule.HelpUri.ToString());
                }
            }
            catch (Exception e)
            {
                runData.OutputWarningWithMutex("Trying to read help URI resulted in an exception: " + e);
            }

            if (rule.Help != null)
            {
                runData.OutputTextWithMutex(rule.Help.Text);
            }
        }

        if (issuesFound)
        {
            runData.OutputErrorWithMutex("Code inspection detected issues, see inspect_results.xml");
        }
        else
        {
            runData.OutputTextWithMutex("inspectcode didn't detect any problems");
        }

        ClearFileLoadedForReporting();
    }

    private static string RemovePotentialFilePrefix(string file)
    {
        if (file.StartsWith("file://"))
            return file.Substring("file://".Length);

        return file;
    }

    /// <summary>
    ///   Returns true for known problematic checks that JetBrains reports that cannot be fixed
    /// </summary>
    /// <param name="ruleId">The type of problem to check</param>
    /// <param name="message">The message of the problem</param>
    /// <returns>True if the error should be always ignored</returns>
    private bool IsAlwaysIgnoredJetBrainsIssue(string ruleId, string message)
    {
        // TODO: are new ignores needed?
        _ = ruleId;
        _ = message;

        return false;
    }

    private void PrepareFileForReporting(string path)
    {
        if (lastLoadedFileForReporting == path)
            return;

        lastLoadedFileForReporting = path;

        lastLoadedFileReportingData = File.ReadAllText(path, Encoding.UTF8);
    }

    private void ClearFileLoadedForReporting()
    {
        lastLoadedFileForReporting = null;

        lastLoadedFileReportingData = string.Empty;
    }
}
