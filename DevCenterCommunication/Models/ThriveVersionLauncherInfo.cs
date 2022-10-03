namespace DevCenterCommunication.Models;

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using SharedBase.Models;

/// <summary>
///   Info about a Thrive version for the launcher
/// </summary>
public class ThriveVersionLauncherInfo
{
    public ThriveVersionLauncherInfo(int id, string releaseNumber,
        Dictionary<PackagePlatform, DownloadableInfo> platforms)
    {
        Id = id;
        ReleaseNumber = releaseNumber;
        Platforms = platforms;
    }

    /// <summary>
    ///   Internal unique ID of the release for use in the launcher
    /// </summary>
    [Range(1, int.MaxValue - 1)]
    public int Id { get; }

    /// <summary>
    ///   The release version (with an optional -beta suffix)
    /// </summary>
    [Required]
    [StringLength(30, MinimumLength = 3)]
    public string ReleaseNumber { get; }

    /// <summary>
    ///   The platforms and the downloads for those platforms that this version is available for
    /// </summary>
    [MinLength(1)]
    [MaxLength(50)]
    public Dictionary<PackagePlatform, DownloadableInfo> Platforms { get; }
}
