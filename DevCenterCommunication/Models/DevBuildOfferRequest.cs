namespace DevCenterCommunication.Models;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

public class DevBuildOfferRequest
{
    [Required]
    [JsonPropertyName("build_hash")]
    public string BuildHash { get; set; } = string.Empty;

    [Required]
    [JsonPropertyName("build_platform")]
    public string BuildPlatform { get; set; } = string.Empty;
}
