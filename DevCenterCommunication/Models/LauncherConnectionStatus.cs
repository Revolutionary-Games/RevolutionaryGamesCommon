namespace DevCenterCommunication.Models;

using System.Text.Json.Serialization;

public class LauncherConnectionStatus
{
    [JsonPropertyName("valid")]
    public bool Valid { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("developer")]
    public bool Developer { get; set; }
}
