namespace DevCenterCommunication.Models;

using System.ComponentModel.DataAnnotations;

public class LauncherLinkCodeCheckForm
{
    public LauncherLinkCodeCheckForm(string code)
    {
        Code = code;
    }

    [Required]
    [MaxLength(200)]
    public string Code { get; }
}
