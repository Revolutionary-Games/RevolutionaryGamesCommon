namespace ScriptsBase.Checks;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileTypes;
using Utilities;

/// <summary>
///   Runs checks that are implemented in C# on present files in the repository
/// </summary>
public class FileChecks : CodeCheck
{
    private const bool PrintNameTwice = false;

    private const string StartFileEnumerateFolder = "./";

    private static readonly bool NeedsToReplacePaths = Path.DirectorySeparatorChar != '/';

    /// <summary>
    ///   Known list if binary file extensions to prevent checks from running on them
    /// </summary>
    private static readonly IReadOnlyList<string> knownBinaryFiles = new List<string>
    {
        ".obj",
        ".dll",
        ".a",
        ".so",
        ".bin",
    };

    private readonly List<FileCheck> enabledChecks = new();

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
    }

    public override async Task Run(CodeCheckRun runData, CancellationToken cancellationToken)
    {
        bool errors = false;

        foreach (var file in EnumerateFilesRecursively(StartFileEnumerateFolder, runData))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (ColourConsole.DebugPrintingEnabled)
                runData.OutputTextWithMutex($"Handling: {file}");

            try
            {
                if (!await Handle(file, runData))
                {
                    // Give a little bit of extra spacing between the files with errors
                    runData.OutputTextWithMutex(string.Empty);
                    errors = true;

                    // Don't stop here as we want all file errors at once
                }
            }
            catch (Exception e)
            {
                runData.OutputTextWithMutex($"An exception occurred when handling a file ({file}): {e}");
                runData.ReportError($"Error: {e.Message}");
                return;
            }
        }

        if (errors)
        {
            runData.ReportError("Code format issues detected");
        }
    }

    private static IEnumerable<string> EnumerateFilesRecursively(string start, CodeCheckRun runData)
    {
        foreach (var file in Directory.EnumerateFiles(start))
        {
            string handledFile;
            if (file.StartsWith(StartFileEnumerateFolder))
            {
                handledFile = file.Substring(StartFileEnumerateFolder.Length);
            }
            else
            {
                handledFile = file;
            }

            // Hopefully this isn't too bad performance for Windows people
            if (NeedsToReplacePaths)
            {
                handledFile = file.Replace(Path.DirectorySeparatorChar, '/');
            }

            if (runData.ProcessFile(handledFile))
            {
                // Skip handling any binary files as the handlers (even ones that work on *any* file type, don't really
                // want to run on them). If we ever get a binary type that needs checking we'll need some way to say
                // *really* any file and text files only for a check.
                if (knownBinaryFiles.Any(handledFile.EndsWith))
                    continue;

                yield return handledFile;
            }
        }

        foreach (var folder in Directory.EnumerateDirectories(start))
        {
            foreach (var recursiveCall in EnumerateFilesRecursively(folder, runData))
            {
                yield return recursiveCall;
            }
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
