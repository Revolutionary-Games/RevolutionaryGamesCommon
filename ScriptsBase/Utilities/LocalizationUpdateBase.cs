namespace ScriptsBase.Utilities;

using System;
using System.Threading;
using System.Threading.Tasks;
using Models;

/// <summary>
///   Base class for handling updating localization files
/// </summary>
/// <typeparam name="T">The type of the options class</typeparam>
public abstract class LocalizationUpdateBase<T>
    where T : LocalizationOptionsBase
{
    private readonly T opts;

    protected LocalizationUpdateBase(T opts)
    {
        this.opts = opts;
    }

    public async Task<bool> Run(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
