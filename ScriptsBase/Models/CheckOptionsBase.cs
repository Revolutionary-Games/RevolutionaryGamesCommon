namespace ScriptsBase.Models;

using System.Collections.Generic;
using CommandLine;

[Verb("check", HelpText = "Perform code and file inspections")]
public class CheckOptionsBase : ScriptOptionsBase
{
    [Value(0, MetaName = "Checks", HelpText = "Checks to enable (leave blank for default checks)")]
    public IList<string>? Checks { get; set; }
}
