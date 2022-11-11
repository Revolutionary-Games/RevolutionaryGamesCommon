namespace ScriptsBase.Models;

using System.Collections.Generic;
using CommandLine;
using SharedBase.Models;

[Verb("package", HelpText = "Package the project for distribution")]
public abstract class PackageOptionsBase : ScriptOptionsBase
{
    [Option('o', "output", Default = "builds", MetaValue = "FOLDER", HelpText = "Output folder to package to")]
    public string OutputFolder { get; set; } = "builds";

    [Option("output-without-subfolders", Default = false,
        HelpText = "If specified, output will be directly in the output folder without any subfolders")]
    public bool OutputDirectlyToOutputFolder { get; set; }

    [Option('s', "source", Default = null, HelpText = "Include source code in export")]
    public bool? SourceCode { get; set; }

    [Option('r', "retries", Default = 2, MetaValue = "RETRIES",
        HelpText = "How many times to retry export if it fails")]
    public int Retries { get; set; }

    [Option("clean-zip", Default = false, HelpText = "Delete package zips before writing them again")]
    public bool CleanZips { get; set; }

    [Value(0, MetaName = "PLATFORMS", HelpText = "Platforms to package for (leave blank for default)")]
    public IList<PackagePlatform> Platforms { get; set; } = new List<PackagePlatform>();

    public abstract bool Compress { get; }
}
