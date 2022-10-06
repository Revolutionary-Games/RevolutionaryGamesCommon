namespace DevCenterCommunication.Models;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

public class LauncherLinkResult
{
    public LauncherLinkResult(bool connected, string code)
    {
        Connected = connected;
        Code = code;
    }

    [JsonPropertyName("connected")]
    public bool Connected { get; set; }

    [Required]
    [JsonPropertyName("code")]
    public string Code { get; set; }
}
