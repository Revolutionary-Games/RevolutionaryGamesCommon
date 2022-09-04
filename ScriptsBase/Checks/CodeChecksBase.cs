namespace ScriptsBase.Checks;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Models;
using SharedBase.Utilities;
using Utilities;

/// <summary>
///   Base class for handling running configured <see cref="CodeCheck"/>s
/// </summary>
/// <typeparam name="T">The type of the options class</typeparam>
public abstract class CodeChecksBase<T>
    where T : CheckOptionsBase
{
    public const int MAX_RUNTIME_MINUTES = 45;

    protected readonly T options;

    protected CodeChecksBase(T options)
    {
        this.options = options;
        RunData = new CodeCheckRun();
    }

    public CodeCheckRun RunData { get; }

    /// <summary>
    ///   The valid checks that exist. Note this should not create a new dictionary each time this is accessed
    /// </summary>
    protected abstract Dictionary<string, CodeCheck> ValidChecks { get; }

    protected abstract string? MainSolutionFile { get; }

    /// <summary>
    ///   Extra warnings to ignore in inspections that I couldn't figure out how to suppress normally
    /// </summary>
    protected virtual IEnumerable<string> ForceIgnoredJetbrainsInspections => new string[] { };

    /// <summary>
    ///   Sends extra wildcards on top of <see cref="InspectCode.InspectCodeIgnoredFilesWildcards"/> to be ignored
    ///   in code inspection
    /// </summary>
    protected virtual IEnumerable<string> ExtraIgnoredJetbrainsInspectWildcards => new string[] { };

    protected virtual IEnumerable<string> DefaultChecks => ValidChecks.Keys;

    /// <summary>
    ///   Project specific file paths to always ignore
    /// </summary>
    protected List<Regex> FilePathsToAlwaysIgnore { get; set; } = new();

    /// <summary>
    ///   Default set of editor and temp files to ignore
    /// </summary>
    protected virtual IEnumerable<Regex> DefaultIgnoredFilePaths { get; } = new List<Regex>
    {
        new(@"\.vs/"),
        new(@"\.idea/"),
        new(@"\.mono/"),
        new(@"\.import/"),
        new(@"\.DotSettings$"),
        new(@"/?tmp/"),
        new(@"/?RubySetupSystem/"),
        new(@"/?bin/"),
        new(@"/?obj/"),
        new(@"\.out$"),
        new(@"~$"),
        new(@"\.bak$"),
        new(@"\.git/"),
        new(@"/?builds/", RegexOptions.IgnoreCase),
        new(@"/?dist/", RegexOptions.IgnoreCase),
        new(LocalizationCheckBase.LOCALE_TEMP_SUFFIX + "$"),
        new(JetBrainsCheck.JET_BRAINS_CACHE + "/"),
    }.Concat(InspectCode.InspectCodeIgnoredFiles).ToList();

    /// <summary>
    ///   Runs the code checks with the specified options given to the constructor
    /// </summary>
    /// <returns>0 on success, 2 on error. 1 on configuration error</returns>
    public async Task<int> Run()
    {
        var tokenSource = new CancellationTokenSource();
        tokenSource.CancelAfter(TimeSpan.FromMinutes(MAX_RUNTIME_MINUTES));

        Console.CancelKeyPress += (_, args) =>
        {
            // Only prevent CTRL-C working once
            if (tokenSource.IsCancellationRequested)
                return;

            ColourConsole.WriteNormalLine("Cancel request detected");
            RunData.ReportCancel();
            tokenSource.Cancel();
            args.Cancel = true;
        };

        ColourConsole.WriteDebugLine("Determining which checks to run...");

        var selectedChecks = new List<CodeCheck>();

        var checkNames = options.Checks.Count > 0 ? options.Checks : DefaultChecks.ToList();

        var setupResult = SetupCheckObjectsForRun(checkNames, selectedChecks);

        if (setupResult != 0)
            return setupResult;

        if (options.PreCommitMode)
        {
            await SetupPreCommitMode(tokenSource.Token);
        }

        SetupRunDataObject();

        try
        {
            if (tokenSource.Token.IsCancellationRequested)
                return 1;

            ColourConsole.WriteInfoLine(
                $"Starting formatting checks with the following checks: {string.Join(' ', checkNames)}");

            await RunActualChecks(selectedChecks, tokenSource);
        }
        finally
        {
            if (options.PreCommitMode)
                TeardownPreCommitMode();
        }

        if (RunData.Errors)
        {
            Console.WriteLine("Format issues have been detected");
            return 2;
        }

        ColourConsole.WriteSuccessLine("No code format issues found");
        return 0;
    }

    private async Task RunActualChecks(List<CodeCheck> selectedChecks, CancellationTokenSource tokenSource)
    {
        var cancellationToken = tokenSource.Token;
        var pendingTasks = new List<Task>();

        if (options.Parallel != true)
        {
            ColourConsole.WriteDebugLine(
                "Will wait for each check to finish before starting next one (parallel option is not enabled)");
        }

        try
        {
            foreach (var codeCheck in selectedChecks)
            {
                var task = codeCheck.Run(RunData, cancellationToken);

                if (options.Parallel != true)
                {
                    await task;

                    if (RunData.Errors)
                        break;
                }
                else
                {
                    pendingTasks.Add(task);
                }

                if (cancellationToken.IsCancellationRequested)
                    break;
            }

            foreach (var pendingTask in pendingTasks)
            {
                await pendingTask;

                if (RunData.Errors)
                {
                    tokenSource.Cancel();
                }

                if (cancellationToken.IsCancellationRequested)
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("(further) checks were canceled");
        }
        catch (Exception e)
        {
            Console.WriteLine("Exception occurred when running code checks: {0}", e);
            RunData.ReportError("Check running caused an exception");
        }
    }

    private async Task SetupPreCommitMode(CancellationToken cancellationToken)
    {
        RunData.OutputInfoWithMutex("Preparing to run in pre-commit mode");

        var diff = await GitRunHelpers.DiffNameOnly("./", true, cancellationToken);

        await File.WriteAllTextAsync(OnlyChangedFileDetector.ONLY_FILE_LIST, diff, Encoding.UTF8, cancellationToken);
    }

    private void TeardownPreCommitMode()
    {
        try
        {
            File.Delete(OnlyChangedFileDetector.ONLY_FILE_LIST);
        }
        catch (Exception e)
        {
            RunData.OutputErrorWithMutex($"Failed to delete changed files list: {e}");
        }
    }

    private void SetupRunDataObject()
    {
        ColourConsole.WriteDebugLine("Setting up run data");
        RunData.SetIgnoredFiles(DefaultIgnoredFilePaths.Concat(FilePathsToAlwaysIgnore));
        RunData.SetSpecificSetOfFilesToCheck(OnlyChangedFileDetector.DetectOnlySomeFilesConfiguredForChecking()
            ?.ToList());
        RunData.InstallDotnetTools = options.RestoreTools;
        RunData.SolutionFile = MainSolutionFile;
        RunData.ForceIgnoredJetbrainsInspections = ForceIgnoredJetbrainsInspections.ToList();
        RunData.ExtraIgnoredJetbrainsInspectWildcards = ExtraIgnoredJetbrainsInspectWildcards.ToList();
    }

    private int SetupCheckObjectsForRun(IEnumerable<string> checkNames, List<CodeCheck> selectedChecks)
    {
        var validNames = string.Join(", ", ValidChecks.Keys);

        foreach (var checkName in checkNames)
        {
            if (!ValidChecks.TryGetValue(checkName, out var check))
            {
                RunData.OutputErrorWithMutex($"Unknown check name: {checkName}, valid names: {validNames}");
                {
                    return 1;
                }
            }

            selectedChecks.Add(check);
        }

        if (selectedChecks.Count < 1)
        {
            RunData.OutputErrorWithMutex($"No checks selected. Available checks: {validNames}");
            {
                return 1;
            }
        }

        return 0;
    }
}
