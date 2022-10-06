namespace DevCenterCommunication.Models;

using System.Text.Json.Serialization;

public class LauncherUnlinkResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }
}
