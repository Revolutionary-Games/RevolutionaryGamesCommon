namespace DevCenterCommunication.Models.Enums;

public enum FileAccess
{
    Public = 0,
    RestrictedUser = 1,
    User = 2,
    Developer,
    OwnerOrAdmin,

    /// <summary>
    ///   Only system access
    /// </summary>
    Nobody,
}
