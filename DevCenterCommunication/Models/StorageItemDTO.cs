namespace DevCenterCommunication.Models;

using System.ComponentModel.DataAnnotations;
using Enums;

public class StorageItemDTO : ClientSideTimedModel
{
    [Required]
    public string Name { get; set; } = string.Empty;

    // TODO: rename this to "FileType"
    // ReSharper disable once IdentifierTypo
    public FileType Ftype { get; set; }
    public bool Special { get; set; }
    public long? Size { get; set; }
    public FileAccess ReadAccess { get; set; }
    public FileAccess WriteAccess { get; set; }
    public long? OwnerId { get; set; }
    public long? ParentId { get; set; }
    public bool AllowParentless { get; set; }
}

public class StorageItemInfo : ClientSideModel
{
    [Required]
    public string Name { get; set; } = string.Empty;

    // TODO: rename this to "FileType"
    // ReSharper disable once IdentifierTypo
    public FileType Ftype { get; set; }
    public long? Size { get; set; }
    public FileAccess ReadAccess { get; set; }
}
