namespace DevCenterCommunication.Models;

using System.Text.Json.Serialization;

public class DevBuildUploadResult
{
    public DevBuildUploadResult(string uploadUrl, string verifyToken)
    {
        UploadUrl = uploadUrl;
        VerifyToken = verifyToken;
    }

    [JsonPropertyName("upload_url")]
    public string UploadUrl { get; set; }

    [JsonPropertyName("verify_token")]
    public string VerifyToken { get; set; }
}
