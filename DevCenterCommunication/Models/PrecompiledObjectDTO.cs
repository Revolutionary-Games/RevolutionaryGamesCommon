namespace DevCenterCommunication.Models;

using System.ComponentModel.DataAnnotations;
using SharedBase.ModelVerifiers;

public class PrecompiledObjectDTO : ClientSideTimedModel
{
    [Required]
    [StringLength(50, MinimumLength = 3)]
    [MayNotContain("/")]
    public string Name { get; set; } = null!;

    public long TotalStorageSize { get; set; }

    public bool Public { get; set; } = true;

    public bool Deleted { get; set; }
}
