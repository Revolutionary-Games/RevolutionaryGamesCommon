namespace SharedBase.Models;

/// <summary>
///   Platforms that Thrive or related projects can be packaged for. When adding new values only add them at the end as
///   the exact numeric values of these are relied on in saved info.
/// </summary>
public enum PackagePlatform
{
    Linux,
    Windows,
    Windows32,
    Mac,
}
