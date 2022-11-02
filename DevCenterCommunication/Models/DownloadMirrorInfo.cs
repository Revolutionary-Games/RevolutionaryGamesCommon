namespace DevCenterCommunication.Models;

using System;
using System.ComponentModel.DataAnnotations;
using SharedBase.ModelVerifiers;

/// <summary>
///   Info about a download mirror
/// </summary>
public class DownloadMirrorInfo
{
    public DownloadMirrorInfo(Uri infoLink, string readableName)
    {
        InfoLink = infoLink;
        ReadableName = readableName;
    }

    [ToStringMaxLength]
    public Uri? BannerImage { get; set; }

    [Required]
    [ToStringMaxLength]
    public Uri InfoLink { get; }

    [Required]
    [StringLength(60, MinimumLength = 2)]
    public string ReadableName { get; }

    /// <summary>
    ///   Extra description to show instead of the generic text
    /// </summary>
    [MaxLength(250)]
    public string? ExtraDescription { get; set; }
}
