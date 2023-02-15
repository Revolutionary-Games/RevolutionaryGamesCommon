namespace ScriptsBase.Checks;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Utilities;

/// <summary>
///   Represents a code check run
/// </summary>
public sealed class CodeCheckRun : IDisposable
{
    private readonly object outputLock = new();
    private readonly SemaphoreSlim dotnetToolRestoreMutex = new(1);

    private bool toolsRestored;

    private List<Regex> ignorePatterns = new();

    public bool Errors { get; private set; }

    public SemaphoreSlim BuildMutex { get; } = new(1);

    public bool InstallDotnetTools { get; set; }

    public string? SolutionFile { get; set; }
    public IList<string>? OnlyCheckFiles { get; private set; }
    public IList<string> ForceIgnoredJetbrainsInspections { get; set; } = new List<string>();
    public IList<string> ExtraIgnoredJetbrainsInspectWildcards { get; set; } = new List<string>();

    /// <summary>
    ///   Checks if a file should be processed
    /// </summary>
    /// <param name="file">The relative path to the file to check</param>
    /// <returns>True if should be processed</returns>
    public bool ProcessFile(string file)
    {
        if (OnlyCheckFiles != null)
        {
            foreach (var onlyCheckFile in OnlyCheckFiles)
            {
                if (file.EndsWith(onlyCheckFile))
                {
                    // Apply ignores on top of the specific list of files to ignore
                    if (IsFileIgnored(file))
                        return false;

                    return true;
                }
            }

            return false;
        }

        if (IsFileIgnored(file))
            return false;

        return true;
    }

    public bool IsFileIgnored(string file)
    {
        foreach (var ignorePattern in ignorePatterns)
        {
            if (ignorePattern.IsMatch(file))
                return true;
        }

        return false;
    }

    public async Task<bool> CheckDotnetTools()
    {
        if (!InstallDotnetTools)
        {
            OutputTextWithMutex("Skipping restoring dotnet tools, hopefully they are up to date");
            return true;
        }

        await dotnetToolRestoreMutex.WaitAsync();
        try
        {
            if (toolsRestored)
            {
                OutputTextWithMutex("Tools already restored");
                return true;
            }

            toolsRestored = true;

            OutputInfoWithMutex("Restoring dotnet tools to make sure they are up to date");
            if (!await DotnetToolInstaller.InstallDotnetTools())
            {
                ReportError("Failed to run dotnet tool restore");
                return false;
            }

            return true;
        }
        finally
        {
            dotnetToolRestoreMutex.Release();
        }
    }

    public void ReportError(string description)
    {
        if (!Errors)
            OutputErrorWithMutex("Checks have failed with the following error:");

        Errors = true;
        OutputErrorWithMutex(description);
    }

    public void OutputErrorWithMutex(string message)
    {
        lock (outputLock)
        {
            ColourConsole.WriteErrorLine(message);
        }
    }

    public void OutputWarningWithMutex(string message)
    {
        lock (outputLock)
        {
            ColourConsole.WriteWarningLine(message);
        }
    }

    public void OutputTextWithMutex(string message)
    {
        lock (outputLock)
        {
            ColourConsole.WriteNormalLine(message);
        }
    }

    public void OutputInfoWithMutex(string message)
    {
        lock (outputLock)
        {
            ColourConsole.WriteInfoLine(message);
        }
    }

    public void ReportCancel()
    {
        if (Errors)
            return;

        Errors = true;

        lock (outputLock)
        {
            ColourConsole.WriteWarningLine("Run has been canceled");
        }
    }

    public void Dispose()
    {
        Dispose(true);
    }

    internal void SetIgnoredFiles(IEnumerable<Regex> patterns)
    {
        ignorePatterns = patterns.ToList();
    }

    internal void SetSpecificSetOfFilesToCheck(IList<string>? files)
    {
        OnlyCheckFiles = files;
    }

    internal void ApplyCommandLineIncludes(ICollection<string>? include, ICollection<string>? exclude)
    {
        if (include is { Count: > 0 })
        {
            OnlyCheckFiles ??= new List<string>();

            foreach (var toInclude in include)
            {
                OnlyCheckFiles.Add(toInclude);
            }
        }

        if (exclude is { Count: > 0 })
        {
            foreach (var toExclude in exclude)
            {
                ignorePatterns.Add(new Regex(toExclude));
            }
        }
    }

    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            dotnetToolRestoreMutex.Dispose();
            BuildMutex.Dispose();
        }
    }
}
