namespace ScriptsBase.Checks.FileTypes;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

public class CSharpCheck : FileCheck
{
    /// <summary>
    ///   A bit of extra margin for razor files to have longer lines
    /// </summary>
    public const int MAX_LINE_LENGTH_RAZOR = 140;

    public const int XML_INDENTATION = 2;
    public const int XML_BASE_INDENTATION = 1;

    // The first three regexes are compiled here as they are used on all lines of source code
    public static readonly Regex MissingFloatDecimalPoint = new(@"(?<![\d.])[^.]\d+f\b", RegexOptions.Compiled);
    public static readonly Regex IncorrectFloatSuffixCase = new(@"^\d+F\W", RegexOptions.Compiled);

    private const string RAZOR_EXTENSION = ".razor";

    private readonly int defaultMaxLength;
    private readonly int maxRazorLength;

    private readonly CommentXmlParser xmlCommentParser = new();

    private bool insideXmlComment;

    public CSharpCheck(int maxLength = LineCharacterHelpers.MAX_LINE_LENGTH,
        int maxRazorLength = MAX_LINE_LENGTH_RAZOR) : base(".cs", RAZOR_EXTENSION)
    {
        defaultMaxLength = maxLength;
        this.maxRazorLength = maxRazorLength;
    }

    public override async IAsyncEnumerable<string> Handle(string path)
    {
        var maxLength = path.EndsWith(RAZOR_EXTENSION) ? maxRazorLength : defaultMaxLength;
        bool checkingLength = true;

        bool windows = OperatingSystem.IsWindows();

        // It seems File.ReadLines cannot be used here as that doesn't give us the line separators
        var rawData = await File.ReadAllBytesAsync(path);

        var text = Encoding.UTF8.GetString(rawData);

        int lineNumber = 0;

        // TODO: is it too bad here to have all of the split strings in memory at once
        foreach (var line in text.Split('\n'))
        {
            ++lineNumber;

            LineCharacterHelpers.HandleLineLengthCheckControlComments(line, ref checkingLength);

            var tabError = LineCharacterHelpers.CheckLineForTab(line, lineNumber);
            if (tabError != null)
                yield return tabError;

            bool endsWithCarriageReturn = line.EndsWith("\r");

            if (!windows && endsWithCarriageReturn)
            {
                yield return $"Line {lineNumber} contains a windows style line ending (CR LF)";
            }

            var lengthError =
                LineCharacterHelpers.CheckLineForBeingTooLong(line, lineNumber, checkingLength, maxLength);
            if (lengthError != null)
                yield return lengthError;

            var match = MissingFloatDecimalPoint.Match(line);

            if (match.Success)
            {
                yield return
                    $"Line {lineNumber} contains an invalid float format (missing decimal point). " +
                    $"{match.Groups[0].Value}";
            }

            match = IncorrectFloatSuffixCase.Match(line);

            if (match.Success)
            {
                yield return
                    $"Line {lineNumber} contains an uppercase float suffix. {match.Groups[0].Value}";
            }

            // Further processing is done by the xml parser so we can do a real quick check here
            if (line.Contains("///"))
            {
                if (!insideXmlComment)
                {
                    // Clear parser state when a new comment starts
                    xmlCommentParser.Clear();
                    insideXmlComment = true;
                }

                if (xmlCommentParser.ProcessLine(lineNumber, line))
                {
                    foreach (var error in xmlCommentParser.GetErrors())
                    {
                        yield return error;
                    }
                }
            }
            else if (insideXmlComment)
            {
                insideXmlComment = false;

                // XML comment ended
                if (xmlCommentParser.OnEnded())
                {
                    foreach (var error in xmlCommentParser.GetErrors())
                    {
                        yield return error;
                    }
                }
            }
        }
    }

    private class CommentXmlParser
    {
        private readonly List<string> endedTags = new();
        private readonly List<string> startedTags = new();
        private readonly List<string> errors = new();

        private int xmlNestingLevel;
        private bool seenRemarks;
        private bool seenParaElement;
        private bool insideCodeBlock;

        private int lastSeenRemarksLine;

