namespace ScriptsBase.Models;

using CommandLine;

/// <summary>
///   Base options type for all scripts that access the DevCenter
/// </summary>
public class DevCenterAccessingOptionsBase : ScriptOptionsBase
{
    public const string DEFAULT_DEVCENTER_URL = "https://dev.revolutionarygamesstudio.com";
    public const int DEFAULT_PARALLEL_UPLOADS = 3;

    [Option('k', "key", Required = true, MetaValue = "KEY",
        HelpText = "Set to a DevCenter user API token to use for upload")]
    public string? Key { get; set; }

    [Option("devcenter-url", Required = false, Default = DEFAULT_DEVCENTER_URL,
        MetaValue = "DEVCENTER_URL", HelpText = "DevCenter URL to upload to.")]
    public string Url { get; set; } = DEFAULT_DEVCENTER_URL;
}
