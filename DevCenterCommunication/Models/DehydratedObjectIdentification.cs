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

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj))
            return false;
        if (ReferenceEquals(this, obj))
            return true;
        if (obj.GetType() != GetType())
            return false;

        return Equals((DehydratedObjectIdentification)obj);
    }

    public override int GetHashCode()
    {
        return Sha3.GetHashCode();
    }

    public override string ToString()
    {
        return $"sha3:{Sha3}";
    }

    protected bool Equals(DehydratedObjectIdentification other)
    {
        return Sha3 == other.Sha3;
    }
}
