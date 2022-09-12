namespace DevCenterCommunication.Models;

using System.ComponentModel.DataAnnotations;

public class DebugSymbolUploadResult
{
    [Required]
    public string UploadUrl { get; set; } = string.Empty;

    [Required]
    [MaxLength(CommunicationConstants.MAXIMUM_TOKEN_LENGTH)]
    public string VerifyToken { get; set; } = string.Empty;
}
