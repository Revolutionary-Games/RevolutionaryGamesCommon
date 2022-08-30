using System;
using CommandLine;
using ScriptsBase.Models;
using ScriptsBase.Utilities;

internal class Program
{

    private class CheckOptions : CheckOptionsBase
    {
    }

    [STAThread]
    public static int Main(string[] args)
    {
        var result = CommandLineHelpers.CreateParser().ParseArguments<CheckOptions>(args)
            .MapResult(
                RunChecks,
                CommandLineHelpers.PrintCommandLineErrors);

        ConsoleHelpers.CleanConsoleStateForExit();

        return result;
    }

    private static int RunChecks(CheckOptions opts)
    {
        CommandLineHelpers.HandleDefaultOptions(opts);

        ColourConsole.WriteDebugLine("Running in check mode");


        ColourConsole.WriteDebugLine("Exiting with success code");
        return 0;
    }
}
