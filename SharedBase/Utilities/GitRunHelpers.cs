namespace SharedBase.Utilities;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

public static class GitRunHelpers
{
    private const string PullRequestRefSuffix = "/head";
    private const string NormalRefPrefix = "refs/heads/";

    [UnsupportedOSPlatform("browser")]
    public static async Task EnsureRepoIsCloned(string repoURL, string folder, bool skipLFS,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(FindGit()) { CreateNoWindow = true };

        if (skipLFS)
            SetLFSSmudgeSkip(startInfo);

        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(folder) ??
                throw new Exception("Could not get parent folder to put the repository in"));

            // Need to clone
            startInfo.ArgumentList.Add("clone");
            startInfo.ArgumentList.Add(repoURL);
            startInfo.ArgumentList.Add(folder);
        }
        else
        {
            // Just update remote
            startInfo.WorkingDirectory = folder;
            startInfo.ArgumentList.Add("remote");
            startInfo.ArgumentList.Add("set-url");
            startInfo.ArgumentList.Add("origin");
            startInfo.ArgumentList.Add(repoURL);
        }

        var result = await ProcessRunHelpers.RunProcessAsync(startInfo, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new Exception(
                $"Failed to make sure repo is cloned, process exited with error: {result.FullOutput}");
        }
    }

    [UnsupportedOSPlatform("browser")]
    public static async Task Checkout(string folder, string whatToCheckout, bool skipLFS,
        CancellationToken cancellationToken, bool force = false)
    {
        var startInfo = PrepareToRunGit(folder, skipLFS);
        startInfo.ArgumentList.Add("checkout");
        startInfo.ArgumentList.Add(whatToCheckout);

        if (force)
            startInfo.ArgumentList.Add("--force");

        var result = await ProcessRunHelpers.RunProcessAsync(startInfo, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new Exception(
                $"Failed to checkout in repo, process exited with error: {result.FullOutput}");
        }
    }

    [UnsupportedOSPlatform("browser")]
    public static async Task UpdateSubmodules(string folder, bool init, bool recursive,
        CancellationToken cancellationToken)
    {
        var startInfo = PrepareToRunGit(folder, false);
        startInfo.ArgumentList.Add("submodule");
        startInfo.ArgumentList.Add("update");

        if (init)
            startInfo.ArgumentList.Add("--init");

        if (recursive)
            startInfo.ArgumentList.Add("--recursive");

        var result = await ProcessRunHelpers.RunProcessAsync(startInfo, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new Exception(
                $"Failed to update submodules in repo, process exited with error: {result.FullOutput}");
        }
    }

    [UnsupportedOSPlatform("browser")]
    public static async Task Pull(string folder, bool skipLFS, CancellationToken cancellationToken,
        bool force = false)
    {
        var startInfo = PrepareToRunGit(folder, skipLFS);
        startInfo.ArgumentList.Add("pull");

        if (force)
            startInfo.ArgumentList.Add("--force");

        var result = await ProcessRunHelpers.RunProcessAsync(startInfo, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new Exception(
                $"Failed to pull in repo, process exited with error: {result.FullOutput}");
        }
    }

    [UnsupportedOSPlatform("browser")]
    public static async Task Fetch(string folder, bool all, CancellationToken cancellationToken)
    {
        var startInfo = PrepareToRunGit(folder, true);
        startInfo.ArgumentList.Add("fetch");

        if (all)
            startInfo.ArgumentList.Add("--all");

        var result = await ProcessRunHelpers.RunProcessAsync(startInfo, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new Exception(
                $"Failed to fetch in repo, process exited with error: {result.FullOutput}");
        }
    }

    [UnsupportedOSPlatform("browser")]
    public static async Task Fetch(string folder, string thing, string remote, CancellationToken cancellationToken,
        bool force = true)
    {
        var startInfo = PrepareToRunGit(folder, true);
        startInfo.ArgumentList.Add("fetch");
        startInfo.ArgumentList.Add(remote);
        startInfo.ArgumentList.Add(thing);

        if (force)
            startInfo.ArgumentList.Add("--force");

        var result = await ProcessRunHelpers.RunProcessAsync(startInfo, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new Exception(
                $"Failed to fetch (thing) in repo, process exited with error: {result.FullOutput}");
        }
    }

    /// <summary>
    ///   Gets the current commit in a git repository in the long form
    /// </summary>
    /// <param name="folder">The repository folder</param>
    /// <param name="cancellationToken">Cancellation token for the git process</param>
    /// <returns>The current commit hash</returns>
    /// <exception cref="Exception">If getting the commit hash fails</exception>
    [UnsupportedOSPlatform("browser")]
    public static async Task<string> GetCurrentCommit(string folder, CancellationToken cancellationToken)
    {
        var startInfo = PrepareToRunGit(folder, true);
        startInfo.ArgumentList.Add("rev-parse");

        // Try to force it being shown as a hash
        startInfo.ArgumentList.Add("--verify");
        startInfo.ArgumentList.Add("HEAD");

        var result = await ProcessRunHelpers.RunProcessAsync(startInfo, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new Exception(
                $"Failed to run rev-parse in repo, process exited with error: {result.FullOutput}");
        }

        var resultText = result.Output.Trim();

        if (string.IsNullOrEmpty(resultText))
        {
            throw new Exception(
                $"Failed to run rev-parse in repo, empty output (code: {result.ExitCode}). " +
                $"Error output (if any): {result.ErrorOut}, normal output: {result.Output}");
        }

        // Looks like sometimes the result is truncated hash, try to detect that here and fail
        if (resultText.Length < 20)
        {
            throw new Exception(
                $"Failed to run rev-parse in repo, output is not full hash length (code: {result.ExitCode}). " +
                $"Error output (if any): {result.ErrorOut}, normal output: {result.Output}");
        }

        return resultText;
    }

    [UnsupportedOSPlatform("browser")]
    public static async Task<string> GetCurrentBranch(string folder, CancellationToken cancellationToken)
    {
        var startInfo = PrepareToRunGit(folder, true);
        startInfo.ArgumentList.Add("rev-parse");

        // Try to force it being shown as a hash
        startInfo.ArgumentList.Add("--symbolic-full-name");
        startInfo.ArgumentList.Add("--abbrev-ref");
        startInfo.ArgumentList.Add("HEAD");

        var result = await ProcessRunHelpers.RunProcessAsync(startInfo, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new Exception(
                $"Failed to run rev-parse in repo, process exited with error: {result.FullOutput}");
        }

        var resultText = result.Output.Trim();

        if (string.IsNullOrEmpty(resultText))
        {
            throw new Exception(
                $"Failed to run rev-parse in repo (for branch name), empty output (code: {result.ExitCode}). " +
                $"Error output (if any): {result.ErrorOut}, normal output: {result.Output}");
        }

        return resultText;
    }

    /// <summary>
    ///   Handles the differences between checking a github PR or just a remote ref branch
    /// </summary>
    /// <param name="folder">The fit folder to operate in</param>
    /// <param name="refToCheckout">Ref from Github that should be checked out locally</param>
    /// <param name="skipLFS">If true LFS handling is skipped</param>
    /// <param name="cancellationToken">Cancel the operation early</param>
    [UnsupportedOSPlatform("browser")]
    public static async Task SmartlyCheckoutRef(string folder, string refToCheckout, bool skipLFS,
        CancellationToken cancellationToken)
    {
        const string remote = "origin";
        var parsed = ParseRemoteRef(refToCheckout, remote);

        if (IsPullRequestRef(refToCheckout))
        {
            await Fetch(folder, $"{refToCheckout}:{parsed.LocalBranch}", remote, cancellationToken);
        }
        else
        {
            await Fetch(folder, refToCheckout, remote, cancellationToken);
        }

        await Checkout(folder, parsed.LocalRef, skipLFS, cancellationToken, true);
    }

    [UnsupportedOSPlatform("browser")]
    public static async Task FetchRef(string folder, string refToFetch, CancellationToken cancellationToken)
    {
        const string remote = "origin";
        var parsed = ParseRemoteRef(refToFetch, remote);

        if (IsPullRequestRef(refToFetch))
        {
            await Fetch(folder, $"{refToFetch}:{parsed.LocalBranch}", remote, cancellationToken);
        }
        else
        {
            await Fetch(folder, refToFetch, remote, cancellationToken);
        }
    }

    /// <summary>
    ///   Gets a git diff
    /// </summary>
    /// <param name="folder">
    ///   If specified this is used as working directory, otherwise current working directory is used
    /// </param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <param name="stat">If true "--stat" is passed to git</param>
    /// <param name="useWindowsWorkaround">
    ///   If true special diff parameters are used to make things work on Windows
    /// </param>
    /// <returns>The output from git containing the diff</returns>
    [UnsupportedOSPlatform("browser")]
    public static async Task<string> Diff(string folder, CancellationToken cancellationToken, bool stat = true,
        bool useWindowsWorkaround = true)
    {
        if (!Directory.Exists(folder))
            throw new ArgumentException($"Specified folder: \"{folder}\" doesn't exist");

        var startInfo = new ProcessStartInfo(FindGit()) { CreateNoWindow = true, WorkingDirectory = folder };

        if (useWindowsWorkaround)
        {
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add("core.safecrlf=false");
        }

        startInfo.ArgumentList.Add("diff");

        if (stat)
            startInfo.ArgumentList.Add("--stat");

        var result = await ProcessRunHelpers.RunProcessAsync(startInfo, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new Exception(
                $"Failed to get git diff, process exited with error: {result.FullOutput}");
        }

        return result.Output.Trim();
    }

    [UnsupportedOSPlatform("browser")]
    public static async Task<string> Log(string folder, int limit, CancellationToken cancellationToken)
    {
        var startInfo = PrepareToRunGit(folder, true);
        startInfo.ArgumentList.Add("log");
        startInfo.ArgumentList.Add("-n");
        startInfo.ArgumentList.Add(limit.ToString());

        var result = await ProcessRunHelpers.RunProcessAsync(startInfo, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new Exception(
                $"Failed to run log in repo, process exited with error: {result.FullOutput}");
        }

        return result.Output.Trim();
    }

    [UnsupportedOSPlatform("browser")]
    public static async Task<string> SubmoduleInfo(string folder, CancellationToken cancellationToken)
    {
        var startInfo = PrepareToRunGit(folder, true);
        startInfo.ArgumentList.Add("submodule");

        var result = await ProcessRunHelpers.RunProcessAsync(startInfo, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new Exception(
                $"Failed to run submodule list in repo, process exited with error: {result.FullOutput}");
        }

        return result.Output.Trim();
    }

    [UnsupportedOSPlatform("browser")]
    public static async Task<string> DiffNameOnly(string folder, bool cached, CancellationToken cancellationToken,
        bool useWindowsWorkaround = true)
    {
        var startInfo = PrepareToRunGit(folder, false);

        if (useWindowsWorkaround)
        {
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add("core.safecrlf=false");
        }

        startInfo.ArgumentList.Add("diff");

        if (cached)
            startInfo.ArgumentList.Add("--cached");

        startInfo.ArgumentList.Add("--name-only");

        var result = await ProcessRunHelpers.RunProcessAsync(startInfo, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new Exception(
                $"Failed to get git diff, process exited with error: {result.FullOutput}");
        }

        return result.Output.Trim();
    }

    [UnsupportedOSPlatform("browser")]
    public static async Task<string> Clean(string folder, CancellationToken cancellationToken,
        bool removeUntrackedDirectories = true)
    {
        if (!Directory.Exists(folder))
            throw new ArgumentException($"Specified folder: \"{folder}\" doesn't exist");

        var startInfo = new ProcessStartInfo(FindGit()) { CreateNoWindow = true, WorkingDirectory = folder };

        startInfo.ArgumentList.Add("clean");
        startInfo.ArgumentList.Add("-f");

        if (removeUntrackedDirectories)
            startInfo.ArgumentList.Add("-d");

        var result = await ProcessRunHelpers.RunProcessAsync(startInfo, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new Exception(
                $"Failed to clean repo, process exited with error: {result.FullOutput}");
        }

        return result.Output;
    }

    [UnsupportedOSPlatform("browser")]
    public static async Task Reset(string folder, string whereToResetTo, bool hard,
        CancellationToken cancellationToken)
    {
        var startInfo = PrepareToRunGit(folder, false);
        startInfo.ArgumentList.Add("reset");

        if (hard)
            startInfo.ArgumentList.Add("--hard");

        startInfo.ArgumentList.Add(whereToResetTo);

        var result = await ProcessRunHelpers.RunProcessAsync(startInfo, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new Exception(
                $"Failed to reset in repo, process exited with error: {result.FullOutput}");
        }
    }

    public static bool IsPullRequestRef(string remoteRef)
    {
        if (remoteRef.StartsWith("pull/"))
            return true;

        return false;
    }

    public static string GenerateRefForPullRequest(long id)
    {
        return $"pull/{id}/head";
    }

    public static (string LocalBranch, string LocalRef) ParseRemoteRef(string remoteRef, string remote = "origin")
    {
        string localHeadsRef = $"refs/remotes/{remote}/";

        if (IsPullRequestRef(remoteRef))
        {
            if (remoteRef.EndsWith(PullRequestRefSuffix))
            {
                var localBranch = remoteRef.Substring(0, remoteRef.Length - PullRequestRefSuffix.Length);
                localHeadsRef += localBranch;
                return (localBranch, localHeadsRef);
            }

            throw new Exception($"Unrecognized PR ref: {remoteRef}");
        }

        if (remoteRef.StartsWith(NormalRefPrefix))
        {
            var localBranch = remoteRef.Substring(NormalRefPrefix.Length);
            localHeadsRef += localBranch;
            return (localBranch, localHeadsRef);
        }

        throw new Exception($"Unrecognized normal ref: {remoteRef}");
    }

    public static string ParseRefBranch(string remoteRef)
    {
        return ParseRemoteRef(remoteRef).LocalBranch;
    }

    /// <summary>
    ///   Parses a .gitattributes file for binary file types (note this doesn't fully convert the wildcard patterns to
    ///   regexes)
    /// </summary>
    /// <returns>A list of binary extensions</returns>
    public static async Task<List<string>> ParseGitAttributeBinaryFiles(string folder, bool required,
        CancellationToken cancellationToken)
    {
        var file = Path.Join(folder, ".gitattributes");

        var result = new List<string>();

        if (!required && !File.Exists(file))
            return result;

        // TODO: could move up folders until finding a gitattributes file

        var lines = await File.ReadAllLinesAsync(file, cancellationToken);

        foreach (var line in lines)
        {
            var split = line.Split(' ', 2);
            var pattern = split[0];
            var options = split[1];

            if (options.Contains("-text") || options.Contains("text=false") || options.Contains("binary"))
            {
                result.Add(pattern.TrimStart('*', '?'));
            }
        }

        return result;
    }

    [UnsupportedOSPlatform("browser")]
    private static ProcessStartInfo PrepareToRunGit(string folder, bool skipLFS)
    {
        if (!Directory.Exists(folder))
            throw new ArgumentException($"Specified folder: \"{folder}\" doesn't exist");

        var startInfo = new ProcessStartInfo(FindGit()) { CreateNoWindow = true };

        if (skipLFS)
            SetLFSSmudgeSkip(startInfo);

        startInfo.WorkingDirectory = folder;
        return startInfo;
    }

    [UnsupportedOSPlatform("browser")]
    private static void SetLFSSmudgeSkip(ProcessStartInfo startInfo)
    {
        startInfo.Environment["GIT_LFS_SKIP_SMUDGE"] = "1";
    }

    private static string FindGit()
    {
        var git = ExecutableFinder.Which("git");

        if (git == null)
            throw new Exception("Git executable not found, please install it");

        return git;
    }
}
