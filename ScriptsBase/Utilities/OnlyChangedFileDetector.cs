namespace ScriptsBase.Utilities;

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Models;
using SharedBase.Utilities;

/// <summary>
///   Used to detect a subset of all files to run checks on
/// </summary>
public static class OnlyChangedFileDetector
{
    public const string ONLY_FILE_LIST = "files_to_check.txt";

    /// <summary>
    ///   Detects if there is a file telling which files to check
    /// </summary>
    /// <returns>The list of files to check or null</returns>
    public static IEnumerable<string>? DetectOnlySomeFilesConfiguredForChecking()
    {
        if (!File.Exists(ONLY_FILE_LIST))
            return null;

        var result = new HashSet<string>();

        foreach (var line in File.ReadLines(ONLY_FILE_LIST, Encoding.UTF8))
        {
            var processed = line.Trim().Replace("./", "").TrimStart('/');

            if (processed.Length > 0)
                result.Add(processed);
        }

        if (result.Count < 1)
            return null;

        return result;
    }

    public static bool IncludesChangesToFileType(string suffix, IEnumerable<string>? files)
    {
        if (files == null)
            return true;

        return files.Any(f => f.EndsWith(suffix));
    }

    public static async Task<bool> BuildListOfChangedFiles(ChangesOptionsBase opts)
    {
        var tokenSource = ConsoleHelpers.CreateSimpleConsoleCancellationSource();
        var cancellationToken = tokenSource.Token;

        var text = new StringBuilder();

        await GitRunHelpers.Fetch("./", opts.RemoteBranch, opts.Remote, cancellationToken, false);

        var startInfo = new ProcessStartInfo("git");
        startInfo.ArgumentList.Add("diff-tree");
        startInfo.ArgumentList.Add("--no-commit-id");
        startInfo.ArgumentList.Add("--name-only");
        startInfo.ArgumentList.Add("-r");
        startInfo.ArgumentList.Add($"HEAD..{opts.Remote}/{opts.RemoteBranch}");

        if (!await RunAndAppend(startInfo, text, cancellationToken))
            return false;

        startInfo = new ProcessStartInfo("git");
        startInfo.ArgumentList.Add("diff-tree");
        startInfo.ArgumentList.Add("--no-commit-id");
        startInfo.ArgumentList.Add("--name-only");
        startInfo.ArgumentList.Add("-r");
        startInfo.ArgumentList.Add("HEAD");

        if (!await RunAndAppend(startInfo, text, cancellationToken))
            return false;

        startInfo = new ProcessStartInfo("git");
        startInfo.ArgumentList.Add("diff");
        startInfo.ArgumentList.Add("--name-only");
        startInfo.ArgumentList.Add("--cached");

        if (!await RunAndAppend(startInfo, text, cancellationToken))
            return false;

        startInfo = new ProcessStartInfo("git");
        startInfo.ArgumentList.Add("diff");
        startInfo.ArgumentList.Add("--name-only");

        if (!await RunAndAppend(startInfo, text, cancellationToken))
            return false;

        if (cancellationToken.IsCancellationRequested)
            return false;

        await File.WriteAllTextAsync(ONLY_FILE_LIST, text.ToString(), cancellationToken);

        ColourConsole.WriteInfoLine($"Created list of changed files in {ONLY_FILE_LIST}");

        return true;
    }

    private static async Task<bool> RunAndAppend(ProcessStartInfo startInfo, StringBuilder builder,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return false;

        var result = await ProcessRunHelpers.RunProcessAsync(startInfo, cancellationToken, true);

        if (result.ExitCode != 0)
        {
            ColourConsole.WriteErrorLine("Failed to run git");
            return false;
        }

        var output = result.Output.Trim();

        if (output.Length > 0)
        {
            builder.Append(output);
            builder.Append("\n");
        }

        return true;
    }
}
