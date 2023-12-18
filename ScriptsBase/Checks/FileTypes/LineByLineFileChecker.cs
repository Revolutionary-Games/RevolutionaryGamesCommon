namespace ScriptsBase.Checks.FileTypes;

using System.Collections.Generic;
using System.IO;

/// <summary>
///   Base class for all file checks that just check things line by line to find errors
/// </summary>
public abstract class LineByLineFileChecker : FileCheck
{
    public LineByLineFileChecker(string firstHandledFileEnding, params string[] extraHandledFileEndings) :
        base(firstHandledFileEnding, extraHandledFileEndings)
    {
    }

    public override async IAsyncEnumerable<string> Handle(string path)
    {
        using var reader = File.OpenText(path);

        int lineNumber = 0;

        while (true)
        {
            ++lineNumber;
            var line = await reader.ReadLineAsync();

            if (line == null)
                break;

            foreach (var error in CheckLine(line, lineNumber))
            {
                yield return error;
            }
        }
    }

    protected abstract IEnumerable<string> CheckLine(string line, int lineNumber);

    /// <summary>
    ///   Helper for child classes to more easily report errors with line numbers
    /// </summary>
    /// <param name="lineNumber">The line number</param>
    /// <param name="message">An error message to show having happened on this line</param>
    /// <returns>The final formatted line and message</returns>
    protected string FormatErrorLineHelper(int lineNumber, string message)
    {
        return $"Line {lineNumber} {message}";
    }
}
