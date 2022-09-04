namespace ScriptsBase.Utilities;

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SharedBase.Utilities;

public static class PipPackageVersionChecker
{
    private static readonly Regex PipBabelThriveVersion = new(@"^Babel-Thrive\s*([\d.]+)", RegexOptions.Multiline);

    private static readonly Regex RequirementsBabelThriveVersion =
        new(@"^Babel-Thrive==([\d.]+)$", RegexOptions.Multiline);

    public static async Task<(bool Matches, string Installed, string Wanted)> CompareInstalledBabelThriveVersion(
        CancellationToken cancellationToken,
        string requirementsFile = "docker/jsonlint/requirements.txt")
    {
        var installed = await GetInstalledBabelThriveVersion(cancellationToken);
        var wanted = await GetWantedBabelThriveVersion(requirementsFile, cancellationToken);

        return (installed == wanted, installed, wanted);
    }

    public static async Task<string> ListPipPackages(CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(RequirePipExecutable());
        startInfo.ArgumentList.Add("list");

        var result = await ProcessRunHelpers.RunProcessAsync(startInfo, cancellationToken, true);

        if (result.ExitCode != 0)
        {
            throw new Exception($"Failed to run pip (exit: {result.ExitCode}): {result.FullOutput}");
        }

        // TODO: we could already preprocess the data here into package and version pairs
        return result.Output;
    }

    public static string? LookForPipExecutable()
    {
        return ExecutableFinder.Which("pip3") ?? ExecutableFinder.Which("pip");
    }

    public static string RequirePipExecutable()
    {
        var pip = LookForPipExecutable();

        if (string.IsNullOrEmpty(pip))
            throw new Exception("Could not find pip3 or pip. Please install it and add to PATH");

        return pip;
    }

    public static async Task<string> ReadRequirementsFile(string file, CancellationToken cancellationToken)
    {
        var fileContent = await File.ReadAllTextAsync(file, Encoding.UTF8, cancellationToken);

        // TODO: we could parse the file contents to package version pairs
        return fileContent;
    }

    private static async Task<string> GetInstalledBabelThriveVersion(CancellationToken cancellationToken)
    {
        var packages = await ListPipPackages(cancellationToken);

        var match = PipBabelThriveVersion.Match(packages);

        if (match.Success)
        {
            var value = match.Groups[1].Value;

            if (!string.IsNullOrEmpty(value))
                return value;
        }

        throw new Exception("Could not detect installed Babel-Thrive version. Please install it with pip");
    }

    private static async Task<string> GetWantedBabelThriveVersion(string file, CancellationToken cancellationToken)
    {
        var requirementsContents = await ReadRequirementsFile(file, cancellationToken);

        var match = RequirementsBabelThriveVersion.Match(requirementsContents);

        if (match.Success)
        {
            var value = match.Groups[1].Value;

            if (!string.IsNullOrEmpty(value))
                return value;
        }

        throw new Exception($"Could not read required Babel-Thrive version from: {file}");
    }
}
