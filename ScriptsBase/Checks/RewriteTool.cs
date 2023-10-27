namespace ScriptsBase.Checks;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Utilities;

/// <summary>
///   Uses our custom special logic to do some rewriting in
/// </summary>
public class RewriteTool : CodeCheck
{
    public const string NoModifierPlacementType = "no modifier";

    /// <summary>
    ///   Basic order of class members, see <see cref="Rewriter.MemberOrderComparer"/> for the full implementation
    ///   of the rules
    /// </summary>
    public static readonly IReadOnlyCollection<string> ModifiersOrder = new[]
    {
        "public",
        NoModifierPlacementType,
        "internal",

        // TODO: this might not work (due to being multiple tokens)
        // this is technically an allowed modifier but we don't use this anywhere, see the TODO comment below
        "protected internal",
        "protected",
        "private",
    };

    /// <summary>
    ///   This specifies the order of these types of members that can be contained in a class
    /// </summary>
    public static readonly IReadOnlyCollection<SyntaxKind> SyntaxTypeOrder = new[]
    {
        SyntaxKind.FieldDeclaration,
        SyntaxKind.ConstructorDeclaration,
        SyntaxKind.DestructorDeclaration,
        SyntaxKind.DelegateDeclaration,
        SyntaxKind.EventFieldDeclaration,
        SyntaxKind.InterfaceDeclaration,
        SyntaxKind.EnumDeclaration,
        SyntaxKind.PropertyDeclaration,
        SyntaxKind.ConversionOperatorDeclaration,
        SyntaxKind.OperatorDeclaration,
        SyntaxKind.IndexerDeclaration,
        SyntaxKind.MethodDeclaration,
        SyntaxKind.ClassDeclaration,
        SyntaxKind.StructDeclaration,
    };

