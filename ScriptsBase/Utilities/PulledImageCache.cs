namespace ScriptsBase.Utilities;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SharedBase.Utilities;

/// <summary>
///   Simple cache for info on how long ago a docker image was last pulled. Multiple shouldn't be used at once as
///   multiple instances won't share their internal data so some cache writes will be lost.
/// </summary>
public class PulledImageCache
{
    private readonly Dictionary<string, DateTime> cacheData;

    private bool dirty;

    /// <summary>
    ///   Create a blank cache
    /// </summary>
    public PulledImageCache()
    {
        dirty = true;
        cacheData = new Dictionary<string, DateTime>();
    }

    protected PulledImageCache(Dictionary<string, DateTime> data)
    {
        cacheData = data;
    }

    public static async Task<PulledImageCache> Load()
    {
        var path = GetSavePath();
        ColourConsole.WriteDebugLine($"Reading pulled image cache file from {path}");

        if (!File.Exists(path))
        {
            ColourConsole.WriteDebugLine("Pulled image cache doesn't exist");
            return new PulledImageCache();
        }

        try
        {
            await using var reader = File.OpenRead(path);
            return new PulledImageCache(await JsonSerializer.DeserializeAsync<Dictionary<string, DateTime>>(reader) ??
                throw new NullDecodedJsonException());
        }
        catch (Exception e)
        {
            ColourConsole.WriteErrorLine("Error loading pulled image cache: " + e);
            return new PulledImageCache();
        }
    }

    public static async Task<bool> RefreshImagePullsIfNeeded(IEnumerable<string> images, TimeSpan rePullOlderThan,
        CancellationToken cancellationToken)
    {
        var imagesToCheck = images.ToList();

        if (imagesToCheck.Count < 1)
        {
            ColourConsole.WriteDebugLine("No images configured to re-pull automatically");
            return true;
        }

        var cache = await Load();
        bool failure = false;

        foreach (var image in imagesToCheck)
        {
            ColourConsole.WriteNormalLine($"Checking that \"{image}\" has been recently pulled...");

            if (cache.TryGetValue(image, out var pulledAt))
            {
                if (DateTime.UtcNow - pulledAt < rePullOlderThan)
                {
                    ColourConsole.WriteInfoLine(
                        $"{image} has been pulled in the past {rePullOlderThan}");
                    continue;
                }

                ColourConsole.WriteNormalLine($"{image} has not been pulled recently, will pull it");
            }
            else
            {
                ColourConsole.WriteNormalLine($"{image} has not been pulled");
            }

            try
            {
                var startInfo = new ProcessStartInfo("podman");
                startInfo.ArgumentList.Add("pull");
                startInfo.ArgumentList.Add(image);

                ColourConsole.WriteInfoLine($"Pulling '{image}'...");

                var result = await ProcessRunHelpers.RunProcessAsync(startInfo, cancellationToken);

                if (result.ExitCode != 0)
                {
                    ColourConsole.WriteErrorLine(
                        $"Failed to pull image '{image}' with podman (exit: {result.ExitCode})");
                    return false;
                }

                cache.UpdatePullTime(image);

                ColourConsole.WriteSuccessLine($"Pulled podman image '{image}'");
            }
            catch (Exception e)
            {
                ColourConsole.WriteErrorLine(
                    $"Failed to pull wanted image '{image}' (skip with '--pull-refresh false'): {e}");
                failure = true;
                break;
            }
        }

        await cache.Save();

        return !failure;
    }

    public bool TryGetValue(string imageName, out DateTime lastPulled)
    {
        return cacheData.TryGetValue(imageName, out lastPulled);
    }

    /// <summary>
    ///   Updates last pulled time to current time and marks this dirty
    /// </summary>
    /// <param name="imageName">The image for which the last used time should be updated</param>
    public void UpdatePullTime(string imageName)
    {
        dirty = true;
        cacheData[imageName] = DateTime.UtcNow;
    }

    /// <summary>
    ///   Saves this cache to disk if this is dirty
    /// </summary>
    /// <returns>True when saved, false if no saving was needed</returns>
    public async Task<bool> Save()
    {
        if (!dirty)
            return false;

        ColourConsole.WriteDebugLine("Writing pulled image cache file");

        try
        {
            Directory.CreateDirectory(GetSaveFolder());

            await using var writer = File.OpenWrite(GetSavePath());

            await JsonSerializer.SerializeAsync(writer, cacheData);
        }
        catch (Exception e)
        {
            ColourConsole.WriteErrorLine("Error writing pulled image cache: " + e);
            return false;
        }

        return true;
    }

    private static string GetSavePath()
    {
        return Path.Combine(GetSaveFolder(), "LastPulls.json");
    }

    private static string GetSaveFolder()
    {
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        if (string.IsNullOrWhiteSpace(basePath))
            basePath = ".cache";

        return Path.Combine(basePath, "RevolutionaryGamesScripts");
    }
}
