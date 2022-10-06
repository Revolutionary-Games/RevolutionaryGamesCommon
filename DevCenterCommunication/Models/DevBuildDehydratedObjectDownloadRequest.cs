namespace DevCenterCommunication.Models;

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

public class DevBuildDehydratedObjectDownloadRequest
{
    [Required]
    [JsonPropertyName("objects")]
    [MaxLength(CommunicationConstants.MAX_DEHYDRATED_DOWNLOAD_BATCH)]
    public List<DehydratedObjectIdentification> Objects { get; set; } = new();
}
