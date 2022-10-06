namespace DevCenterCommunication.Models;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

public class DevBuildDownload
{
    public DevBuildDownload(string downloadUrl, string downloadHash)
    {
        DownloadUrl = downloadUrl;
        DownloadHash = downloadHash;
    }

    [Required]
    [JsonPropertyName("download_url")]
    public string DownloadUrl { get; set; }

    [Required]
    [JsonPropertyName("dl_hash")]
    public string DownloadHash { get; set; }
}
