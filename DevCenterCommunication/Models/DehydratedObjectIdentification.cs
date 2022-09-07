namespace DevCenterCommunication.Models;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

public class DehydratedObjectIdentification
{
    public DehydratedObjectIdentification(string sha3)
    {
        Sha3 = sha3;
    }

    [JsonPropertyName("sha3")]
    [Required]
    [MinLength(5)]
    [MaxLength(100)]
    public string Sha3 { get; set; }
}
