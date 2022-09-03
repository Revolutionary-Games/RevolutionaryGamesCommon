using System;
using System.Diagnostics;
using CommandLine;
using Scripts;
using ScriptsBase.Models;
using ScriptsBase.Utilities;
using SharedBase.Utilities;

public class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        RunFolderChecker.EnsureRightRunningFolder("RevolutionaryGamesCommon.sln");

        // TestOptions mostly exists here to make the verb based command parsing work in the first place
        var result = CommandLineHelpers.CreateParser().ParseArguments<CheckOptions, TestOptions>(args)
            .MapResult(
                (CheckOptions options) => RunChecks(options),
                (TestOptions options) => RunTests(options),
                CommandLineHelpers.PrintCommandLineErrors);

        ConsoleHelpers.CleanConsoleStateForExit();

        return result;
    }

    private static int RunChecks(CheckOptions options)
    {
        CommandLineHelpers.HandleDefaultOptions(options);

        ColourConsole.WriteDebugLine("Running in check mode");
        ColourConsole.WriteDebugLine($"Manually specified checks: {string.Join(' ', options.Checks)}");

        var checker = new CodeChecks(options);

        return checker.Run().Result;
    }

    private static int RunTests(TestOptions options)
    {
        CommandLineHelpers.HandleDefaultOptions(options);

        ColourConsole.WriteDebugLine("Running dotnet tests");

        var tokenSource = ConsoleHelpers.CreateSimpleConsoleCancellationSource();

        return ProcessRunHelpers.RunProcessAsync(new ProcessStartInfo("dotnet", "test"), tokenSource.Token, false)
            .Result.ExitCode;
    }

    public class CheckOptions : CheckOptionsBase
    {
    }

    [Verb("test", HelpText = "Run tests using 'dotnet' command")]
    public class TestOptions : ScriptOptionsBase
    {
    }
}
