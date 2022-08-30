namespace ScriptsBase.Checks;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
///   Base class for all code checks
/// </summary>
public abstract class CodeCheck
{
    public abstract Task Run(CodeCheckRun runData, CancellationToken cancellationToken);
}
