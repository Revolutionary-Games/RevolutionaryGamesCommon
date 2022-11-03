namespace DevCenterCommunication.Models;

/// <summary>
///   The channel auto update is for in the launcher. The launcher knows which channel it should check to get the same
///   exact variant as itself is for auto update, so that the auto update doesn't install a different version.
/// </summary>
/// <remarks>
///   <para>
///     This is currently limited to 256 values due to LauncherVersionAutoUpdateChannelDTO assuming this only takes 8
///     bits.
///   </para>
///   <para>
///     When new values are added they need to be handled in <see cref="LauncherAutoUpdateChannelExtensions"/>
///   </para>
/// </remarks>
public enum LauncherAutoUpdateChannel
{
    LinuxUnpacked,
    WindowsInstaller,
    MacDmg,
}
