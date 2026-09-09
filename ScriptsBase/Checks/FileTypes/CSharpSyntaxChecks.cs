namespace ScriptsBase.Checks.FileTypes;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

/// <summary>
///   Runs all registered C# syntax checks on a file parsed into one syntax tree.
/// </summary>
public sealed class CSharpSyntaxChecks : FileCheck
{
    private readonly IReadOnlyList<CSharpSyntaxFileCheck> checks;

    public CSharpSyntaxChecks(params CSharpSyntaxFileCheck[] checks) : base(".cs")
    {
        if (checks.Length < 1)
            throw new ArgumentException("At least one C# syntax check must be specified", nameof(checks));

        this.checks = checks;
    }

    public override async IAsyncEnumerable<string> Handle(string path)
    {
        var bytes = await File.ReadAllBytesAsync(path);
        var sourceText = SourceText.From(bytes, bytes.Length, Encoding.UTF8);
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText, new CSharpParseOptions(), path);

        foreach (var check in checks)
        {
            foreach (var error in check.Handle(path, syntaxTree))
            {
                yield return error;
            }
        }
    }
}

/// <summary>
///   Base class for checks that inspect a parsed C# syntax tree.
/// </summary>
public abstract class CSharpSyntaxFileCheck
{
    public abstract IEnumerable<string> Handle(string path, SyntaxTree syntaxTree);
}
