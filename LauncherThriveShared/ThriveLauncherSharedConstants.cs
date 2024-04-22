namespace LauncherThriveShared;

/// <summary>
///   Constants that are shared between the Thrive Launcher and Thrive itself
/// </summary>
public static class ThriveLauncherSharedConstants
{
    public const string DISABLE_VIDEOS_LAUNCH_OPTION = "--thrive-disable-videos";
    public const string OPENED_THROUGH_LAUNCHER_OPTION = "--thrive-started-by-launcher";
    public const string OPENING_LAUNCHER_IS_HIDDEN = "--thrive-launcher-hidden";
    public const string THRIVE_LAUNCHER_STORE_PREFIX = "--thrive-store=";
    public const string THRIVE_LAUNCH_ID_PREFIX = "--thrive-launch-id=";

    public const string SKIP_CPU_CHECK_OPTION = "--skip-cpu-check";
    public const string DISABLE_CPU_AVX_OPTION = "--disable-avx";

    public const string STARTUP_SUCCEEDED_MESSAGE = "------------ Thrive Startup Succeeded ------------";
    public const string USER_REQUESTED_QUIT = "User requested program exit, Thrive will close shortly";
    public const string REQUEST_LAUNCHER_OPEN = "------------ SHOWING LAUNCHER REQUESTED ------------";

    public const string LOGS_FOLDER_NAME = "logs";
    public const string LATEST_START_INFO_FILE_NAME = "latest_start.json";
}
