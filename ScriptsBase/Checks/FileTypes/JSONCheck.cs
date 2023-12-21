namespace ScriptsBase.Checks.FileTypes;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

public class JSONCheck : FileCheck
{
    public const string DotnetToolsConfigFileName = "dotnet-tools.json";

    private readonly IReadOnlyCollection<string> fileTypesToNotHaveTrailingNewLine;

    public JSONCheck(string[]? filesWithoutTrailingNewLine = null) : base(".json")
    {
        fileTypesToNotHaveTrailingNewLine = filesWithoutTrailingNewLine ?? new[] { DotnetToolsConfigFileName };
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

        bool addTrailingNewLine = ShouldHaveNewLine(path);

        memoryStream.Seek(-1, SeekOrigin.End);

        // Add a trailing newline if not already added
        if (addTrailingNewLine)
        {
            if (memoryStream.ReadByte() != '\n')
                memoryStream.WriteByte((byte)'\n');
        }
        else
        {
            if (memoryStream.ReadByte() == '\n')
                memoryStream.SetLength(memoryStream.Length - 1);
        }

        memoryStream.Position = 0;

        using var reader = new StreamReader(memoryStream, Encoding.UTF8);

        var newContent = await reader.ReadToEndAsync();

        if (addTrailingNewLine)
        {
            if (!newContent.EndsWith("\n"))
                throw new Exception("Generated JSON doesn't end with a newline (script problem)");
        }

        // Trying to make this work nicely on Windows
        if (OperatingSystem.IsWindows())
            newContent = newContent.Replace("\r\n", "\n");

        if (originalFileContent == newContent)
            yield break;

        // This ends up writing the file with a BOM
        await File.WriteAllTextAsync(path, newContent, Encoding.UTF8);

        yield return "JSON formatting made changes";
    }

    private bool ShouldHaveNewLine(string path)
    {
        return !fileTypesToNotHaveTrailingNewLine.Any(path.EndsWith);
    }
}
