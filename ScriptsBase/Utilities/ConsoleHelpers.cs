namespace ScriptsBase.Utilities;

using System;

public static class ConsoleHelpers
{
    public static void CleanConsoleStateForExit()
    {
        Console.ResetColor();
    }
}
