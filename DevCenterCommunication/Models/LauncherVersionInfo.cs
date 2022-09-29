namespace DevCenterCommunication.Models;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

public class LauncherVersionInfo
{
    public LauncherVersionInfo(string latestVersion)
    {
        LatestVersion = latestVersion;
    }

    [Required]
    [StringLength(30, MinimumLength = 3)]
    public string LatestVersion { get; }

    [Required]
    [MaxLength(300)]
    public Uri DownloadsPage { get; set; } = new("https://github.com/Revolutionary-Games/Thrive-Launcher/releases");

    // TODO: put in creating this info
    [Required]
    [MaxLength(50)]
    public Dictionary<string, DownloadableInfo> AutoUpdateDownloads { get; set; } = new();
}
