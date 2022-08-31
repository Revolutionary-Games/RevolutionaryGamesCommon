namespace ScriptsBase.Checks;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FileTypes;
using Utilities;

/// <summary>
///   Runs checks that are implemented in C# on present files in the repository
/// </summary>
public class FileChecks : CodeCheck
{
    private const string StartFileEnumerateFolder = "./";

    private static readonly bool NeedsToReplacePaths = Path.DirectorySeparatorChar != '/';

    public override async Task Run(CodeCheckRun runData, CancellationToken cancellationToken)
    {
        bool errors = false;

        foreach (var file in EnumerateFilesRecursively(StartFileEnumerateFolder, runData))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (ColourConsole.DebugPrintingEnabled)
            {
                runData.OutputTextWithMutex($"Handling: {file}");
            }

            try
            {
                if (!await FileHandlingForwarder.Handle(file, runData))
                {
                    // Give a little bit of extra spacing between the files with errors
                    runData.OutputTextWithMutex("");
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
}
