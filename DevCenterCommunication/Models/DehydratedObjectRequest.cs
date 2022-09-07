namespace DevCenterCommunication.Models;

using System.ComponentModel.DataAnnotations;

public class DehydratedObjectRequest : DehydratedObjectIdentification
{
    public DehydratedObjectRequest(string sha3, int size) : base(sha3)
    {
        Size = size;
    }

    [Required]
    [Range(1, CommunicationConstants.MAX_DEHYDRATED_UPLOAD_SIZE)]
    public int Size { get; set; }
}
