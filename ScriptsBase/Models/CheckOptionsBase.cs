namespace ScriptsBase.Models;

using System.Collections.Generic;
using CommandLine;

[Verb("check", HelpText = "Perform code and file inspections")]
public class CheckOptionsBase : ScriptOptionsBase
{
    [Option('p', "parallel", Required = false, Default = true, HelpText = "Set to run checks in parallel")]
    public bool Parallel { get; set; }

    [Option('r', "restore-tools", Required = false, Default = true,
        HelpText = "Automatically restores dotnet tools before running")]
    public bool RestoreTools { get; set; }

    [Value(0, MetaName = "Checks", HelpText = "Checks to enable (leave blank for default checks)")]
    public IList<string> Checks { get; set; } = new List<string>();
}
