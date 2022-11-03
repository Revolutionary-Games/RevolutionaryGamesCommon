namespace DevCenterCommunication.Models;

using System;

public static class LauncherAutoUpdateChannelExtensions
{
    /// <summary>
    ///   The local filename to use for a download of this type of update
    /// </summary>
    /// <param name="channel">The channel type to get this info for</param>
    /// <returns>Local filename</returns>
    public static string DownloadFilename(this LauncherAutoUpdateChannel channel)
    {
        switch (channel)
        {
            case LauncherAutoUpdateChannel.LinuxUnpacked:
                return "ThriveLauncher.tar.gz";
            case LauncherAutoUpdateChannel.WindowsInstaller:
                return "ThriveLauncher.exe";
            case LauncherAutoUpdateChannel.MacDmg:
                return "ThriveLauncher.dmg";
            default:
                throw new ArgumentOutOfRangeException(nameof(channel), channel, null);
        }
    }
}
