namespace DevCenterCommunication.Models;

using System.Text.Json.Serialization;

public class DevBuildOfferResult
{
    [JsonPropertyName("upload")]
    public bool Upload { get; set; }
}
