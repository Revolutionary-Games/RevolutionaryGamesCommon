namespace ScriptsBase.Models;

using CommandLine;

[Verb("container", HelpText = "Tool for creating container images for this project")]
public class ContainerOptionsBase : ScriptOptionsBase
{
    [Option('l', "latest", Required = false, Default = true, HelpText = "Tag resulting build as latest")]
    public bool? Latest { get; set; }

    [Option("tag", Required = false, Default = true, HelpText = "Set to tag the built image")]
    public bool? Tag { get; set; }

    [Option('e', "export", Required = false, Default = true, HelpText = "Export the built image as a tar.xz")]
    public bool? Export { get; set; }

    [Value(0, MetaName = "Version", Default = "latest", HelpText = "Version to build/tag as")]
    public string Version { get; set; } = string.Empty;
}
