namespace DevCenterCommunication.Models;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

public class DevBuildUploadResult
{
    public DevBuildUploadResult(string uploadUrl, string verifyToken)
    {
        UploadUrl = uploadUrl;
        VerifyToken = verifyToken;
    }

    [Required]
    [JsonPropertyName("upload_url")]
    public string UploadUrl { get; set; }

    [Required]
    [JsonPropertyName("verify_token")]
    public string VerifyToken { get; set; }
}
