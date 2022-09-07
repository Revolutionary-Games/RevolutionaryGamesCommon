namespace DevCenterCommunication.Models;

using System.Collections.Generic;
using System.Text.Json.Serialization;

public class DevObjectOfferResult
{
    /// <summary>
    ///   The SHA3s of objects the server wants
    /// </summary>
    [JsonPropertyName("upload")]
    public List<string> Upload { get; set; } = new();
}
