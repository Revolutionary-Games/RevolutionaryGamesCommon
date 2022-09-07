namespace DevCenterCommunication.Models;

using System.Collections.Generic;
using System.Text.Json.Serialization;

public class DehydratedUploadResult
{
    [JsonPropertyName("upload")]
    public List<ObjectToUpload> Upload { get; set; } = new();

    public class ObjectToUpload
    {
        public ObjectToUpload(string sha3, string uploadUrl, string verifyToken)
        {
            Sha3 = sha3;
            UploadUrl = uploadUrl;
            VerifyToken = verifyToken;
        }

        [JsonPropertyName("sha3")]
        public string Sha3 { get; set; }

        [JsonPropertyName("upload_url")]
        public string UploadUrl { get; set; }

        [JsonPropertyName("verify_token")]
        public string VerifyToken { get; set; }
    }
}
