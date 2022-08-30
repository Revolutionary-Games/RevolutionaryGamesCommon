namespace Scripts;

using System.Collections.Generic;
using ScriptsBase.Checks;

public class CodeChecks : CodeChecksBase<Program.CheckOptions>
{
    public CodeChecks(Program.CheckOptions opts) : base(opts)
    {
    }

    // TODO: add all the checks
    protected override Dictionary<string, CodeCheck> ValidChecks { get; } = new()
    {
        { "inspectcode", new InspectCode() },
    };

    protected override string MainSolutionFile => "RevolutionaryGamesCommon.sln";
}
