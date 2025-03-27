namespace ScriptsBase.Checks;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
///   Checks that given files exist
/// </summary>
public class FileMustExist : CodeCheck
{
    private readonly string[] requiredFiles;

    public FileMustExist(params string[] requiredFiles)
    {
        if (requiredFiles.Length == 0)
            throw new ArgumentException("Must provide at least one file to check for");

        this.requiredFiles = requiredFiles;
    }

    public override Task Run(CodeCheckRun runData, CancellationToken cancellationToken)
    {
        foreach (var requiredFile in requiredFiles)
        {
            if (File.Exists(requiredFile) && new FileInfo(requiredFile).Length > 0)
                continue;

            runData.ReportError($"File {requiredFile} does not exist");
        }

        return Task.CompletedTask;
    }
}
