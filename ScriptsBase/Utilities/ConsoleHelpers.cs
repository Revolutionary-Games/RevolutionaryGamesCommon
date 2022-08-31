namespace ScriptsBase.Utilities;

using System;
using System.Threading;

public static class ConsoleHelpers
{
    public static void CleanConsoleStateForExit()
    {
        Console.ResetColor();
    }

    public static void ExitWithError(string message)
    {
        ColourConsole.WriteErrorLine(message);
        CleanConsoleStateForExit();
        Environment.Exit(1);
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
