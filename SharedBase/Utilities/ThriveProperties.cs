namespace SharedBase.Utilities;

using System;
using System.IO;
using global::Models;
using Models;

/// <summary>
///   Properties about Thrive that are needed by multiple project's scripts (and other tools)
/// </summary>
public static class ThriveProperties
{
    public static string GetFolderNameForPlatform(PackagePlatform platform, string thriveVersion, bool steamMode)
    {
        string suffix = string.Empty;

        if (steamMode)
        {
            suffix = "_steam";
        }

        var platformName = GetBasePlatformPartOfFolderName(platform);

        return $"Thrive_{thriveVersion}_{platformName}{suffix}";
    }

    public static string GetBasePlatformPartOfFolderName(PackagePlatform platform)
    {
        switch (platform)
        {
            case PackagePlatform.Linux:
                return "linux_x11";
            case PackagePlatform.Windows:
                return "windows_desktop";
            case PackagePlatform.Windows32:
                return "windows_desktop_(32-bit)";
            case PackagePlatform.Mac:
                return "mac_osx";
            default:
                throw new ArgumentOutOfRangeException(nameof(platform), platform, null);
        }
    }

    public static string GetFolderNameForLauncher(PackagePlatform platform, string launcherVersion,
        LauncherExportType exportType)
    {
        var platformName = GetBasePlatformFolderNameForLauncher(platform);

        var typeSuffix = string.Empty;

        switch (exportType)
        {
            case LauncherExportType.Standalone:
                break;
            case LauncherExportType.WithUpdater:
                typeSuffix = "_updateable";
                break;
            case LauncherExportType.Steam:
                typeSuffix = "_steam";
                break;
            case LauncherExportType.Itch:
                typeSuffix = "_itch";
                break;
            case LauncherExportType.Flatpak:
                typeSuffix = "_flatpak";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(exportType), exportType, null);
        }

        return $"ThriveLauncher_{launcherVersion}_{platformName}{typeSuffix}";
    }

    public static string GetBasePlatformFolderNameForLauncher(PackagePlatform platform)
    {
        switch (platform)
        {
            case PackagePlatform.Linux:
                return "linux";
            case PackagePlatform.Windows:
                return "windows";
            case PackagePlatform.Windows32:
                return "windows_32-bit";
            case PackagePlatform.Mac:
                return "mac";
            default:
                throw new ArgumentOutOfRangeException(nameof(platform), platform, null);
        }
    }

    public static string GetThriveExecutableName(PackagePlatform platform)
    {
        switch (platform)
        {
            case PackagePlatform.Mac:
            case PackagePlatform.Linux:
                return "Thrive";
            case PackagePlatform.Windows:
            case PackagePlatform.Windows32:
                return "Thrive.exe";
            default:
                throw new ArgumentOutOfRangeException(nameof(platform), platform, null);
        }
    }

    public static string GetGodotTemplateInstallPath(string godotVersionFull)
    {
        if (!OperatingSystem.IsLinux())
            throw new NotImplementedException("Currently only implemented for Linux");

        return Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            $".local/share/godot/templates/{godotVersionFull}");
    }
}
