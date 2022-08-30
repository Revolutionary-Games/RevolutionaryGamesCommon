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
using System.Xml;
using SharedBase.Utilities;

public class InspectCode : JetBrainsCheck
{
    private const string InspectResultFile = "inspect_results.xml";

    private string? lastLoadedFileForReporting;
    private string lastLoadedFileReportingData = string.Empty;

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
        new(@"*.min.css"),
        new(@"*.dll"),
    };

    protected override async Task RunJetBrainsTool(CodeCheckRun runData, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("dotnet");
        startInfo.ArgumentList.Add("tool");
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("jb");
        startInfo.ArgumentList.Add("inspectcode");
        startInfo.ArgumentList.Add(runData.SolutionFile!);
        startInfo.ArgumentList.Add($"-o={InspectResultFile}");
        startInfo.ArgumentList.Add("--build");
        startInfo.ArgumentList.Add($"--caches-home={JET_BRAINS_CACHE}");

        AddJetbrainsToolRunIncludes(runData, startInfo);

        // TODO: add files not specified to be checked in CodeCheckRun if any files are specified as not checked
        // if that's a good idea
        var formattedExcludes = string.Join(';', InspectCodeIgnoredFilesWildcards);

        startInfo.ArgumentList.Add($"--exclude={formattedExcludes}");

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

        var doc = new XmlDocument();
        doc.Load(InspectResultFile);
        var issueTypeSeverities = new Dictionary<string, string>();

        foreach (XmlNode node in doc.SelectNodes("//IssueType") ?? throw new Exception("Issue types not found"))
        {
            issueTypeSeverities.Add(node.Attributes?["Id"]?.Value ?? throw new Exception("Issue type has no id"),
                node.Attributes?["Severity"]?.Value ?? throw new Exception("Issue type has no severity"));
        }

        foreach (XmlNode node in doc.SelectNodes("//Issue") ?? throw new Exception("Issues not found"))
        {
            var type = node.Attributes?["TypeId"]?.Value ?? throw new Exception("Issue node has no type id");
            var severity = issueTypeSeverities[type];

            if (severity == "SUGGESTION")
                continue;

            if (runData.ForceIgnoredJetbrainsInspections.Contains(type))
                continue;

            var file = node.Attributes?["File"]?.Value ?? throw new Exception("Issue node has no file");
            var line = node.Attributes?["Line"]?.Value ?? throw new Exception("Issue node has no line");
            var message = node.Attributes?["Message"]?.Value ?? throw new Exception("Issue node has no message");
            var offset = node.Attributes?["Offset"]?.Value;

            if (!issuesFound)
            {
                runData.ReportError("Code inspection detected issues:");
                issuesFound = true;
            }

            runData.OutputErrorWithMutex($"{file}:{line} {message} type: {type}");

            if (offset != null)
            {
                var values = offset.Split('-').Select(int.Parse).ToList();
                var start = values[0];
                var end = values[1];

                // TODO: determine if the offset counts "\r\n" as one or two characters

                PrepareFileForReporting(file.Replace('\\', '/'));

                if (lastLoadedFileReportingData.Length <= end)
                {
                    runData.OutputWarningWithMutex(
                        $"Offset ({offset}) specified in previous error could not be read in the file");
                }
                else
                {
                    // TODO: could probably display the entire line with a second line underline highlighting the error
                    //  to make the context clearer
                    var code = lastLoadedFileReportingData.Substring(start, end - start);

                    runData.OutputTextWithMutex($"Offending code (offset {offset}) for previous message: '{code}'");
                }
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
