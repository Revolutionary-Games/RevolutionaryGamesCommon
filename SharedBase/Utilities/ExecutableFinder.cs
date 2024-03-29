namespace SharedBase.Utilities;

using System;
using System.IO;

public static class ExecutableFinder
{
    /// <summary>
    ///   Tries to find command / executable in path
    /// </summary>
    /// <param name="commandName">The name of the executable</param>
    /// <returns>Full path to the command or null</returns>
    /// <remarks>
    ///   <para>
    ///     This approach has been ported over from RubySetupSystem
    ///   </para>
    /// </remarks>
    public static string? Which(string commandName)
    {
        if (OperatingSystem.IsWindows())
        {
            if (commandName.EndsWith(".exe"))
            {
                commandName = commandName.Substring(0, commandName.Length - ".exe".Length);
            }
        }

        var extensions = PathExtensions();

        foreach (var path in SystemPath())
        {
            foreach (var extension in extensions)
            {
                var fullPath = Path.Join(path, $"{commandName}{extension}");

                if (File.Exists(fullPath))
                {
                    var attributes = File.GetAttributes(fullPath);

                    // TODO: there used to be executable flag check here but apparently C# doesn't have that
                    // So that is skipped, so this can find something that isn't an executable that is in PATH
                    if (!attributes.HasFlag(FileAttributes.Directory))
                        return fullPath;
                }
            }
        }

        return null;
    }

    public static void PrintPathInfo(TextWriter writer)
    {
        writer.WriteLine("Tool / executable was searched for in PATH folders, but was not found. " +
            "Note that changes to PATH may only be picked up after terminal restart.");

        writer.WriteLine("Currently active PATH:");

        foreach (var path in SystemPath())
        {
            writer.Write(" ");
            writer.WriteLine(path);
        }

        writer.WriteLine("End of PATH folder list");
    }

    public static string[] SystemPath()
    {
        return Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ??
            throw new Exception("PATH environment variable is missing");
    }

    public static string[] PathExtensions()
    {
        return (Environment.GetEnvironmentVariable("PATHEXT") ?? string.Empty).Split(';');
    }
}
