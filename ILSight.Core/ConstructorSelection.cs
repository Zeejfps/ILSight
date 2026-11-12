// Adapted from ILSpy's CSharpLanguage (MIT License, Copyright (c) AlphaSierraPapa for the SharpDevelop Team).

using System.Reflection.Metadata;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.CSharp.Transforms;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

/// <summary>Keeps <see cref="Kept"/> and the members whose initializers it runs. The decompiler's
/// IAstTransform demands <see cref="Run"/>.</summary>
internal sealed class ConstructorSelection : IAstTransform
{
    public required IMethod Kept { get; init; }

    public void Run(AstNode rootNode, TransformContext context)
    {
        KeepConstructor(Kept, rootNode);
    }

    private static void KeepConstructor(IMethod ctor, AstNode rootNode)
    {
        var removed = new HashSet<ISymbol?>();
        ConstructorDeclaration? kept = null;
        foreach (var node in rootNode.Children)
        {
            switch (node)
            {
                case ConstructorDeclaration cd:
                    var constructor = cd.GetSymbol();
                    if (constructor == ctor)
                    {
                        kept = cd;
                    }
                    else
                    {
                        Remove(removed, cd);
                    }

                    break;
                case FieldDeclaration fd when fd.Variables.All(static v => v.Initializer is null):
                    Remove(removed, fd);
                    break;
                case EventDeclaration ed when ed.Variables.All(static v => v.Initializer is null):
                    Remove(removed, ed);
                    break;
                case PropertyDeclaration { Initializer: null } pd:
                    Remove(removed, pd);
                    break;
                case CustomEventDeclaration or IndexerDeclaration:
                    Remove(removed, node);
                    break;
            }
        }
        if (kept?.Initializer?.ConstructorInitializerType == ConstructorInitializerType.This)
        {
            foreach (var node in rootNode.Children)
            {
                if (node is not ConstructorDeclaration)
                {
                    Remove(removed, node);
                }
            }
        }
        foreach (var node in rootNode.Children)
        {
            if (node is not Comment)
            {
                continue;
            }

            var symbol = node.GetSymbol();
            if (removed.Contains(symbol))
            {
                node.Remove();
            }
        }
    }

    private static void Remove(HashSet<ISymbol?> removed, AstNode node)
    {
        node.Remove();
        removed.Add(node.GetSymbol());
    }
}
