namespace ScriptsBase.Models;

using System.Collections.Generic;
using CommandLine;

[Verb("check", HelpText = "Perform code and file inspections")]
public class CheckOptionsBase : ScriptOptionsBase
{
    [Option('p', "parallel", Required = false, Default = true, HelpText = "Set to run checks in parallel")]
    public bool? Parallel { get; set; }

    [Option('r', "restore-tools", Required = false, Default = true,
        HelpText = "Automatically restores dotnet tools before running")]
    public bool RestoreTools { get; set; }

    [Option("pre-commit", Required = false, Default = false,
        HelpText = "Run in pre-commit mode (automatically build list of changes)")]
    public bool PreCommitMode { get; set; }

    [Option("include", Required = false,
        HelpText = "Include files to run on (if specified overwrites the default behaviour of running on all files)")]
    public IList<string>? Include { get; set; }

    [Option("exclude", Required = false,
        HelpText = "Exclude specified files (specify patterns as regexes)")]
    public IList<string>? Exclude { get; set; }

    [Value(0, MetaName = "Checks", HelpText = "Checks to enable (leave blank for default checks)")]
    public IList<string> Checks { get; set; } = new List<string>();
}
