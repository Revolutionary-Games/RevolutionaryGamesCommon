namespace DevCenterCommunication.Models;

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

public class DebugSymbolOfferRequest
{
    [Required]
    [MaxLength(CommunicationConstants.MAX_DEBUG_SYMBOL_OFFER_BATCH)]
    [MinLength(1)]
    public List<string> SymbolPaths { get; set; } = new();
}
