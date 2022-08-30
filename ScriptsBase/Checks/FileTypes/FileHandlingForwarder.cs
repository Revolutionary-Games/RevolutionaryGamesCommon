namespace ScriptsBase.Checks.FileTypes;

using System.Collections.Generic;
using System.Threading.Tasks;

public static class FileHandlingForwarder
{
    /// <summary>
    ///   Forwards a file to process (if not skipped) to a real processing function
    /// </summary>
    /// <param name="path">The file path</param>
    /// <param name="runData">Current run data to check for exclusions and report errors</param>
    /// <returns>Return true if everything was fine, false on error</returns>
    public static async Task<bool> Handle(string path, CodeCheckRun runData)
    {
        if (!runData.ProcessFile(path))
            return true;

        IAsyncEnumerable<string> errors;

        if (path.EndsWith(".cs"))
        {
            errors = CSharp.Handle(path);
        }
        else
        {
            // Unhandled file type
            return true;
        }

        bool success = true;

        await foreach (var error in errors)
        {
            if (success)
            {
                runData.ReportError($"Problems found in file {path}:");
                success = false;
            }

            runData.OutputTextWithMutex(error);
        }

        if (!success)
            runData.OutputErrorWithMutex($"Problems found in file (see above): {path}");

        return success;
    }
}
