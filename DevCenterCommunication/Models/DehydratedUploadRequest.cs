namespace DevCenterCommunication.Models;

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

public class DehydratedUploadRequest
{
    [Required]
    [MaxLength(CommunicationConstants.MAX_DEHYDRATED_OBJECTS_PER_OFFER)]
    public List<DehydratedObjectRequest> Objects { get; set; } = new();
}
