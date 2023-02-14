namespace ScriptsBase.Checks.FileTypes;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

    public static readonly Regex XmlComment = new(@"^\W{0,2}\s*\/\/\/(\s*)", RegexOptions.Compiled);
    public static readonly Regex XmlTagStart = new(@"<([^>\/]+)>\s*([^<]*)");
    public static readonly Regex XmlTagEnd = new(@"<\/\w+>");

    private const string RAZOR_EXTENSION = ".razor";

    private readonly int defaultMaxLength;
    private readonly int maxRazorLength;

    private bool insideXmlComment;
    private int xmlNestingLevel;
    private bool seenRemarks;
    private bool seenParaElement;
    private bool insideCodeBlock;

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

            match = XmlComment.Match(line);

            if (match.Success)
            {
                if (!insideXmlComment)
                {
                    // An XML comment has just started
                    insideXmlComment = true;
                    xmlNestingLevel = 0;
                    seenRemarks = false;
                    seenParaElement = false;
                    insideCodeBlock = false;
                }

                int indentation = match.Groups[1].Length;

                // Tags starting and ending affect indentation, so they need to be taken into account here
                int endedTags = XmlTagEnd.Count(line);
                var startedTagMatches = XmlTagStart.Matches(line);

                // Ending tags apply to the current line
                xmlNestingLevel -= endedTags;

                // If tags end while we are in a code block, assume it ended the block
                // TODO: we'd need a lot fancier parser to do something better here (as a stopgap we could also capture
                // the ended tag names and compare those)
                if (insideCodeBlock && endedTags > 0)
                    insideCodeBlock = false;

                // Starting tags only apply to the current line if there are more (or as many) of them as ending tags.
                // And there were ending tags on this line
                bool alreadyAppliedStarted = false;
                if (startedTagMatches.Count >= endedTags && endedTags > 0)
                {
                    xmlNestingLevel += startedTagMatches.Count;
                    alreadyAppliedStarted = true;
                }

                if (xmlNestingLevel < 0)
                {
                    yield return $"Line {lineNumber} had xml indent become negative. Our simple parser could not " +
                        "understand the XML in this comment correctly!";
                }

                int expectedIndentation = XML_BASE_INDENTATION + (xmlNestingLevel * XML_INDENTATION);

                // Inside code blocks extra indentation needs to be allowed
                if (indentation != expectedIndentation && (!insideCodeBlock || indentation < expectedIndentation))
                {
                    yield return $"Line {lineNumber} has wrong amount of indentation in XML comment. " +
                        $"{indentation} != {expectedIndentation} (expected)";
                }

                // Offset the next line if we didn't take the started tags already into account
                if (!alreadyAppliedStarted)
                    xmlNestingLevel += startedTagMatches.Count;

                if (endedTags == startedTagMatches.Count)
                {
                    // This empty case is just here to prevent the other cases from triggering in the good situation
                }
                else if (endedTags > startedTagMatches.Count && endedTags > 1)
                {
                    yield return $"Line {lineNumber} has more ending XML tags than starts, multiline XML content " +
                        "should be split to have start and end tags on their own lines.";
                }
                else if (startedTagMatches.Count > 1)
                {
                    yield return $"Line {lineNumber} has more than one starting XML tags, multiline XML starting " +
                        "tags should each be on their own line.";
                }
                else if (startedTagMatches.Count > 0 && startedTagMatches.Last().Groups[2].Length > 0)
                {
                    yield return $"Line {lineNumber} has content after a starting multiline XML tag, multiline " +
                        "content should start on its own line.";
                }

                bool sawRemarksNow = false;

                foreach (Match tagMatch in startedTagMatches)
                {
                    if (sawRemarksNow)
                    {
                        yield return
                            $"Line {lineNumber} has a <remarks> section where the content (following tag) " +
                            "is not on the next line.";
                    }

                    var currentTag = tagMatch.Groups[1].Value;
                    if (currentTag == "remarks")
                    {
                        seenRemarks = true;

                        if (tagMatch.Groups[2].Length > 0)
                        {
                            yield return
                                $"Line {lineNumber} has a <remarks> section where the content is not on the next line.";
                        }

                        sawRemarksNow = true;
                    }

                    if (currentTag == "para")
                    {
                        seenParaElement = true;

                        if (tagMatch.Groups[2].Length > 0)
                        {
                            yield return
                                $"Line {lineNumber} has a <para> section where the content is not on the next line.";
                        }
                    }

                    if (currentTag is "code" or "c")
                    {
                        insideCodeBlock = true;
                    }
                }
            }
            else if (insideXmlComment)
            {
                // XML comment ended
                insideXmlComment = false;

                if (seenRemarks && !seenParaElement)
                {
                    yield return
                        $"Line {lineNumber - 1} has an XML comment with a <remarks> section that doesn't contain " +
                        "<para> sections (all remarks text needs to be inside para sections)";
                }
            }
        }
    }
}
