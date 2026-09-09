namespace ScriptsBase.Checks.FileTypes;

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>
///   Checks that switch sections spanning more than 5 lines are wrapped in braces.
/// </summary>
public class SwitchCaseBracesCheck : CSharpSyntaxFileCheck
{
    private const int MAX_UNBRACED_CASE_LINES = 5;

    public override IEnumerable<string> Handle(string path, SyntaxTree syntaxTree)
    {
        var sourceText = syntaxTree.GetText();
        var root = syntaxTree.GetRoot();

        foreach (var switchSection in root.DescendantNodes().OfType<SwitchSectionSyntax>())
        {
            if (switchSection.Statements.Count < 1 ||
                (switchSection.Statements.Count == 1 && switchSection.Statements[0] is BlockSyntax))
            {
                continue;
            }

            var lineSpan = sourceText.Lines.GetLinePositionSpan(switchSection.Span);
            var lineCount = lineSpan.End.Line - lineSpan.Start.Line + 1;

            if (lineCount > MAX_UNBRACED_CASE_LINES)
            {
                var line = lineSpan.Start.Line + 1;
                yield return
                    $"Line {line}: A switch case spanning more than {MAX_UNBRACED_CASE_LINES} lines must use braces " +
                    $"(it spans {lineCount} lines)";
            }
        }
    }
}
