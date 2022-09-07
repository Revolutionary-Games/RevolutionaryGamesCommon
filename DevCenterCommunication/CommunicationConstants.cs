namespace DevCenterCommunication;

using SharedBase.Utilities;

/// <summary>
///   Constants shared between the DevCenter and other code communicating with it (excluding the web frontend)
/// </summary>
public class CommunicationConstants
{
    public const int MAX_DEHYDRATED_OBJECTS_PER_OFFER = 100;
    public const int MAX_DEHYDRATED_OBJECTS_IN_DEV_BUILD = 5000;
    public const int MAX_PAGE_SIZE_FOR_BUILD_SEARCH = 100;
    public const int MAX_DEHYDRATED_DOWNLOAD_BATCH = 100;

    /// <summary>
    ///   Maximum size of a dehydrated file
    /// </summary>
    public const long MAX_DEHYDRATED_UPLOAD_SIZE = 200 * GlobalConstants.MEBIBYTE;

    /// <summary>
    ///   Maximum size of a devbuild file
    /// </summary>
    public const long MAX_DEV_BUILD_UPLOAD_SIZE = 50 * GlobalConstants.MEBIBYTE;
}
