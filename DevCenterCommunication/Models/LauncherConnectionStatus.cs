namespace DevCenterCommunication.Models;

using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

public class LauncherConnectionStatus
{
    [JsonConstructor]
    public LauncherConnectionStatus(string username, string email, bool developer)
    {
        Username = username;
        Email = email;
        Developer = developer;
        Valid = true;
    }

    public LauncherConnectionStatus(bool notValid)
    {
        if (notValid)
            throw new ArgumentException("Valid needs to be false");

        Valid = false;
        Username = "error";
        Email = "error";
    }

    [JsonPropertyName("valid")]
    public bool Valid { get; set; }

    [Required]
    [JsonPropertyName("username")]
    public string Username { get; set; }

    [Required]
    [JsonPropertyName("email")]
    public string Email { get; set; }

    [JsonPropertyName("developer")]
    public bool Developer { get; set; }
}
