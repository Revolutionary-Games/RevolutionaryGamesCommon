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

    private int lineIndex;

    public LineByLineReader(string text)
    {
        this.text = text;
    }

    /// <summary>
    ///   True once this has reached the end of the <see cref="text"/>. And can no longer advance / read anything.
    /// </summary>
    public bool Ended { get; private set; }

    /// <summary>
    ///   True when this is currently at a line end
    /// </summary>
    public bool AtLineEnd { get; private set; }

    /// <summary>
    ///   Zero-based index of the current line number
    /// </summary>
    public int LineIndex => lineIndex;

    /// <summary>
    ///   A normal line number of the current line (line numbers begin at 1)
    /// </summary>
    public int LineNumber => lineIndex + 1;

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
    /// <returns>
    ///   Split strings as an enumerable range, if ends with a newline includes an empty string at the end
    /// </returns>
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
                if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    ++i;
                }

                start = i + 1;
            }
        }

        if (start < i)
        {
            yield return text.Substring(start, i - start);
        }
        else
        {
            yield return string.Empty;
        }
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
            if (IsLineEnding(text[index]))
            {
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
    ///   Scan backwards until a line ending (moves to the first character of multiline line ends) and stops there.
    ///   Reverse variant of <see cref="LookForLineEnd"/>.
    /// </summary>
    /// <returns>True when found</returns>
    public bool LookBackwardsForLineEnd()
    {
        if (AtLineEnd)
        {
            throw new InvalidOperationException(
                "Currently at a line end, should be moved to next / previous line first");
        }

        if (Ended)
        {
            // Become non-ended
            Ended = false;
            index = text.Length - 1;
        }
        else if (index >= text.Length)
        {
            // Allow scanning backwards when exactly at the end
            index = text.Length - 1;
        }

        for (; index >= 0; --index)
        {
            if (IsLineEnding(text[index]))
            {
                // --lineNumber;

                // Handle multi character line endings
                if (text[index] == '\n' && index > 0 && text[index - 1] == '\r')
                    --index;

                AtLineEnd = true;
                return true;
            }
        }

        // Found start of text without finding a new line
        index = 0;
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

        ++lineIndex;
        AtLineEnd = false;
    }

    /// <summary>
    ///   When at a new line moves to the previous line
    /// </summary>
    /// <exception cref="InvalidOperationException">When not at a line end <see cref="AtLineEnd"/></exception>
    public void MoveToPreviousLine()
    {
        if (!AtLineEnd)
            throw new InvalidOperationException("Not at a line end");

        --index;

        if (index < 0)
            index = 0;

        --lineIndex;
        AtLineEnd = false;
    }

    /// <summary>
    ///   Only valid when this is ended. When called this rewinds this to just before the end status.
    /// </summary>
    /// <exception cref="InvalidOperationException">If not at the end</exception>
    public void MoveBackwardsFromEnd()
    {
        if (!Ended)
            throw new InvalidOperationException("Must be currently ended");

        index = text.Length;
        Ended = false;
    }

    /// <summary>
    ///   Reads current line from its beginning to here. Even if <see cref="AtLineEnd"/> is true this won't include the
    ///   line ending characters.
    /// </summary>
    /// <returns>The current line in the text</returns>
    /// <exception cref="InvalidOperationException">If this is already ended</exception>
    public string ReadCurrentLineToStart()
    {
        FindCurrentLineRange(out var startIndex, out var endIndex);

        return text.Substring(startIndex, endIndex - startIndex + 1);
    }

    /// <summary>
    ///   Similar to <see cref="ReadCurrentLineToStart"/> but only finds the character range that is the current line
    /// </summary>
    /// <param name="startIndex">Start of current line</param>
    /// <param name="endIndex">
    ///   End of current line (this character is included so if calculating length +1 needs to be added)
    /// </param>
    public void FindCurrentLineRange(out int startIndex, out int endIndex)
    {
        ThrowIfEnded();

        endIndex = index;

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

        startIndex = endIndex;

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
    }

    /// <summary>
    ///   Efficiently compares the current line of this reader with another reader
    /// </summary>
    /// <param name="otherReader">Other reader to compare with</param>
    /// <returns>True when the lines match</returns>
    /// <exception cref="InvalidOperationException">
    ///   If this reader has ended, if other reader has ended this is assumed to not match
    /// </exception>
    public bool CompareCurrentLineWith(LineByLineReader otherReader)
    {
        ThrowIfEnded();

        if (otherReader.Ended)
            return false;

        FindCurrentLineRange(out var ourStart, out var ourEnd);
        otherReader.FindCurrentLineRange(out var otherStart, out var otherEnd);

        var otherText = otherReader.text;

        // Different length lines cannot be equal
        int length = ourEnd - ourStart + 1;
        if (length != otherEnd - otherStart + 1)
            return false;

        // After calculating the ranges just compare character by character
        for (int i = 0; i < length; ++i)
        {
            if (text[ourStart + i] != otherText[otherStart + i])
                return false;
        }

        return true;
    }

    /// <summary>
    ///   Checks if this reader is behind the other reader on the string they are reading
    /// </summary>
    /// <param name="otherReader">Other reader to compare</param>
    /// <returns>True if behind</returns>
    /// <exception cref="ArgumentException">If called with an unrelated reader (different string)</exception>
    public bool IsBehind(LineByLineReader otherReader)
    {
        if (Ended)
            return false;

        if (otherReader.Ended)
            return true;

        if (text != otherReader.text)
            throw new ArgumentException("Should only compare reader that is on the same string");

        return index < otherReader.index;
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
            lineIndex = lineIndex,
            Ended = Ended,
            AtLineEnd = AtLineEnd,
        };
    }

    public override string ToString()
    {
        if (Ended)
            return $"Ended reader at index {index}";

        return $"Reader {(AtLineEnd ? "(EOL) " : string.Empty)}at line {LineNumber} (index: {index}):" +
            $" {ReadCurrentLineToStart()}";
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
