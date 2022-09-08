namespace ScriptsBase.Models;

using CommandLine;

[Verb("localization", HelpText = "Update localization files")]
public class LocalizationOptionsBase : ScriptOptionsBase
{
    [Option("pot-suffix", Default = null, HelpText = "Override the default .pot file suffix")]
    public string? PotSuffix { get; set; }

    [Option("po-suffix", Default = null, HelpText = "Override the default .po file suffix")]
    public string? PoSuffix { get; set; }

    [Option('q', "quiet", Default = false, HelpText = "Disable showing translation output")]
    public bool Quiet { get; set; }
}
