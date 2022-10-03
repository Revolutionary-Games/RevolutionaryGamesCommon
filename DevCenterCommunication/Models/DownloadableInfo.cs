namespace DevCenterCommunication.Models;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

/// <summary>
///   A file with a known hash that can be downloaded from one or more mirror sites
/// </summary>
public class DownloadableInfo
{
    public DownloadableInfo(string fileSha3, string localFileName, Dictionary<string, Uri> mirrors)
    {
        FileSha3 = fileSha3;
        LocalFileName = localFileName;
        Mirrors = mirrors;
    }

    [Required]
    [StringLength(500, MinimumLength = 10)]
    public string FileSha3 { get; }

    /// <summary>
    ///   The filename to use locally after downloading this
    /// </summary>
    public string LocalFileName { get; }

    /// <summary>
    ///   List of mirrors where this can be downloaded from. The key is the mirror name identifier (which must also
    ///   exist in <see cref="LauncherThriveInformation.MirrorList"/>)
    /// </summary>
    [Required]
    [MinLength(1)]
    [MaxLength(100)]
    public Dictionary<string, Uri> Mirrors { get; }

    // For future potential use we may need to make torrent downloads of Thrive if we can't find mirrors with big
    // enough filesize limits
    [MaxLength(500)]
    public Uri? TorrentDownload { get; set; }

    [StringLength(500, MinimumLength = 10)]
    public string? TorrentSha3 { get; set; }
}
