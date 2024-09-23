namespace Scripts;

using System.Collections.Generic;
using ScriptsBase.Checks;

public class CodeChecks : CodeChecksBase<Program.CheckOptions>
{
    public CodeChecks(Program.CheckOptions opts) : base(opts)
    {
        ValidChecks = new Dictionary<string, CodeCheck>
        {
            { "files", new FileChecks() },
            { "compile", new CompileCheck(!options.NoExtraRebuild) },
            { "inspectcode", new InspectCode() },
            { "cleanupcode", new CleanupCode() },
            { "rewrite", new RewriteTool() },
        };
    }

    protected override Dictionary<string, CodeCheck> ValidChecks { get; }

    protected override string MainSolutionFile => "RevolutionaryGamesCommon.sln";
}
