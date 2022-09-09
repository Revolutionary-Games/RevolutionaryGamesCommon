namespace ScriptsBase.Models;

using CommandLine;

/// <summary>
///   Base options for all script specific option classes to inherit
/// </summary>
public abstract class ScriptOptionsBase
{
    [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages")]
    public bool Verbose { get; set; }

    [Option("disable-colour", Default = false, HelpText = "Disable colour output")]
    public bool DisableColour { get; set; }
}
