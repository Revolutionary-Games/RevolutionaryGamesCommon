namespace SharedBase.Utilities;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

/// <summary>
///   Utility for reading a string line by line
/// </summary>
public struct LineByLineReader
{
    private readonly string text;

    /// <summary>
    ///   Currently read index in <see cref="text"/>
    /// </summary>
    private int index;

    public LineByLineReader(string text)
    {
        this.text = text;
    }

    /// <summary>
    ///    True once this has reached the end of the <see cref="text"/>. And can no longer advance / read anything.
    /// </summary>
    public bool Ended { get; private set; }

    /// <summary>
    ///   True when this is currently at a line end
    /// </summary>
    public bool AtLineEnd { get; private set; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsLineEnding(char character)
    {
        return character is '\r' or '\n';
    }

    /// <summary>
    ///   Split a text into lines immediately. This uses more memory but in cases where all lines are just needed
    ///   separately this is the right method to use.
    /// </summary>
    /// <param name="text">Text to split along with UNIX or Windows style line endings</param>
    /// <returns>Split strings as an enumerable range</returns>
    public static IEnumerable<string> SplitToLines(string text)
    {
        int start = 0;

        int i;
        for (i = 0; i < text.Length; ++i)
        {
            if (IsLineEnding(text[i]))
            {
                yield return text.Substring(start, i - start);

                // Skip multi character line end
                if (text[i] == '\r' && i + 1 > text.Length && text[i + 1] == '\n')
                {
                    ++i;
                }

                start = i + 1;
            }
        }

        if (start < i)
            yield return text.Substring(start, i - start);
    }

    /// <summary>
    ///   Scan forward until the next line end and stop there
    /// </summary>
    /// <returns>True if a line end was found</returns>
    public bool LookForLineEnd()
    {
        if (AtLineEnd)
            throw new InvalidOperationException("Currently at a line end, should be moved to next line first");

        if (CheckTextEndConditions())
            return false;

        for (; index < text.Length; ++index)
        {
            if (text[index] == '\r' || text[index] == '\n')
            {
                // ++lineNumber;
                AtLineEnd = true;
                return true;
            }
        }

        // If this reached the end of the text without hitting a newline character, already move this to the state that
        // is ready to go to the end condition on next search
        index = text.Length + 1;

        return false;
    }

    /// <summary>
    ///   Moves to the next line if this is currently at a line end, otherwise throws an exception
    /// </summary>
    /// <exception cref="InvalidOperationException">When not at a line end <see cref="AtLineEnd"/></exception>
    public void MoveToNextLine()
    {
        if (!AtLineEnd)
            throw new InvalidOperationException("Not at a line end");

        // Skip multibyte line ending
        if (text[index] == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
        {
            index += 2;
        }
        else
        {
            ++index;
        }

        AtLineEnd = false;
    }

    /// <summary>
    ///   Reads current line from its beginning to here. Even if <see cref="AtLineEnd"/> is true this won't include the
    ///   line ending characters.
    /// </summary>
    /// <returns>The current line in the text</returns>
    /// <exception cref="InvalidOperationException">If this is already ended</exception>
    public string ReadCurrentLineToStart()
    {
        ThrowIfEnded();

        var endIndex = index;

        // Adjust to end before the line ending
        if (AtLineEnd)
        {
            --endIndex;
        }
        else if (index >= text.Length)
        {
            // Allow reading to the end of the text before this reader is marked as ended
            endIndex = text.Length - 1;
        }

        int startIndex = endIndex;

        // Scan backwards to find the previous line (or start of the string)
        for (; startIndex > 0; --startIndex)
        {
            if (IsLineEnding(text[startIndex]))
            {
                // Copy just before this
                ++startIndex;
                break;
            }
        }

        return text.Substring(startIndex, endIndex - startIndex + 1);
    }

    /// <summary>
    ///   Clones the reader to get a new reader that is at the same position as this one
    /// </summary>
    /// <returns>Reader that is a copy of this</returns>
    public LineByLineReader Clone()
    {
        return new LineByLineReader(text)
        {
            index = index,
            Ended = Ended,
            AtLineEnd = AtLineEnd,
        };
    }

    /// <summary>
    ///   Mark this reader as having ended if currently at length + 1 or greater amount out of bounds. This is done
    ///   like this to allow reading code to detect being at the last bit of the string and then only after handling
    ///   that would this get marked as ended.
    /// </summary>
    /// <returns>True when ended</returns>
    private bool CheckTextEndConditions()
    {
        if (Ended)
            return true;

        if (index < text.Length)
            return false;

        // Make sure at line end flag is not on when out of bounds
        AtLineEnd = false;

        if (index >= text.Length + 1)
        {
            // Fully ended (there's been at least one previous call to this that was out of bounds
            Ended = true;
            return true;
        }

        // Increment index even though it is out of bounds to track that this is already called once
        ++index;

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfEnded()
    {
        if (Ended)
            throw new InvalidOperationException("Reader is already past the end of the text");
    }
}
