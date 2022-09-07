namespace ScriptsBase.Utilities;

using System;

/// <summary>
///   Writes console messages with colours
/// </summary>
public static class ColourConsole
{
    /// <summary>
    ///   Due to dotnet not working with DOTNET_CONSOLE_ANSI_COLOR even though it should
    ///   https://github.com/dotnet/runtime/issues/33980 setting this on uses our own ansi colour handling on,
    ///   non-windows
    /// </summary>
    public const bool UseCustomColourHandling = true;

    public static readonly Lazy<bool> ColourIsPrevented = new(CheckNoColourEnvironmentVariable);

    /// <summary>
    ///   Resets terminal to default colour
    /// </summary>
    private const string ResetForegroundColour = "\x1B[39m\x1B[22m";

    private const string ResetBackgroundColour = "\x1B[49m";

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

    public static void WriteLineWithColour(string message, ConsoleColor colour)
    {
        if (ColourIsPrevented.Value)
        {
            Console.WriteLine(message);
            return;
        }

        if (!OperatingSystem.IsWindows() && UseCustomColourHandling)
        {
            Console.WriteLine($"{GetAnsiForegroundColour(colour)}{message}{ResetForegroundColour}");
            return;
        }

        var currentColour = Console.ForegroundColor;

        if (currentColour == colour)
        {
            Console.WriteLine(message);
        }
        else
        {
            Console.ForegroundColor = colour;
            Console.WriteLine(message);
            Console.ForegroundColor = currentColour;
        }
    }

    public static void WriteWithColour(string message, ConsoleColor colour)
    {
        if (ColourIsPrevented.Value)
        {
            Console.Write(message);
            return;
        }

        if (!OperatingSystem.IsWindows() && UseCustomColourHandling)
        {
            Console.Write($"{GetAnsiForegroundColour(colour)}{message}{ResetForegroundColour}");
            return;
        }

        var currentColour = Console.ForegroundColor;

        if (currentColour == colour)
        {
            Console.Write(message);
        }
        else
        {
            Console.ForegroundColor = colour;
            Console.Write(message);
            Console.ForegroundColor = currentColour;
        }
    }

    public static void ResetColor()
    {
        if (ColourIsPrevented.Value)
        {
            return;
        }

        // This is actually not a good idea here as we'll output these after the last full line of output of our process
        // if (!OperatingSystem.IsWindows() && UseCustomColourHandling)
        // {
        //     Console.Write(ResetForegroundColour);
        //     Console.Write(ResetBackgroundColour);
        //     return;
        // }

        Console.ResetColor();
    }

    private static string GetAnsiForegroundColour(ConsoleColor color)
    {
        return color switch
        {
            ConsoleColor.Black => "\x1B[30m",
            ConsoleColor.DarkRed => "\x1B[31m",
            ConsoleColor.DarkGreen => "\x1B[32m",
            ConsoleColor.DarkYellow => "\x1B[33m",
            ConsoleColor.DarkBlue => "\x1B[34m",
            ConsoleColor.DarkMagenta => "\x1B[35m",
            ConsoleColor.DarkCyan => "\x1B[36m",
            ConsoleColor.Gray => "\x1B[37m",
            ConsoleColor.Red => "\x1B[1m\x1B[31m",
            ConsoleColor.Green => "\x1B[1m\x1B[32m",
            ConsoleColor.Yellow => "\x1B[1m\x1B[33m",
            ConsoleColor.Blue => "\x1B[1m\x1B[34m",
            ConsoleColor.Magenta => "\x1B[1m\x1B[35m",
            ConsoleColor.Cyan => "\x1B[1m\x1B[36m",
            ConsoleColor.White => "\x1B[1m\x1B[37m",
            _ => ResetForegroundColour,
        };
    }

    private static string GetAnsiBackgroundColour(ConsoleColor color)
    {
        return color switch
        {
            ConsoleColor.Black => "\x1B[40m",
            ConsoleColor.DarkRed or ConsoleColor.Red => "\x1B[41m",
            ConsoleColor.DarkGreen or ConsoleColor.Green => "\x1B[42m",
            ConsoleColor.DarkYellow or ConsoleColor.Yellow => "\x1B[43m",
            ConsoleColor.DarkBlue or ConsoleColor.Blue => "\x1B[44m",
            ConsoleColor.DarkMagenta or ConsoleColor.Magenta => "\x1B[45m",
            ConsoleColor.DarkCyan or ConsoleColor.Cyan => "\x1B[46m",
            ConsoleColor.Gray or ConsoleColor.DarkGray => "\x1B[47m",
            _ => ResetForegroundColour,
        };
    }

    private static bool CheckNoColourEnvironmentVariable()
    {
        var value = Environment.GetEnvironmentVariable("NO_COLOR");

        if (value is { Length: > 0 })
        {
            // Colour is disabled
            // https://no-color.org/
            return true;
        }

        return false;
    }
}
