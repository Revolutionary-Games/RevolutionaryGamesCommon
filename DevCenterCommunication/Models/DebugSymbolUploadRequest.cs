namespace DevCenterCommunication.Models;

using System.ComponentModel.DataAnnotations;

public class DebugSymbolUploadRequest
{
    [Required]
    [StringLength(120, MinimumLength = 3)]
    public string SymbolPath { get; set; } = string.Empty;

    [Range(1, CommunicationConstants.MAX_DEBUG_SYMBOL_SIZE)]
    public long Size { get; set; }
}
