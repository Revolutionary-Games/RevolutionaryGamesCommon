namespace DevCenterCommunication.Models;

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

public class DevBuildSearchResults
{
    [Required]
    [JsonPropertyName("result")]
    public List<DevBuildLauncherDTO> Result { get; set; } = new();

    [JsonPropertyName("next_offset")]
    public int? NextOffset { get; set; }
}
