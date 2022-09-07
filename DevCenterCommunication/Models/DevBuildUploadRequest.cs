namespace DevCenterCommunication.Models;

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

public class DevBuildUploadRequest
{
    [Required]
    [JsonPropertyName("build_hash")]
    [MinLength(5)]
    [MaxLength(100)]
    public string BuildHash { get; set; } = string.Empty;

    [Required]
    [JsonPropertyName("build_branch")]
    [MinLength(2)]
    [MaxLength(100)]
    public string BuildBranch { get; set; } = string.Empty;

    [Required]
    [JsonPropertyName("build_platform")]
    [MaxLength(255)]
    public string BuildPlatform { get; set; } = string.Empty;

    [Required]
    [JsonPropertyName("build_size")]
    [Range(1, CommunicationConstants.MAX_DEV_BUILD_UPLOAD_SIZE)]
    public int BuildSize { get; set; }

    [Required]
    [JsonPropertyName("build_zip_hash")]
    [MinLength(2)]
    [MaxLength(100)]
    public string BuildZipHash { get; set; } = string.Empty;

    [Required]
    [JsonPropertyName("required_objects")]
    [MaxLength(CommunicationConstants.MAX_DEHYDRATED_OBJECTS_IN_DEV_BUILD)]
    public List<string> RequiredDehydratedObjects { get; set; } = new();
}
