namespace ScriptsBase.Utilities;

using System;
using System.Threading;
using System.Threading.Tasks;

public static class ConsoleHelpers
{
    public static void CleanConsoleStateForExit()
    {
        CleanConsoleStateForInput();
    }

    public static void CleanConsoleStateForInput()
    {
        ColourConsole.ResetColour();
    }

    public static void ExitWithError(string message)
    {
        ColourConsole.WriteErrorLine(message);
        CleanConsoleStateForExit();
        Environment.Exit(1);
    }

    /// <summary>
    ///   Waits until a new line of input is available
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that should become canceled if CTRL-C is pressed</param>
    /// <returns>True if execution should continue, false if cancellation was requested</returns>
    /// <remarks>
    ///   <para>
    ///     The cancellation token can be setup for example with <see cref="CreateSimpleConsoleCancellationSource"/>
    ///   </para>
    /// </remarks>
    public static async Task<bool> WaitForInputToContinue(CancellationToken cancellationToken)
    {
        CleanConsoleStateForInput();

        var readKeyTask = new Task(() => _ = Console.ReadLine());

        readKeyTask.Start();

        try
        {
            await readKeyTask.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }

        if (cancellationToken.IsCancellationRequested)
            return false;

        return true;
    }

    public static CancellationTokenSource CreateSimpleConsoleCancellationSource()
    {
        var tokenSource = new CancellationTokenSource();

        Console.CancelKeyPress += (_, args) =>
        {
            // Only prevent CTRL-C working once
            if (tokenSource.IsCancellationRequested)
                return;

            ColourConsole.WriteNormalLine("Cancel request detected");
            tokenSource.Cancel();
            args.Cancel = true;
        };

        return tokenSource;
    }
}
