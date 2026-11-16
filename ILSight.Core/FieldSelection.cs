// Adapted from ILSpy's CSharpLanguage (MIT License, Copyright (c) AlphaSierraPapa for the SharpDevelop Team).

using System.Reflection.Metadata;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.CSharp.Transforms;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

/// <summary>Keeps only <see cref="Kept"/>. The decompiler's IAstTransform demands <see cref="Run"/>.</summary>
internal sealed class FieldSelection : IAstTransform
{
    public required IField Kept { get; init; }

    /// <summary>What the transform marks when it throws: the app's code the decompiler calls back (CD4).</summary>
    public required CallbackFailure Callbacks { get; init; }

    // The declarations and the symbols annotated on them are read of the decompiler's AST before the count, and the
    // nodes chosen are removed by the AST's own code after it (CF9, CJ9): the choice is the counted work.
    public void Run(AstNode rootNode, TransformContext context)
    {
        var declared = ReadDeclarations(rootNode);
        var removed = Callbacks.Run((Kept, declared), static s => ChooseRemoved(s.Kept, s.declared));
        foreach (var node in removed)
        {
            node.Remove();
        }
    }

    private static List<(AstNode Node, ISymbol? Symbol)> ReadDeclarations(AstNode rootNode)
    {
        var declared = new List<(AstNode Node, ISymbol? Symbol)>();
        foreach (var node in rootNode.Children)
        {
            if (node is EntityDeclaration or Comment)
            {
                declared.Add((node, node.GetSymbol()));
            }
        }
        return declared;
    }

    // Every declaration but the kept field's, compared by reference, which reads nothing.
    private static List<AstNode> ChooseRemoved(IField field, List<(AstNode Node, ISymbol? Symbol)> declared)
    {
        var removed = new List<AstNode>();
        foreach (var (node, symbol) in declared)
        {
            if (symbol != field)
            {
                removed.Add(node);
            }
        }
        return removed;
    }
}
