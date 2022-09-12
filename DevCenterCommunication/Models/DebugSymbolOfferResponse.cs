namespace DevCenterCommunication.Models;

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

public class DebugSymbolOfferResponse
{
    [Required]
    [MaxLength(CommunicationConstants.MAX_DEBUG_SYMBOL_OFFER_BATCH)]
    public List<string> Upload { get; set; } = new();
}
