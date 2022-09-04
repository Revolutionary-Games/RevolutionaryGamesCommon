namespace ScriptsBase.Checks.FileTypes;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

public class JSONCheck : FileCheck
{
    public JSONCheck() : base(".json")
    {
    }

    public override async IAsyncEnumerable<string> Handle(string path)
    {
        var originalFileContent = await File.ReadAllTextAsync(path, Encoding.UTF8);

        if (OperatingSystem.IsWindows())
            originalFileContent = originalFileContent.Replace("\r\n", "\n");

        var parsed = JsonDocument.Parse(originalFileContent);

        using var memoryStream = new MemoryStream();
        memoryStream.Capacity = originalFileContent.Length;

        await using (var jsonWriter = new Utf8JsonWriter(memoryStream, new JsonWriterOptions
                     {
                         Indented = true,
                     }))
        {
            parsed.WriteTo(jsonWriter);
        }

        // Add a trailing newline if not already added
        memoryStream.Seek(-1, SeekOrigin.End);
        if (memoryStream.ReadByte() != '\n')
            memoryStream.WriteByte((byte)'\n');

        memoryStream.Position = 0;

        using var reader = new StreamReader(memoryStream, Encoding.UTF8);

        var newContent = await reader.ReadToEndAsync();

        if (!newContent.EndsWith("\n"))
            throw new Exception("Generated JSON doesn't end with a newline (script problem)");

        // Trying to make this work nicely on Windows
        // TODO: test on Windows
        if (OperatingSystem.IsWindows())
            newContent = newContent.Replace("\r\n", "\n");

        if (originalFileContent == newContent)
            yield break;

        await File.WriteAllTextAsync(path, newContent, Encoding.UTF8);

        yield return "JSON formatting made changes";
    }
}