        public bool ProcessLine(int lineNumber, string line)
        {
            // Process the line character by character with our basic custom XML parser here

            if (!ParseXmlData(lineNumber, line, out bool inXml, out int indentation, out bool contentAfterTag))
            {
                ReportParserError("tag has not ended", lineNumber);
            }

            // Skip processing if we didn't find the start of xml data
            if (!inXml)
                return false;

            // Process the data we extracted after parsing the line

            // Tags starting and ending affect indentation, so they need to be taken into account here
            int endedTagCount = endedTags.Count;
            int startedTagCount = startedTags.Count;

            // Ending tags apply to the current line
            xmlNestingLevel -= endedTagCount;

            if (insideCodeBlock && (endedTags.Contains("code") || endedTags.Contains("c")))
                insideCodeBlock = false;

            // Starting tags only apply to the current line if there are more (or as many) of them as ending tags.
            // And there were ending tags on this line
            bool alreadyAppliedStarted = false;
            if (startedTagCount >= endedTagCount && endedTagCount > 0)
            {
                xmlNestingLevel += startedTagCount;
                alreadyAppliedStarted = true;
            }

            if (xmlNestingLevel < 0)
            {
                AddError($"Line {lineNumber} had xml indent become negative. Our simple parser could not " +
                    "understand the XML in this comment correctly!");
            }

            int expectedIndentation = XML_BASE_INDENTATION + xmlNestingLevel * XML_INDENTATION;

            // Inside code blocks extra indentation needs to be allowed
            if (indentation != expectedIndentation && (!insideCodeBlock || indentation < expectedIndentation))
            {
                AddError($"Line {lineNumber} has wrong amount of indentation in XML comment. " +
                    $"{indentation} != {expectedIndentation} (expected)");
            }

            // Offset the next line if we didn't take the started tags already into account
            if (!alreadyAppliedStarted)
                xmlNestingLevel += startedTagCount;

            if (endedTagCount == startedTagCount)
            {
                // This empty case is just here to prevent the other cases from triggering in the good situation
            }
            else if (endedTagCount > startedTagCount && endedTagCount > 1)
            {
                AddError($"Line {lineNumber} has more ending XML tags than starts, multiline XML content " +
                    "should be split to have start and end tags on their own lines.");
            }
            else if (startedTagCount > 1)
            {
                AddError($"Line {lineNumber} has more than one starting XML tags, multiline XML starting " +
                    "tags should each be on their own line.");
            }
            else if (startedTagCount > 0 && contentAfterTag)
            {
                AddError($"Line {lineNumber} has content after a starting multiline XML tag, multiline " +
                    "content should start on its own line.");
            }

            for (var i = 0; i < startedTags.Count; i++)
            {
                var currentTag = startedTags[i];
                if (currentTag == "remarks")
                {
                    seenRemarks = true;
                    lastSeenRemarksLine = lineNumber;

                    if (contentAfterTag || i + 1 < startedTags.Count)
                    {
                        AddError($"Line {lineNumber} has a <remarks> section where the content (or next tag) " +
                            "is not on the next line.");
                    }
                }

                if (currentTag == "para")
                {
                    seenParaElement = true;

                    if (contentAfterTag)
                    {
                        AddError($"Line {lineNumber} has a <para> section where the content is not on the next line.");
                    }
                }

                if (currentTag is "code" or "c")
                {
                    insideCodeBlock = true;
                }

                if (currentTag == "summary")
                {
                    if (contentAfterTag || i + 1 < startedTags.Count)
                    {
                        AddError($"Line {lineNumber} has a <summary> tag where the content is not on the next line.");
                    }
                }
            }

            return errors.Count > 0;
        }

        public bool OnEnded()
        {
            if (seenRemarks && !seenParaElement)
            {
                AddError(
                    $"Line {lastSeenRemarksLine} has an XML comment with a <remarks> section that doesn't contain " +
                    "<para> sections (all remarks text needs to be inside para sections)");
            }

            return errors is { Count: > 0 };
        }

        public IEnumerable<string> GetErrors()
        {
            foreach (var error in errors)
            {
                yield return error;
            }

            errors.Clear();
        }

        public void Clear()
        {
            errors.Clear();

            xmlNestingLevel = 0;
            seenRemarks = false;
            seenParaElement = false;
            insideCodeBlock = false;

            lastSeenRemarksLine = 0;
        }

