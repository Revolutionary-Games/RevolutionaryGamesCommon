namespace ScriptsBase.Checks;

using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

/// <summary>
///   Uses our custom special logic to do some rewriting in
/// </summary>
public class RewriteTool : CodeCheck
{
    private const string StartFileEnumerateFolder = "./";

    public override async Task Run(CodeCheckRun runData, CancellationToken cancellationToken)
    {
        var ignored = await FileChecks.GenerateKnownToBeIgnoredExtensions("./");

        var files = FileChecks.EnumerateFilesRecursively(StartFileEnumerateFolder, runData, ignored)
            .Where(f => f.EndsWith(".cs")).ToList();

        if (files.Count < 1)
        {
            runData.OutputTextWithMutex("No files to check with rewrite tool");
            return;
        }

        var mutex = runData.BuildMutex;

        await mutex.WaitAsync(cancellationToken);
        try
        {
            foreach (var file in files)
            {
                if (!await RunOnFile(file, runData, cancellationToken))
                {
                    // As we don't have a writer for writing the filename when we find the first error, we put an extra
                    // newline out here to give more spacing if there are errors from multiple files
                    runData.ReportError($"Code rewrite did changes (see above) in file: {file}\n");
                }
            }
        }
        finally
        {
            mutex.Release();
        }
    }

    private async Task<bool> RunOnFile(string file, CodeCheckRun runData, CancellationToken cancellationToken)
    {
        var text = await File.ReadAllBytesAsync(file, cancellationToken);

        var sourceText = SourceText.From(text, text.Length, Encoding.UTF8);
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText, new CSharpParseOptions(), file, cancellationToken);

        var oldRoot = await syntaxTree.GetRootAsync(cancellationToken);

        var rewriter = new Rewriter(runData, sourceText);

        var newRoot = rewriter.Visit(oldRoot);

        // When the rewriter changes something the identity of the root node changes, which we can simply check like
        // this to detect when changes are made
        if (newRoot == oldRoot)
        {
            // No changes
            return true;
        }

        // Write the changed file over the existing one
        var tempFile = file + ".tmp";

        await File.WriteAllTextAsync(tempFile, newRoot.ToFullString(), Encoding.UTF8, cancellationToken);
        File.Move(tempFile, file, true);

        return false;
    }

    private class Rewriter : CSharpSyntaxRewriter
    {
        private readonly CodeCheckRun runData;
        private readonly SourceText sourceText;
        private bool inInterface;

        public Rewriter(CodeCheckRun runData, SourceText sourceText)
        {
            this.runData = runData;
            this.sourceText = sourceText;
        }

        public override SyntaxNode? VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
        {
            inInterface = true;

            var result = base.VisitInterfaceDeclaration(node);

            inInterface = false;

            return result;
        }

        public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            if (!inInterface)
                return base.VisitMethodDeclaration(node);

            if (node.Modifiers.Count < 1)
            {
                runData.OutputTextWithMutex(
                    $"{GetLocation(node.Span)} had an interface property with missing access modifiers");

                var publicToken = SyntaxFactory.Token(SyntaxKind.PublicKeyword);

                var newNode = SyntaxFactory.MethodDeclaration(node.AttributeLists, new SyntaxTokenList(publicToken),
                    node.ReturnType, node.ExplicitInterfaceSpecifier, node.Identifier, node.TypeParameterList,
                    node.ParameterList, node.ConstraintClauses, node.Body, node.ExpressionBody, node.SemicolonToken);

                return newNode;
            }

            return base.VisitMethodDeclaration(node);
        }

        public override SyntaxNode? VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            if (!inInterface)
                return base.VisitPropertyDeclaration(node);

            if (node.Modifiers.Count < 1)
            {
                runData.OutputTextWithMutex(
                    $"{GetLocation(node.Span)} had an interface method with missing access modifiers");

                var publicToken = SyntaxFactory.Token(SyntaxKind.PublicKeyword);

                var newNode = SyntaxFactory.PropertyDeclaration(node.AttributeLists, new SyntaxTokenList(publicToken),
                    node.Type, node.ExplicitInterfaceSpecifier, node.Identifier, node.AccessorList, node.ExpressionBody,
                    node.Initializer, node.SemicolonToken);

                return newNode;
            }

            return base.VisitPropertyDeclaration(node);
        }

        private string GetLocation(TextSpan span)
        {
            return $"Line {sourceText.Lines.GetLinePosition(span.Start).Line + 1}";
        }
    }
}
