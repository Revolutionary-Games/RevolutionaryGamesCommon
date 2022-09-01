namespace ScriptsBase.Utilities;

using System;
using System.Collections.Generic;
using System.Linq;
using CommandLine;
using Models;

public static class CommandLineHelpers
{
    public static Parser CreateParser()
    {
        return new Parser(config =>
        {
            config.AllowMultiInstance = true;
            config.AutoHelp = true;
            config.AutoVersion = true;
            config.EnableDashDash = true;
            config.IgnoreUnknownArguments = false;
            config.MaximumDisplayWidth = 80;
            config.HelpWriter = Console.Error;
        });
    }

    public static int PrintCommandLineErrors(IEnumerable<Error> errors)
    {
        var errorList = errors.ToList();

        if (errorList.Count < 1)
        {
            ColourConsole.WriteError("Unknown command line parsing errors");
            return 1;
        }

        var firstError = errorList.First();
        if (IsCommandLineErrorNotAnError(firstError))
        {
            return 0;
        }

        ColourConsole.WriteError("Invalid command line arguments specified. ");

        foreach (var error in errorList)
            Console.WriteLine(error.Tag);

        return 1;
    }

    public static void ErrorOnUnparsed(IEnumerable<Error> errors)
    {
        var errorList = errors.ToList();

        if (errorList.Count < 1)
            return;

        var firstError = errorList.First();
        if (IsCommandLineErrorNotAnError(firstError))
        {
            Environment.Exit(0);
        }

        ColourConsole.WriteError("Unknown command line arguments specified: ");

        foreach (var error in errorList)
            Console.WriteLine(error.Tag);

        Console.WriteLine();

        Environment.Exit(1);
    }

    /// <summary>
    ///   Returns true if the specified error is not actually an error (help or version info)
    /// </summary>
    /// <param name="error"></param>
    /// <returns></returns>
    public static bool IsCommandLineErrorNotAnError(Error error)
    {
        if (error.Tag is ErrorType.HelpRequestedError or ErrorType.VersionRequestedError
            or ErrorType.HelpVerbRequestedError)
        {
            return true;
        }

        return false;
    }

    public static void HandleDefaultOptions(ScriptOptionsBase baseOptions)
    {
        ColourConsole.DebugPrintingEnabled = baseOptions.Verbose;
    }
}
