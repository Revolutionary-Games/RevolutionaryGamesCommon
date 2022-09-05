namespace ScriptsBase.Utilities;

using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Models;
using SharedBase.Utilities;

/// <summary>
///   Base class for handling running container operations
/// </summary>
/// <typeparam name="T">The type of the options class</typeparam>
public abstract class ContainerToolBase<T>
    where T : ContainerOptionsBase
{
    private readonly Regex builtImageId =
        new(@"COMMIT.*\n.*-->\s+[a-f0-9]+\s*\n([a-f0-9]+)\s+$", RegexOptions.IgnoreCase);

    private readonly Regex dotnetSdkInstalledVersion = new(@"([\d\.]+)\s\[\/usr\/lib(64)?\/dotnet\/sdk\]");

    private readonly T options;

    protected ContainerToolBase(T options)
    {
        this.options = options;
    }

    protected abstract string ExportFileNameBase { get; }
    protected abstract string ImagesAndConfigsFolder { get; }
    protected abstract string DefaultImageToBuild { get; }
    protected abstract string ImageNameBase { get; }

    public async Task<bool> Run(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Version))
            options.Version = "latest";

        if (options.Version != "latest")
        {
            if (!int.TryParse(options.Version, out var versionNumber))
            {
                ColourConsole.WriteErrorLine("Expected version to be 'latest' or a number");
                return false;
            }

            options.Version = $"v{versionNumber}";
        }

        ColourConsole.WriteInfoLine($"Building image version {options.Version}");

        string? tag = null;
        string? extraTag = null;

        if (options.Tag == true)
        {
            tag = $"{ImageNameBase}:{options.Version}";

            if (options.Latest == true && options.Version != "latest")
                extraTag = $"{ImageNameBase}:latest";
        }

        var builtImage = await Build(DefaultImageToBuild, tag, extraTag, cancellationToken);

        if (builtImage == null)
        {
            ColourConsole.WriteErrorLine("Failed to build image");
            return false;
        }

        if (options.Export == true)
        {
            if (!await ExportAsFile(builtImage, options.Version, cancellationToken))
            {
                ColourConsole.WriteErrorLine("Failed to save image as file");
                return false;
            }
        }

        ColourConsole.WriteSuccessLine("Container operations succeeded");
        return true;
    }

    protected virtual Task<bool> PostCheckBuild(string tagOrId)
    {
        return Task.FromResult(true);
    }

    protected async Task<bool> CheckDotnetSdkWasInstalled(string tagOrId)
    {
        var startInfo = new ProcessStartInfo("podman");
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--rm");
        startInfo.ArgumentList.Add(tagOrId);
        startInfo.ArgumentList.Add("dotnet");
        startInfo.ArgumentList.Add("--list-sdks");

        var result = await ProcessRunHelpers.RunProcessAsync(startInfo, CancellationToken.None, true);

        if (result.ExitCode != 0)
        {
            ColourConsole.WriteErrorLine(
                $"Failed to run podman command to determine if dotnet SDK install succeeded: {result.FullOutput}");
            return false;
        }

        var match = dotnetSdkInstalledVersion.Match(result.FullOutput);

        if (!match.Success)
        {
            ColourConsole.WriteErrorLine(
                "Could not determine that dotnet SDK was successfully installed in image, " +
                $"output: {result.FullOutput}");
            return false;
        }

        var installedVersion = match.Groups[1].Value;

        ColourConsole.WriteInfoLine($"Verified image has installed dotnet SDK correctly ({installedVersion})");
        return true;
    }

    private async Task<string?> Build(string buildType, string? tag, string? extraTag,
        CancellationToken cancellationToken)
    {
        var folder = Path.Join(ImagesAndConfigsFolder, buildType);

        var startInfo = new ProcessStartInfo("podman");
        startInfo.ArgumentList.Add("build");
        startInfo.ArgumentList.Add(folder);

        bool capture = tag == null;

        if (capture)
        {
            ColourConsole.WriteNormalLine(
                "As tagging is not enabled, building with output captured (you won't see any output " +
                "until build is finished)");
        }
        else
        {
            startInfo.ArgumentList.Add($"--tag={tag}");

            if (extraTag != null)
            {
                startInfo.ArgumentList.Add($"--tag={extraTag}");
            }
        }

        var result = await ProcessRunHelpers.RunProcessAsync(startInfo, cancellationToken, capture);

        if (result.ExitCode != 0)
        {
            if (capture)
                ColourConsole.WriteNormalLine(result.FullOutput);
            ColourConsole.WriteErrorLine($"Failed to build with podman (exit: {result.ExitCode})");
            return null;
        }

        if (tag == null)
        {
            ColourConsole.WriteNormalLine(result.FullOutput);

            string? id = null;

            // TODO: test that the following regex works
            var match = builtImageId.Match(result.FullOutput);

            if (match.Success)
            {
                id = match.Groups[1].Value;

                ColourConsole.WriteInfoLine($"Detected built image as: {id}");
            }

            if (id == null)
            {
                ColourConsole.WriteErrorLine("Could not detect built image id from output");
                return null;
            }

            if (!await PostCheckBuild(id))
            {
                ColourConsole.WriteErrorLine("Post build check failed");
                return null;
            }

            ColourConsole.WriteSuccessLine($"Successfully built: {id}");
            return id;
        }

        if (!await PostCheckBuild(tag))
        {
            ColourConsole.WriteErrorLine("Post build check failed");
            return null;
        }

        ColourConsole.WriteSuccessLine($"Successfully built and tagged: {tag}");

        return tag;
    }

    private async Task<bool> ExportAsFile(string tag, string version, CancellationToken cancellationToken)
    {
        var file = Path.Join(ImagesAndConfigsFolder, $"{ExportFileNameBase}_{version}.tar.xz");

        var startInfo = new ProcessStartInfo("podman");
        startInfo.ArgumentList.Add("save");
        startInfo.ArgumentList.Add($"{tag}");
        startInfo.ArgumentList.Add("-o");
        startInfo.ArgumentList.Add(file);

        var result = await ProcessRunHelpers.RunProcessAsync(startInfo, cancellationToken, true);

        if (result.ExitCode != 0)
        {
            ColourConsole.WriteErrorLine($"Failed to save with podman: {result.FullOutput}");
            return false;
        }

        ColourConsole.WriteInfoLine($"Successfully saved: {file}");
        return true;
    }
}
