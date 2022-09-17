namespace ScriptsBase.Utilities;

using System;
using System.Text;
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
    /// <param name="customPrompt">If not null will be used instead of the normal prompt</param>
    /// <returns>True if execution should continue, false if cancellation was requested</returns>
    /// <remarks>
    ///   <para>
    ///     The cancellation token can be setup for example with <see cref="CreateSimpleConsoleCancellationSource"/>
    ///   </para>
    /// </remarks>
    public static async Task<bool> WaitForInputToContinue(CancellationToken cancellationToken,
        string? customPrompt = null)
    {
        customPrompt ??= $"{Environment.NewLine}Please press enter to continue, or CTRL+C to cancel";

        if (!string.IsNullOrEmpty(customPrompt))
            ColourConsole.WriteNormalLine(customPrompt);

        CleanConsoleStateForInput();

        if (Console.IsInputRedirected)
        {
            ColourConsole.WriteNormalLine("Input detected as redirected, trying to verify...");

            try
            {
                Console.ReadKey(false);
            }
            catch (InvalidOperationException)
            {
                ColourConsole.WriteWarningLine("Input is redirected (from file), continuing immediately");
                return true;
            }
        }

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

    public static Task<string> PromptForUserInput(string prompt, bool showInputText,
        CancellationToken cancellationToken)
    {
        ColourConsole.WriteNormal($"> {prompt}: ");

        CleanConsoleStateForInput();

        var readKeyTask =
            new Task<string>(() =>
            {
                // When input text is hidden we need to use a more advanced reading method
                if (!showInputText)
                {
                    var stringBuilder = new StringBuilder();

                    while (true)
                    {
                        // Intercept is true to not show the input text
                        try
                        {
                            var info = Console.ReadKey(true);

                            cancellationToken.ThrowIfCancellationRequested();

                            if (info.Key == ConsoleKey.Enter)
                                break;

                            if (info.Key == ConsoleKey.C && (info.Modifiers & ConsoleModifiers.Control) != 0)
                                throw new OperationCanceledException();

                            if (info.KeyChar == 0)
                            {
                                ColourConsole.WriteErrorLine($"Unsupported key was pressed (unknown text): {info.Key}");
                                continue;
                            }

                            stringBuilder.Append(info.KeyChar);
                        }
                        catch (InvalidOperationException e)
                        {
                            ColourConsole.WriteNormalLine(
                                $"Single key read is not supported, falling back to reading a line: {e}");
                            return Console.ReadLine() ?? throw new Exception("No more stdin input lines");
                        }
                    }

                    // As our prompt doesn't end with a newline write one now after reading
                    // This isn't needed in the ReadLine case as that echoes the user's newline press to the terminal
                    // already, so if we also output a newline there's a duplicate newline
                    ColourConsole.WriteNormalLine(string.Empty);
                    return stringBuilder.ToString();
                }

                return Console.ReadLine() ?? throw new Exception("No more stdin input lines");
            });

        readKeyTask.Start();

        return readKeyTask.WaitAsync(cancellationToken);
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
