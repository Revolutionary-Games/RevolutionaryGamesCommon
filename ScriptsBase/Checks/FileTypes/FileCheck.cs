namespace ScriptsBase.Checks.FileTypes;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
///   Base class for the various file based checks
/// </summary>
public abstract class FileCheck
{
    /// <summary>
    ///   Sets the types of files this check runs on. <see cref="string.Empty"/> means any file type
    /// </summary>
    /// <param name="firstHandledFileEnding">The primary file type this runs on</param>
    /// <param name="extraHandledFileEndings">Optional list of extra types to run on</param>
    protected FileCheck(string firstHandledFileEnding, params string[] extraHandledFileEndings)
    {
        var fullList = new List<string> { firstHandledFileEnding };

        fullList.AddRange(extraHandledFileEndings);

        if (fullList.Contains(string.Empty))
        {
            if (fullList.Count > 1)
            {
                throw new ArgumentException(
                    "Running on all files should be the only type specified if that is desired");
            }
        }

        HandledFileEndings = fullList;
    }

    /// <summary>
    ///   Sets the types of files this check runs on. This variant takes just one list to specify all the types
    ///   to run on.
    /// </summary>
    /// <param name="allHandledFileExtensions">All file extensions this runs on. May not be empty</param>
    protected FileCheck(params string[] allHandledFileExtensions)
    {
        if (allHandledFileExtensions.Length < 1)
            throw new ArgumentException("Must give at least  one file type to run on");

        if (allHandledFileExtensions.Count(e => e == string.Empty) > 1)
        {
            throw new ArgumentException("Running on all files should be specified only once");
        }

        HandledFileEndings = allHandledFileExtensions;
    }

    public List<string>? IgnoredFiles { get; set; }

    /// <summary>
    ///   Contains a list of file endings (extensions) that this check handles
    /// </summary>
    private IReadOnlyCollection<string> HandledFileEndings { get; }

    public virtual bool HandlesFile(string file)
    {
        if (IgnoredFiles != null)
        {
            if (IgnoredFiles.Any(file.EndsWith))
                return false;
        }

        return HandledFileEndings.Any(file.EndsWith);
    }

    public abstract IAsyncEnumerable<string> Handle(string path);
}
