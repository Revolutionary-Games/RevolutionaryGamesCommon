namespace SharedBase.Utilities;

using System;
using Models;

public static class PlatformUtilities
{
    /// <summary>
    ///   Gets the current platform we run on
    /// </summary>
    /// <returns>The current platform</returns>
    public static PackagePlatform GetCurrentPlatform()
    {
        if (OperatingSystem.IsLinux())
            return PackagePlatform.Linux;

        if (OperatingSystem.IsWindows())
        {
            if (Environment.Is64BitOperatingSystem)
                return PackagePlatform.Windows;

            return PackagePlatform.Windows32;
        }

        if (OperatingSystem.IsMacOS())
            return PackagePlatform.Mac;

        throw new NotSupportedException("Unknown OS to get current platform for");
    }
}
