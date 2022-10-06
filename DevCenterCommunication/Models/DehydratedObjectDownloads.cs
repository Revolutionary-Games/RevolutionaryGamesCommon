namespace DevCenterCommunication.Models;

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

public class DehydratedObjectDownloads
{
    [Required]
    [JsonPropertyName("downloads")]
    public List<DehydratedObjectDownload> Downloads { get; set; } = new();

    public class DehydratedObjectDownload : DehydratedObjectIdentification
    {
        public DehydratedObjectDownload(string sha3, string downloadUrl) : base(sha3)
        {
            DownloadUrl = downloadUrl;
        }

        [Required]
        [JsonPropertyName("download_url")]
        public string DownloadUrl { get; set; }
    }
}