    /// <summary>
    ///   Orders some methods specifically based on their names. Lower values are first.
    ///   0 is default when nothing matches.
    /// </summary>
    public static readonly IReadOnlyCollection<(Regex Regex, int Order)> OrderOfMethodNames = new[]
    {
        (new Regex("^_Ready"), -100),
        (new Regex("^ResolveNodeReferences"), -99),
        (new Regex("^ResolveDerivedTypeNodeReferences"), -98),
        (new Regex("^_EnterTree"), -95),
        (new Regex("^_ExitTree"), -94),
        (new Regex("^Init"), -93),
        (new Regex("^_(Physics)?Process"), -7),
        (new Regex("^PreUpdate"), -6),
        (new Regex("^Update$"), -5),
        (new Regex("^PostUpdate"), -4),
        (new Regex("^_Notification"), -3),
        (new Regex("^_Draw"), -2),
        (new Regex("^_.*"), -1),

        (new Regex("^Equals?"), 40),
        (new Regex("^Clone.*"), 50),
        (new Regex("^Dispose"), 90),
        (new Regex("^Get(Visual)?HashCode"), 95),
        (new Regex("(Description|Detail)String$"), 99),
        (new Regex("^ToString"), 100),
    };

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
                    $"{GetLocation(node.Span)} had an interface method with missing access modifiers");

                var (publicToken, returnType) = CopyTriviaForPrependedNode(CreatePublicToken(), node.ReturnType);

                var newNode = SyntaxFactory.MethodDeclaration(node.AttributeLists, new SyntaxTokenList(publicToken),
                    returnType, node.ExplicitInterfaceSpecifier, node.Identifier, node.TypeParameterList,
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
                    $"{GetLocation(node.Span)} had an interface property with missing access modifiers");

                var (publicToken, type) = CopyTriviaForPrependedNode(CreatePublicToken(), node.Type);

                var newNode = SyntaxFactory.PropertyDeclaration(node.AttributeLists, new SyntaxTokenList(publicToken),
                    type, node.ExplicitInterfaceSpecifier, node.Identifier, node.AccessorList, node.ExpressionBody,
                    node.Initializer, node.SemicolonToken);

                return newNode;
            }

            return base.VisitPropertyDeclaration(node);
        }

        /// <summary>
        ///   Handles checking that class members are in the correct order
        /// </summary>
        public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            // Because the SyntaxList builder modifies the node identities, we need to create this temporary list
            // to be able to compare if the order changed or not
            var newOrder = node.Members.Order(new MemberOrderComparer()).ToList();

            // Skip any further processing if the order is the same
            if (newOrder.SequenceEqual(node.Members))
                return base.VisitClassDeclaration(node);

            if (newOrder.Count != node.Members.Count)
                throw new Exception("Member reordering somehow lost a member");

            var reordered = new SyntaxList<MemberDeclarationSyntax>(newOrder);

            runData.OutputTextWithMutex(
                $"{GetLocation(node.Span)} has class \"{node.Identifier.ToString()}\" with members in incorrect order");

            var expected = MemberDeclarationsToTextList(reordered);
            var current = MemberDeclarationsToTextList(node.Members);

            if (expected == current)
            {
                // This indicates a problem in the order comparison
                runData.OutputWarningWithMutex("Expected and current order string representations are the same. " +
                    "This indicates a bug in the formatting tool.");
            }

            runData.OutputTextWithMutex($"Expected order:\n{expected}\nBut current order is:\n{current}");

            runData.OutputTextWithMutex(
                "Note: reordering may not properly move all related comments or pragmas, so please verify " +
                "the reordering is fully correct before committing");

            var newClass = SyntaxFactory.ClassDeclaration(node.AttributeLists, node.Modifiers, node.Keyword,
                node.Identifier, node.TypeParameterList, node.BaseList, node.ConstraintClauses, node.OpenBraceToken,
                reordered, node.CloseBraceToken, node.SemicolonToken);

            return base.VisitClassDeclaration(newClass);
        }

        private static string MemberDeclarationsToTextList(IEnumerable<MemberDeclarationSyntax> members)
        {
            var stringBuilder = new StringBuilder();

            bool first = true;
            foreach (var memberDeclarationSyntax in members)
            {
                if (!first)
                    stringBuilder.Append('\n');

                first = false;

                var name = GetNameFromMemberDeclaration(memberDeclarationSyntax);
                stringBuilder.Append($" - {name}");
            }

            if (stringBuilder.Length < 1)
                stringBuilder.Append(" - all members of unknown type to display names");

            return stringBuilder.ToString();
        }

        private static string GetNameFromMemberDeclaration(MemberDeclarationSyntax memberDeclarationSyntax)
        {
            switch (memberDeclarationSyntax)
            {
                case FieldDeclarationSyntax fieldSyntax:
                    return fieldSyntax.Declaration.Variables.FirstOrDefault()?.Identifier.ToString() ??
                        "unknown variable name";
                case MethodDeclarationSyntax methodSyntax:
                    return methodSyntax.Identifier.ToString();
                case PropertyDeclarationSyntax propertySyntax:
                    return propertySyntax.Identifier.ToString();
                case ClassDeclarationSyntax classSyntax:
                    return classSyntax.Identifier.ToString();
                case StructDeclarationSyntax structSyntax:
                    return structSyntax.Identifier.ToString();
                case InterfaceDeclarationSyntax interfaceSyntax:
                    return interfaceSyntax.Identifier.ToString();
                case DelegateDeclarationSyntax delegateSyntax:
                    return delegateSyntax.Identifier.ToString();
                case ConstructorDeclarationSyntax constructorSyntax:
                    return constructorSyntax.Identifier.ToString();
                case EnumDeclarationSyntax enumDeclaration:
                    return enumDeclaration.Identifier.ToString();
                case EventFieldDeclarationSyntax eventFieldSyntax:
                    return eventFieldSyntax.Declaration.Variables.FirstOrDefault()?.Identifier.ToString() ??
                        "unknown event field name";
                case ConversionOperatorDeclarationSyntax conversionOperatorSyntax:
                    return $"conversion operator {conversionOperatorSyntax.Type}" +
                        $"{conversionOperatorSyntax.ParameterList.ToString()}";
                case OperatorDeclarationSyntax operatorSyntax:
                    return $"operator {operatorSyntax.OperatorToken}{operatorSyntax.ParameterList.ToString()}";
                case IndexerDeclarationSyntax indexerSyntax:
                    return $"{indexerSyntax.ThisKeyword}{indexerSyntax.ParameterList.ToString()}";
                default:
                    return $"Unknown member of kind: {memberDeclarationSyntax.Kind()}";
            }
        }

        private string GetLocation(TextSpan span)
        {
            return $"Line {sourceText.Lines.GetLinePosition(span.Start).Line + 1}";
        }

        private SyntaxToken CreatePublicToken()
        {
            return SyntaxFactory.Token(SyntaxKind.PublicKeyword);
        }

        private (T1 NewNode, T2 OldNode) CopyTriviaForPrependedNode<T1, T2>(T1 newNode, T2 oldNode)
            where T1 : SyntaxNode
            where T2 : SyntaxNode
        {
            var singleSpaceTrivia = SyntaxFactory.SyntaxTrivia(SyntaxKind.WhitespaceTrivia, " ");

            return (newNode.WithLeadingTrivia(oldNode.GetLeadingTrivia()),
                oldNode.WithLeadingTrivia(singleSpaceTrivia));
        }

        private (SyntaxToken NewNode, T2 OldNode) CopyTriviaForPrependedNode<T2>(SyntaxToken newNode, T2 oldNode)
            where T2 : SyntaxNode
        {
            var singleSpaceTrivia = SyntaxFactory.SyntaxTrivia(SyntaxKind.WhitespaceTrivia, " ");

            return (newNode.WithLeadingTrivia(oldNode.GetLeadingTrivia()),
                oldNode.WithLeadingTrivia(singleSpaceTrivia));
        }

        private class MemberOrderComparer : IComparer<MemberDeclarationSyntax>
        {
            public int Compare(MemberDeclarationSyntax? x, MemberDeclarationSyntax? y)
            {
                if (x == null || y == null)
                    throw new ArgumentNullException();

                if (x.Equals(y))
                    return 0;

                // Compare first based on the member type to get the rough sections down
                int xKindIndex = SyntaxTypeOrder.IndexOf(x.Kind());
                int yKindIndex = SyntaxTypeOrder.IndexOf(y.Kind());

                if (xKindIndex != -1 && yKindIndex != -1)
                {
                    if (xKindIndex < yKindIndex)
                        return -1;

                    if (xKindIndex > yKindIndex)
                        return 1;
                }

                // Compare access modifiers next
                int xIndex;
                if (x.Modifiers.Count > 0 && y.Modifiers.Count > 0)
                {
                    // TODO: special case for internal and protected at the same time
                    xIndex = ModifiersOrder.IndexOf(x.Modifiers.Select(m => m.ToString())
                        .FirstOrDefault(m => ModifiersOrder.Contains(m)));
                }
                else
                {
                    // No modifier is by default at this point
                    xIndex = ModifiersOrder.IndexOf(NoModifierPlacementType);
                }

                int yIndex;
                if (y.Modifiers.Count > 0)
                {
                    yIndex = ModifiersOrder.IndexOf(y.Modifiers.Select(m => m.ToString())
                        .FirstOrDefault(m => ModifiersOrder.Contains(m)));
                }
                else
                {
                    yIndex = ModifiersOrder.IndexOf(NoModifierPlacementType);
                }

                if (xIndex < yIndex)
                    return -1;

                if (xIndex > yIndex)
                    return 1;

                // Compare other than access modifiers next
                bool xConst = x.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword));
                bool yConst = y.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword));

                if (xConst && !yConst)
                    return -1;

                if (!xConst && yConst)
                    return 1;

                bool xStatic = x.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword));
                bool yStatic = y.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword));

                if (xStatic && !yStatic)
                    return -1;

                if (!xStatic && yStatic)
                    return 1;

                bool xReadOnly = x.Modifiers.Any(m => m.IsKind(SyntaxKind.ReadOnlyKeyword));
                bool yReadOnly = y.Modifiers.Any(m => m.IsKind(SyntaxKind.ReadOnlyKeyword));

                if (xReadOnly && !yReadOnly)
                    return -1;

                if (!xReadOnly && yReadOnly)
                    return 1;

                // Modifiers are all the same, compare some special names
                if (x is MethodDeclarationSyntax xMethod && y is MethodDeclarationSyntax yMethod)
                {
                    var xName = xMethod.Identifier.ToString();
                    var yName = yMethod.Identifier.ToString();

                    int xPriority = OrderOfMethodNames.FirstOrDefault(t => t.Regex.IsMatch(xName), (null!, 0)).Order;
                    int yPriority = OrderOfMethodNames.FirstOrDefault(t => t.Regex.IsMatch(yName), (null!, 0)).Order;

                    if (xPriority < yPriority)
                        return -1;

                    if (xPriority > yPriority)
                        return 1;
                }

                // Everything we check for is equal
                return 0;
            }
        }
    }
}