        private bool ParseXmlData(int lineNumber, string line, out bool inXml, out int indentation,
            out bool contentAfterTag)
        {
            indentation = 0;

            inXml = false;
            contentAfterTag = false;

            int? sectionStart = null;
            int? sectionEnd = null;

            bool escapeCharacter = false;
            bool resetEscape = false;
            bool insideString = false;

            bool seenStartOfXmlContent = false;
            bool tagIsEndTag = false;
            bool tagIsSelfClosing = false;
            bool insideXmlTag = false;

            endedTags.Clear();
            startedTags.Clear();

            for (int charIndex = 0; charIndex < line.Length; ++charIndex)
            {
                char character = line[charIndex];

                // Handle unsetting escape characters
                if (resetEscape)
                {
                    escapeCharacter = false;
                    resetEscape = false;
                }
                else if (escapeCharacter)
                {
                    resetEscape = true;
                }

                if (!escapeCharacter && character == '\\')
                {
                    escapeCharacter = true;
                    continue;
                }

                // Handle being inside string
                if (insideString)
                {
                    if (!escapeCharacter && character == '"')
                        insideString = false;

                    if (inXml && !seenStartOfXmlContent)
                    {
                        // Starting a string breaks the sequence of whitespace at the start of the line
                        seenStartOfXmlContent = true;
                    }

                    continue;
                }

                // Handle string starting
                if (!escapeCharacter && character == '"')
                {
                    insideString = true;
                    continue;
                }

                // Look for the starting "///" if we aren't in XML content
                if (!inXml)
                {
                    if (character == '/')
                    {
                        if (sectionStart == null)
                        {
                            // First '/' seen
                            sectionStart = charIndex;
                        }
                        else
                        {
                            // We've reached XML if there's 3 in a row
                            if (charIndex - sectionStart.Value >= 2)
                            {
                                inXml = true;
                                sectionStart = null;
                            }
                        }

                        continue;
                    }

                    sectionStart = null;
                    continue;
                }

                // Process xml content

                if (!seenStartOfXmlContent)
                {
                    if (char.IsWhiteSpace(character))
                    {
                        ++indentation;
                        continue;
                    }

                    seenStartOfXmlContent = true;
                }

                // Parse all tags until the end
                if (insideXmlTag)
                {
                    if (character == '>')
                    {
                        sectionEnd ??= charIndex - 1;

                        if (sectionStart == null)
                        {
                            ReportParserError("Tag name not found before '>'", lineNumber);
                            continue;
                        }

                        // A tag ended
                        if (tagIsSelfClosing)
                        {
                            // Self closing tags don't need handling
                        }
                        else if (tagIsEndTag)
                        {
                            endedTags.Add(GetLineSection(line, sectionStart.Value, sectionEnd.Value));
                        }
                        else
                        {
                            startedTags.Add(GetLineSection(line, sectionStart.Value, sectionEnd.Value));

                            // Only starting tags need to reset this to properly detect content after tags
                            contentAfterTag = false;
                        }

                        insideXmlTag = false;
                        continue;
                    }

                    if (char.IsWhiteSpace(character))
                    {
                        if (sectionStart == null)
                        {
                            AddError(
                                $"Line {lineNumber} has xml tag that has extra space inside the angle brackets (<>).");
                        }

                        sectionEnd ??= charIndex - 1;

                        continue;
                    }

                    if (character == '/' && sectionStart == null)
                    {
                        // This is a closing tag
                        if (tagIsEndTag)
                            ReportParserError("multiple closing tag markers", lineNumber);

                        tagIsEndTag = true;
                        continue;
                    }

                    if (character == '/')
                    {
                        // Trailing '/' means this is a self closing tag
                        if (tagIsSelfClosing)
                            ReportParserError("multiple self closing markers", lineNumber);

                        tagIsSelfClosing = true;
                        continue;
                    }

                    sectionStart ??= charIndex;

                    continue;
                }

                if (character == '<')
                {
                    // Starting a new tag
                    insideXmlTag = true;
                    sectionStart = null;
                    sectionEnd = null;
                    tagIsEndTag = false;
                    tagIsSelfClosing = false;
                    continue;
                }

                if (!char.IsWhiteSpace(character) && startedTags.Count > 0)
                {
                    contentAfterTag = true;
                }
            }

            // Parsing fails if we end up being inside a tag
            return !insideXmlTag;
        }

        private void AddError(string error)
        {
            errors.Add(error);
        }

        private void ReportParserError(string error, int line)
        {
            AddError($"Line {line} cannot be understood by our formatting simple XML parser: {error}");
        }

        private string GetLineSection(string line, int startIndex, int endIndex)
        {
            return line.Substring(startIndex, endIndex - startIndex + 1);
        }
    }
}
