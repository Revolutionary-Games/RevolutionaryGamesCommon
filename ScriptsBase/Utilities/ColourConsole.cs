namespace ScriptsBase.Utilities;

using System;

/// <summary>
///   Writes console messages with colours
/// </summary>
public static class ColourConsole
{
    /// <summary>
    ///   Enables or disables the debug write methods in this class.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     This is not really the cleanest but probably fine enough to not use ILogger system in our command line
    ///     tools
    ///   </para>
    /// </remarks>
    public static bool DebugPrintingEnabled { get; set; }

    public static void WriteErrorLine(string message)
    {
        WriteLineWithColour(message, ConsoleColor.Red);
    }

    public static void WriteError(string message)
    {
        WriteWithColour(message, ConsoleColor.Red);
    }

    public static void WriteWarningLine(string message)
    {
        WriteLineWithColour(message, ConsoleColor.DarkYellow);
    }

    public static void WriteWarning(string message)
    {
        WriteWithColour(message, ConsoleColor.DarkYellow);
    }

    public static void WriteInfoLine(string message)
    {
        WriteLineWithColour(message, ConsoleColor.DarkBlue);
    }

    public static void WriteInfo(string message)
    {
        WriteWithColour(message, ConsoleColor.DarkBlue);
    }

    public static void WriteSuccessLine(string message)
    {
        WriteLineWithColour(message, ConsoleColor.Green);
    }

    public static void WriteSuccess(string message)
    {
        WriteWithColour(message, ConsoleColor.Green);
    }

    public static void WriteDebugLine(string message)
    {
        if (DebugPrintingEnabled)
            WriteLineWithColour(message, ConsoleColor.Gray);
    }

    public static void WriteDebug(string message)
    {
        if (DebugPrintingEnabled)
            WriteWithColour(message, ConsoleColor.Gray);
    }

    public static void WriteNormalLine(string message)
    {
        WriteLineWithColour(message, ConsoleColor.DarkGray);
    }

    public static void WriteNormal(string message)
    {
        WriteWithColour(message, ConsoleColor.DarkGray);
    }

    public static void WriteLineWithColour(string message, ConsoleColor color)
    {
        var currentColour = Console.ForegroundColor;

        if (currentColour == color)
        {
            Console.WriteLine(message);
        }
        else
        {
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ForegroundColor = currentColour;
        }
    }

    public static void WriteWithColour(string message, ConsoleColor color)
    {
        var currentColour = Console.ForegroundColor;

        if (currentColour == color)
        {
            Console.Write(message);
        }
        else
        {
            Console.ForegroundColor = color;
            Console.Write(message);
            Console.ForegroundColor = currentColour;
        }
    }
}
