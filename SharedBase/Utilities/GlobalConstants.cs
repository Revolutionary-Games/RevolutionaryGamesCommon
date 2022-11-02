namespace SharedBase.Utilities;

/// <summary>
///   Global constants used by multiple Revolutionary Games projects
/// </summary>
public static class GlobalConstants
{
    public const int MinEmailLength = 3;
    public const int MaxEmailLength = 250;

    public const int KIBIBYTE = 1024;
    public const int MEBIBYTE = KIBIBYTE * KIBIBYTE;
    public const int GIBIBYTE = MEBIBYTE * 1024;

    public const int DEFAULT_MAX_LENGTH_FOR_TO_STRING_ATTRIBUTE = 500;
}
