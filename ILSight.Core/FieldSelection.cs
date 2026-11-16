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

    public void Run(AstNode rootNode, TransformContext context)
    {
        Callbacks.Run((Kept, rootNode), static s => KeepField(s.Kept, s.rootNode));
    }

    private static void KeepField(IField field, AstNode rootNode)
    {
        foreach (var node in rootNode.Children)
        {
            if (node is not (EntityDeclaration or Comment))
            {
                continue;
            }

            var symbol = node.GetSymbol();
            if (symbol != field)
            {
                node.Remove();
            }
        }
    }
}
