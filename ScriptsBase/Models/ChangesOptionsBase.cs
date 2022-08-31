namespace ScriptsBase.Models;

using CommandLine;

/// <summary>
///   Base options for all options classes for building lists of changed files
/// </summary>
/// <remarks>
///   <para>
///     This doesn't specify the OriginBranch as the default name would be incorrect for some projects
///   </para>
/// </remarks>
[Verb("changes", HelpText = "Create a list of changed files to run checks faster")]
public abstract class ChangesOptionsBase : ScriptOptionsBase
{
    [Option('r', "remote", Required = false, Default = "origin",
        HelpText = "The git remote to compare against")]
    public string Remote { get; set; } = "origin";

    public abstract string RemoteBranch { get; set; }
}
