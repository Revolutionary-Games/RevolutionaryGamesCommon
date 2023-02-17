namespace DevCenterCommunication.Models;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using SharedBase.ModelVerifiers;

public class LauncherVersionInfo
{
    public LauncherVersionInfo(string latestVersion)
    {
        LatestVersion = latestVersion;
    }

    [Required]
    [StringLength(30, MinimumLength = 3)]
    public string LatestVersion { get; }

    /// <summary>
    ///   The time when the <see cref="LatestVersion"/> was set as the latest version
    /// </summary>
    public DateTime? LatestVersionPublishedAt { get; set; }

    [Required]
    [ToStringMaxLength]
    public Uri DownloadsPage { get; set; } = new("https://revolutionarygamesstudio.com/");

    // Created from JSON data
    // ReSharper disable once CollectionNeverUpdated.Global
    [Required]
    [MaxLength(50)]
    public Dictionary<LauncherAutoUpdateChannel, DownloadableInfo> AutoUpdateDownloads { get; set; } = new();
}
