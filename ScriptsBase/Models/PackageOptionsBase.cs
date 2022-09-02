namespace ScriptsBase.Models;

using System.Collections.Generic;
using CommandLine;

[Verb("package", HelpText = "Package the project for distribution")]
public class PackageOptionsBase : ScriptOptionsBase
{
    [Option('o', "output", Default = "builds", HelpText = "Output folder to package to")]
    public string OutputFolder { get; set; } = "builds";

    [Value(0, MetaName = "Platforms", HelpText = "Platforms to package for (leave blank for default)")]
    public IList<string> Platforms { get; set; } = new List<string>();
}
