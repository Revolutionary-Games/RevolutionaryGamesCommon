namespace ScriptsBase.Checks.FileTypes;

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

/// <summary>
///   Checks that every branch in an if/else chain uses braces.
/// </summary>
public class IfElseBracesCheck : CSharpSyntaxFileCheck
{
    public override IEnumerable<string> Handle(string path, SyntaxTree syntaxTree)
    {
        var sourceText = syntaxTree.GetText();
        var root = syntaxTree.GetRoot();

        foreach (var ifStatement in root.DescendantNodes().OfType<IfStatementSyntax>())
        {
            // An else-if is part of the chain started by its parent if. Check the whole chain once so a
            // missing brace is reported only once.
            if (ifStatement.Parent is ElseClauseSyntax)
                continue;

            if (ifStatement.Else != null)
            {
                foreach (var error in CheckChain(sourceText, ifStatement))
                    yield return error;
            }
            else if (IsMultilineCondition(ifStatement, sourceText) && ifStatement.Statement is not BlockSyntax)
            {
                yield return CreateError(sourceText, ifStatement,
                    "An if statement with a multiline condition must use braces");
            }
        }
    }

    private static bool IsMultilineCondition(IfStatementSyntax ifStatement, SourceText sourceText)
    {
        var conditionLineSpan = sourceText.Lines.GetLinePositionSpan(ifStatement.Condition.Span);
        return conditionLineSpan.Start.Line != conditionLineSpan.End.Line;
    }

    private static IEnumerable<string> CheckChain(SourceText sourceText, IfStatementSyntax ifStatement)
    {
        if (ifStatement.Statement is not BlockSyntax)
        {
            yield return CreateError(sourceText, ifStatement,
                "An if statement with an else branch must use braces for every branch");
        }

        if (ifStatement.Else == null)
            yield break;

        if (ifStatement.Else.Statement is IfStatementSyntax elseIf)
        {
            foreach (var error in CheckChain(sourceText, elseIf))
                yield return error;
        }
        else if (ifStatement.Else.Statement is not BlockSyntax)
        {
            yield return CreateError(sourceText, ifStatement.Else,
                "An if statement with an else branch must use braces for every branch");
        }
    }

    private static string CreateError(SourceText sourceText, SyntaxNode node, string message)
    {
        var line = sourceText.Lines.GetLineFromPosition(node.SpanStart).LineNumber + 1;
        return $"Line {line}: {message}";
    }
}
