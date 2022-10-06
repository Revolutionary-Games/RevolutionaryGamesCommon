namespace DevCenterCommunication.Models;

using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using SharedBase.Converters;

public class DevBuildFindByTypeForm
{
    [JsonConverter(typeof(ActualEnumStringConverter))]
    public enum BuildType
    {
        [EnumMember(Value = "botd")]
        BuildOfTheDay,

        [EnumMember(Value = "latest")]
        Latest,
    }

    [JsonPropertyName("type")]
    [Required]
    public BuildType Type { get; set; }

    [JsonPropertyName("platform")]
    [MaxLength(200)]
    [Required]
    public string Platform { get; set; } = string.Empty;
}
