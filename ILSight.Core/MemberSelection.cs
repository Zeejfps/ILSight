// Adapted from ILSpy's CSharpLanguage (MIT License, Copyright (c) AlphaSierraPapa for the SharpDevelop Team).

using System.Reflection.Metadata;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.CSharp.Transforms;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

/// <summary>
/// How ILSpy shows a constructor or a field on its own: the type's fields, events, properties and
/// constructors of the same staticness are decompiled together, so initializers moved into a
/// constructor come back out, and then everything but the one asked for is removed.
/// </summary>
internal static class MemberSelection
{
    public static List<EntityHandle> FieldsAndConstructors(ITypeDefinition type, bool isStatic)
    {
        var members = new List<EntityHandle>();
        foreach (var field in type.Fields)
        {
            if (!field.MetadataToken.IsNil && field.IsStatic == isStatic)
            {
                members.Add(field.MetadataToken);
            }
        }
        foreach (var e in type.Events)
        {
            if (!e.MetadataToken.IsNil && e.IsStatic == isStatic)
            {
                members.Add(e.MetadataToken);
            }
        }
        foreach (var p in type.Properties)
        {
            if (!p.MetadataToken.IsNil && p.IsStatic == isStatic && !p.IsIndexer)
            {
                members.Add(p.MetadataToken);
            }
        }
        foreach (var ctor in type.Methods)
        {
            if (!ctor.MetadataToken.IsNil && ctor.IsConstructor && ctor.IsStatic == isStatic)
            {
                members.Add(ctor.MetadataToken);
            }
        }
        return members;
    }

    /// <summary>Keeps <see cref="Kept"/> and the members whose initializers it runs. The decompiler's
    /// IAstTransform demands <see cref="Run"/>.</summary>
    public sealed class Constructor : IAstTransform
    {
        public required IMethod Kept { get; init; }

        public void Run(AstNode rootNode, TransformContext context)
        {
            KeepConstructor(Kept, rootNode);
        }
    }

    /// <summary>Keeps only <see cref="Kept"/>. The decompiler's IAstTransform demands <see cref="Run"/>.</summary>
    public sealed class Field : IAstTransform
    {
        public required IField Kept { get; init; }

        public void Run(AstNode rootNode, TransformContext context)
        {
            KeepField(Kept, rootNode);
        }
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
                    if (cd.GetSymbol() == ctor)
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
            if (node is Comment && removed.Contains(node.GetSymbol()))
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

    private static void KeepField(IField field, AstNode rootNode)
    {
        foreach (var node in rootNode.Children)
        {
            if (node is EntityDeclaration or Comment && node.GetSymbol() != field)
            {
                node.Remove();
            }
        }
    }
}
