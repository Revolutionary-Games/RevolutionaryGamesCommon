namespace DevCenterCommunication.Models;

using System.ComponentModel.DataAnnotations;
using SharedBase.Utilities;

public class DataWithSignature
{
    public DataWithSignature(string data, string signature)
    {
        Data = data;
        Signature = signature;
    }

    [Required]
    [StringLength(GlobalConstants.MEBIBYTE * 5, MinimumLength = 1)]
    public string Data { get; set; }

    [Required]
    [MaxLength(50)]
    public string SigningMethod { get; set; } = "RSA+SHA512";

    [Required]
    [StringLength(500, MinimumLength = 10)]
    public string Signature { get; set; }
}
