namespace ScriptsBase.Utilities;

using System;
using System.IO;

public static class RunFolderChecker
{
    public static void EnsureRightRunningFolder(string fileThatShouldExist)
    {
        if (!File.Exists(fileThatShouldExist))
        {
            ColourConsole.WriteErrorLine(
                $"Error this script needs to be ran from the folder containing '{fileThatShouldExist}'");
            Environment.Exit(3);
        }
    }
}
