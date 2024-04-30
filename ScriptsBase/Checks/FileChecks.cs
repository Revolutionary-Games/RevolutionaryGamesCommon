namespace ScriptsBase.Checks;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileTypes;
using SharedBase.Utilities;
using Utilities;

/// <summary>
///   Runs checks that are implemented in C# on present files in the repository
/// </summary>
public class FileChecks : CodeCheck
{
    private const bool PrintNameTwice = false;

    private const string StartFileEnumerateFolder = "./";

    /// <summary>
    ///   Used to warn the user when big files are accidentally being checked
    /// </summary>
    private const int ExpectedMebibyteMaxSize = 2;

    private static readonly bool NeedsToReplacePaths = Path.DirectorySeparatorChar != '/';

    /// <summary>
    ///   Known list if binary file extensions to prevent checks from running on them
    /// </summary>
    private static readonly IReadOnlyList<string> KnownBinaryFiles = new List<string>
    {
        ".obj",
        ".dll",
        ".a",
        ".so",
        ".bin",
        ".tar",
        ".tar.xz",
        ".tar.gz",
        ".7z",

        // Not really a binary file but we don't want to manually edit these
        ".svg",
    };

    private readonly List<FileCheck> enabledChecks = new();

    private readonly IReadOnlyList<string> binaryFileExtensions;

    /// <summary>
    ///   Initializes file checks with default settings
    /// </summary>
    public FileChecks() : this(true)
    {
    }

    /// <summary>
    ///   Sets up this check with much more control than the empty constructor
    /// </summary>
    /// <param name="useDefaults">If true default checks are added automatically</param>
    /// <param name="customChecks">A list of custom checks to prepend to the active checks</param>
    public FileChecks(bool useDefaults, params FileCheck[] customChecks)
    {
        enabledChecks.AddRange(customChecks);

        if (useDefaults)
        {
            enabledChecks.Add(new GitConflictMarkerCheck());
            enabledChecks.Add(new CSharpCheck());
            enabledChecks.Add(new TscnCheck());
            enabledChecks.Add(new JSONCheck());
            enabledChecks.Add(new ShaderCheck());
            enabledChecks.Add(new CsprojCheck());
            enabledChecks.Add(new EndsWithNewLineCheck(".csproj", ".shader"));
            enabledChecks.Add(new PoFormatCheck());
            enabledChecks.Add(new PoContentCheck());
        }

        // Detect what git LFS has marked as binary and add those to known binary files
        // TODO: check if we should somehow run this asynchronously
        binaryFileExtensions = GenerateKnownToBeIgnoredExtensions("./").Result;
    }

    /// <summary>
    ///   Generate a list of known bad file endings to not handle (binary files)
    /// </summary>
    /// <param name="gitFolder">Folder to run git in to get info from git</param>
    /// <returns>The list of known bad file endings</returns>
    public static async Task<List<string>> GenerateKnownToBeIgnoredExtensions(string gitFolder)
    {
        var binaryAttributes =
            await GitRunHelpers.ParseGitAttributeBinaryFiles(gitFolder, false, CancellationToken.None);

        // TODO: maybe we should also load .gitignore here?

        return KnownBinaryFiles.Concat(binaryAttributes).ToList();
    }

    /// <summary>
    ///   Enumerate non-ignored files recursively
    /// </summary>
    /// <param name="start">The folder to start</param>
    /// <param name="runData">Run data to check excludes from</param>
    /// <param name="binaryFileExtensions">Binary file extensions that are ignored</param>
    /// <returns>All found files</returns>
    public static IEnumerable<string> EnumerateFilesRecursively(string start, CodeCheckRun runData,
        IReadOnlyCollection<string> binaryFileExtensions)
    {
        foreach (var file in Directory.EnumerateFiles(start))
        {
            // Hopefully this isn't too bad performance for Windows people
            string handledFile;
            if (NeedsToReplacePaths)
            {
                handledFile = file.Replace(Path.DirectorySeparatorChar, '/');
            }
            else
            {
                handledFile = file;
            }

            if (handledFile.StartsWith(StartFileEnumerateFolder))
            {
                handledFile = handledFile.Substring(StartFileEnumerateFolder.Length);
            }

            if (runData.ProcessFile(handledFile))
            {
                // Skip handling any binary files as the handlers (even ones that work on *any* file type, don't really
                // want to run on them). If we ever get a binary type that needs checking we'll need some way to say
                // *really* any file and text files only for a check.
                if (binaryFileExtensions.Any(handledFile.EndsWith))
                    continue;

                yield return handledFile;
            }
        }

        foreach (var folder in Directory.EnumerateDirectories(start))
        {
            foreach (var recursiveCall in EnumerateFilesRecursively(folder, runData, binaryFileExtensions))
            {
                yield return recursiveCall;
            }
        }
    }

