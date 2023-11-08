namespace DevCenterCommunication.Models;

using System.ComponentModel.DataAnnotations;

/// <summary>
///   Response from the server that it wants the client ot upload the resource it offered to the given upload URL and
///   then use the verify token to report completion of the upload.
/// </summary>
public class UploadRequestResponse
{
    [Required]
    public string UploadUrl { get; set; } = string.Empty;

    [Required]
    [MaxLength(CommunicationConstants.MAXIMUM_TOKEN_LENGTH)]
    public string VerifyToken { get; set; } = string.Empty;
}
