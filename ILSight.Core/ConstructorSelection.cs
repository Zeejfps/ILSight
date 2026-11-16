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

    /// <summary>What the transform marks when it throws: the app's code the decompiler calls back (CD4).</summary>
    public required CallbackFailure Callbacks { get; init; }

    // The type's members, the symbols annotated on them and whether they initialize anything are read of the
    // decompiler's AST before the count, and the nodes chosen are removed by the AST's own code after it (CF9, CJ9): the
    // choice is the counted work.
    public void Run(AstNode rootNode, TransformContext context)
    {
        var members = ReadMembers(rootNode);
        var removed = Callbacks.Run((Kept, members), static s => ChooseRemoved(s.Kept, s.members));
        foreach (var node in removed)
        {
            node.Remove();
        }
    }

    // What the choice needs of a member of the type: its node, its symbol, its sort, and for a constructor whether it
    // chains to another of the type's.
    private sealed record MemberFacts
    {
        public required AstNode Node { get; init; }

        public required ISymbol? Symbol { get; init; }

        public required MemberSort Sort { get; init; }

        public required bool ChainsToThis { get; init; }
    }

    private enum MemberSort
    {
        Constructor,
        Uninitialized,
        Comment,
        Other,
    }

    private static List<MemberFacts> ReadMembers(AstNode rootNode)
    {
        var members = new List<MemberFacts>();
        foreach (var node in rootNode.Children)
        {
            var sort = node switch
            {
                ConstructorDeclaration => MemberSort.Constructor,
                FieldDeclaration fd when fd.Variables.All(static v => v.Initializer is null) => MemberSort.Uninitialized,
                EventDeclaration ed when ed.Variables.All(static v => v.Initializer is null) => MemberSort.Uninitialized,
                PropertyDeclaration { Initializer: null } => MemberSort.Uninitialized,
                CustomEventDeclaration or IndexerDeclaration => MemberSort.Uninitialized,
                Comment => MemberSort.Comment,
                _ => MemberSort.Other,
            };
            var chainsToThis = node is ConstructorDeclaration cd && cd.Initializer?.ConstructorInitializerType == ConstructorInitializerType.This;
            members.Add(new MemberFacts { Node = node, Symbol = node.GetSymbol(), Sort = sort, ChainsToThis = chainsToThis });
        }
        return members;
    }

    // The kept constructor and the members whose initializers it runs stay; the rest, and the comments of what goes,
    // are removed. Symbols are compared by reference, which reads nothing.
    private static List<AstNode> ChooseRemoved(IMethod ctor, List<MemberFacts> members)
    {
        var removed = new List<AstNode>();
        var removedNodes = new HashSet<AstNode>(ReferenceEqualityComparer.Instance);
        var removedSymbols = new HashSet<ISymbol?>();
        MemberFacts? kept = null;
        foreach (var member in members)
        {
            if (member.Sort == MemberSort.Constructor && member.Symbol == ctor)
            {
                kept = member;
            }
            else if (member.Sort is MemberSort.Constructor or MemberSort.Uninitialized)
            {
                Remove(removed, removedNodes, removedSymbols, member);
            }
        }

        if (kept?.ChainsToThis == true)
        {
            foreach (var member in members)
            {
                if (member.Sort != MemberSort.Constructor && !removedNodes.Contains(member.Node))
                {
                    Remove(removed, removedNodes, removedSymbols, member);
                }
            }
        }

        foreach (var member in members)
        {
            if (member.Sort == MemberSort.Comment && !removedNodes.Contains(member.Node) && removedSymbols.Contains(member.Symbol))
            {
                removed.Add(member.Node);
            }
        }
        return removed;
    }

    private static void Remove(List<AstNode> removed, HashSet<AstNode> removedNodes, HashSet<ISymbol?> removedSymbols, MemberFacts member)
    {
        removed.Add(member.Node);
        removedNodes.Add(member.Node);
        removedSymbols.Add(member.Symbol);
    }
}