    public override async Task Run(CodeCheckRun runData, CancellationToken cancellationToken)
    {
        var files = EnumerateFilesRecursively(StartFileEnumerateFolder, runData, binaryFileExtensions);

        if (!await RunChecksOnFiles(files, runData, cancellationToken))
        {
            runData.ReportError("Code format issues detected");
        }
    }

    private async Task<bool> RunChecksOnFiles(IEnumerable<string> files, CodeCheckRun runData,
        CancellationToken cancellationToken)
    {
        bool success = true;

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (ColourConsole.DebugPrintingEnabled)
                runData.OutputTextWithMutex($"Handling: {file}");

            CheckAndWarnAboutFileSize(runData, file);

            try
            {
                if (!await Handle(file, runData))
                {
                    // Give a little bit of extra spacing between the files with errors
                    runData.OutputTextWithMutex(string.Empty);
                    success = false;

                    // Don't stop here as we want all file errors at once
                }
            }
            catch (Exception e)
            {
                runData.OutputTextWithMutex($"An exception occurred when handling a file ({file}): {e}");
                runData.ReportError($"Error: {e.Message}");
                return false;
            }
        }

        return success;
    }

    private void CheckAndWarnAboutFileSize(CodeCheckRun runData, string file)
    {
        var fileInfo = new FileInfo(file);

        if (fileInfo.Length > GlobalConstants.MEBIBYTE * ExpectedMebibyteMaxSize)
        {
            var mebibytes = Math.Round(fileInfo.Length / (float)GlobalConstants.MEBIBYTE, 2);

            runData.OutputWarningWithMutex(
                $"File {file} is bigger ({mebibytes} MiB) than is expected ({ExpectedMebibyteMaxSize} MiB) " +
                "for file based checks. Is a file that should be ignored being checked?");
        }
    }

    /// <summary>
    ///   Forwards a file to process (if not skipped) to a real processing function
    /// </summary>
    /// <param name="path">The file path</param>
    /// <param name="runData">Current run data to check for exclusions and report errors</param>
    /// <returns>Return true if everything was fine, false on error</returns>
    private async Task<bool> Handle(string path, CodeCheckRun runData)
    {
        if (!runData.ProcessFile(path))
            return true;

        bool success = true;

        // We don't load the file contents here as the OS should cache the file read really well without us potentially
        // loading a really large file all into memory at once. Also some checks need to work on more than just the

        foreach (var check in enabledChecks)
        {
            // Allow multiple checks to work on a single file as they can detect different things
            if (check.HandlesFile(path))
            {
                var errors = check.Handle(path);

                await foreach (var error in errors)
                {
                    if (success)
                    {
                        runData.ReportError($"Problems found in file {path}:");
                        success = false;
                    }

                    runData.OutputTextWithMutex(error);
                }
            }
        }

        if (!success)
        {
            // ReSharper disable HeuristicUnreachableCode RedundantIfElseBlock
#pragma warning disable CS0162
            if (PrintNameTwice)
            {
                runData.OutputErrorWithMutex($"Problems found in file (see above): {path}");
            }
            else
            {
                runData.OutputErrorWithMutex("Problems found in file (see above)");
            }

            // ReSharper restore HeuristicUnreachableCode RedundantIfElseBlock
#pragma warning restore CS0162
        }

        return success;
    }
}
