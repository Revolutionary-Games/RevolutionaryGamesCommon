namespace Models;

/// <summary>
///   Configures the auto-update and store version detection of the exported launcher
/// </summary>
public enum LauncherExportType
{
    Standalone,
    WithUpdater,
    Steam,
    Itch,
    Flatpak,
}
