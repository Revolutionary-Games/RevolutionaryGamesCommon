namespace DevCenterCommunication.Models;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

/// <summary>
///   The overall info the launcher downloads and uses to show Thrive versions etc.
/// </summary>
public class LauncherThriveInformation
{
    public LauncherThriveInformation(LauncherVersionInfo launcherVersion, int latestStable,
        List<ThriveVersionLauncherInfo> versions, Dictionary<string, DownloadMirrorInfo> mirrorList)
    {
        LauncherVersion = launcherVersion;
        LatestStable = latestStable;
        Versions = versions;
        MirrorList = mirrorList;
    }

    public LauncherVersionInfo LauncherVersion { get; set; }

    public int LatestStable { get; set; }

    /// <summary>
    ///   If set specifies the latest public beta version
    /// </summary>
    public int? LatestUnstable { get; set; }

    [Required]
    [MinLength(1)]
    [MaxLength(100)]
    public Dictionary<string, DownloadMirrorInfo> MirrorList { get; set; }

    [Required]
    [MinLength(1)]
    public List<ThriveVersionLauncherInfo> Versions { get; set; }

    public ThriveVersionLauncherInfo? FindVersionById(int id)
    {
        return Versions.FirstOrDefault(v => v.Id == id);
    }

    public bool IsLatest(ThriveVersionLauncherInfo version)
    {
        return LatestStable == version.Id;
    }

    public ThriveVersionLauncherInfo LatestVersion()
    {
        return LatestVersionOrNull() ?? throw new InvalidOperationException("No latest version found");
    }

    public ThriveVersionLauncherInfo? LatestVersionOrNull()
    {
        return Versions.FirstOrDefault(v => v.Id == LatestStable);
    }
}
