namespace DevCenterCommunication.Models;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

public class DevBuildSearchForm
{
    [JsonPropertyName("platform")]
    [MaxLength(200)]
    public string? Platform { get; set; }

    [JsonPropertyName("offset")]
    [Range(0, int.MaxValue)]
    public int Offset { get; set; } = 0;

    [JsonPropertyName("page_size")]
    [Range(1, CommunicationConstants.MAX_PAGE_SIZE_FOR_BUILD_SEARCH)]
    public int PageSize { get; set; } = CommunicationConstants.MAX_PAGE_SIZE_FOR_BUILD_SEARCH;
}

public class DevBuildHashSearchForm : DevBuildSearchForm
{
    [Required]
    [JsonPropertyName("devbuild_hash")]
    [MaxLength(200)]
    public string BuildHash { get; set; } = string.Empty;
}
