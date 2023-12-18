namespace ScriptsBase.Checks.FileTypes;

using System;
using System.Collections.Generic;
using System.IO;

public class EndsWithNewLineCheck : FileCheck
{
    public EndsWithNewLineCheck(string firstHandledFileEnding, params string[] extraHandledFileEndings) :
        base(firstHandledFileEnding, extraHandledFileEndings)
    {
    }

    public override async IAsyncEnumerable<string> Handle(string path)
    {
        // TODO: check if this is still true with the C# script version
        // This next check is a bit problematic on Windows so it is skipped
        if (OperatingSystem.IsWindows())
            yield break;

        await using var reader = File.OpenRead(path);

        reader.Seek(-1, SeekOrigin.End);

        var buffer = new byte[1];

        if (await reader.ReadAsync(buffer) != 1)
            throw new Exception("Failed to read last byte of file");

        if (buffer[0] != '\n')
        {
            yield return "File doesn't end with a new line";
        }
    }
}
