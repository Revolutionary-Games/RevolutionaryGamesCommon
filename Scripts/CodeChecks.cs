namespace Scripts;

using System.Collections.Generic;
using ScriptsBase.Checks;

public class CodeChecks : CodeChecksBase<Program.CheckOptions>
{
    public CodeChecks(Program.CheckOptions opts) : base(opts)
    {
    }

    protected override Dictionary<string, CodeCheck> ValidChecks { get; } = new()
    {
        { "files", new FileChecks() },
        { "compile", new CompileCheck() },
        { "inspectcode", new InspectCode() },
        { "cleanupcode", new CleanupCode() },
    };

    protected override string MainSolutionFile => "RevolutionaryGamesCommon.sln";
}
