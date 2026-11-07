// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.Diagrams;

/// <summary>The types of a diagram, each as a mermaid class definition of its own members, with
/// properties holding diagram types drawn as relations instead, and the members it inherits kept
/// apart by the type declaring them, so the diagrammer can leave out those a base type it draws
/// already shows.</summary>
internal sealed class DiagramTypes
{
    public required DiagramNames Names { get; init; }

    public required DiagramDocs Docs { get; init; }

    // The settings compiler-generated members are recognised by, among them automatic properties'
    // backing fields.
    public readonly DecompilerSettings Settings = new();
    public required MermaidMembers Members { get; init; }

    public readonly record struct ManyRelation(IProperty Property, IType ElementType);

    /// <summary>A type's members as the diagram splits them, each kind by the type declaring it:
    /// plain properties, properties holding one or many diagram types, methods and fields.</summary>
    public sealed record DeclaredMembers(
        Dictionary<IType, IProperty[]> FlatProperties,
        Dictionary<IType, IProperty[]> HasOne,
        Dictionary<IType, ManyRelation[]> HasMany,
        Dictionary<IType, IMethod[]> Methods,
        Dictionary<IType, IField[]> Fields);
}
