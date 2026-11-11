using System.Collections.Immutable;
using System.Diagnostics;
using ILSight.Core.Metadata;

namespace ILSight.Core;

/// <summary>A folder the tree lists by its role, not by what it holds.</summary>
public enum NodeFolder
{
    References,
    Resources,
    BaseTypes,
    DerivedTypes,
    ReferencedTypes,
    MetadataTables,
}

/// <summary>Whose metadata a metadata folder shows: the module's own, or its debug metadata.</summary>
public enum MetadataSource
{
    Own,
    Debug,
}

/// <summary>What a node is among its siblings, as a <see cref="NodePath"/> names it: by what it
/// stands for, not by the label its row shows, so that a renamed generic parameter, a table's row count or
/// a class turned record leaves it the same.</summary>
public abstract record NodePathSegment
{
    private NodePathSegment()
    {
    }

    /// <summary>A namespace by its full name; empty for the global namespace.</summary>
    public sealed record Namespace(string Name) : NodePathSegment;

    /// <summary>A type or member the module defines, by its XML documentation id, such as <c>T:Ns.Calculator`1</c>.</summary>
    public sealed record Definition(string Id) : NodePathSegment;

    public sealed record Folder(NodeFolder Role) : NodePathSegment;

    public sealed record Metadata(MetadataSource Which) : NodePathSegment;

    public sealed record MetadataPart(MetadataPartKey Part) : NodePathSegment;

    /// <summary>A reference by the simple name of the assembly it names.</summary>
    public sealed record AssemblyReference(string Name) : NodePathSegment;

    public sealed record ModuleReference(string Name) : NodePathSegment;

    /// <summary>An import by the documentation id of the method that calls into the module.</summary>
    public sealed record ImportedMethod(string Id) : NodePathSegment;

    /// <summary>A type used from a reference by its reflection name: <c>System.Collections.Generic.List`1</c>.</summary>
    public sealed record UsedType(string FullName) : NodePathSegment;

    /// <summary>A member used of a type by the documentation id of the member as its type declares it.</summary>
    public sealed record UsedMember(string Id) : NodePathSegment;

    /// <summary>A forwarded type by its reflection name.</summary>
    public sealed record ForwardedType(string FullName) : NodePathSegment;

    /// <summary>A base type by its reflection name, type arguments included.</summary>
    public sealed record BaseType(string Name) : NodePathSegment;

    /// <summary>A derived type by the documentation id of the type that derives.</summary>
    public sealed record DerivedType(string Id) : NodePathSegment;

    /// <summary>A manifest resource by its name, or an entry of a container by its path there.</summary>
    public sealed record Resource(string Name) : NodePathSegment;

    /// <summary>A folder of a package by its full path there, whatever folders it is merged with.</summary>
    public sealed record PackageFolder(string Path) : NodePathSegment;

    public sealed record PackageFile(string Path) : NodePathSegment;

    /// <summary>An assembly read out of a package, by the full path of its file there.</summary>
    public sealed record EmbeddedModule(string Path) : NodePathSegment;

    /// <summary>Whether this names an entry of a package by its full path, from the package's root.</summary>
    internal bool IsPackageEntry => this is PackageFolder or PackageFile or EmbeddedModule;
}

/// <summary>One step of a <see cref="NodePath"/>: what the node is, and the label it showed when
/// the path was taken, which describes the step but does not name it.</summary>
public readonly record struct NodePathStep(NodePathSegment Segment, string Label);

/// <summary>
/// Where a <see cref="BrowseNode"/> sits: the listed file whose tree it is in, and the steps down from that
/// tree's root, naming each node by what it is. Unlike the node, it stays meaningful after the assembly is
/// closed or reloaded; <see cref="BrowseSessions.FindNode(BrowseSession, LoadedAssembly, NodePath)"/> finds the node it names again.
/// </summary>
public sealed class NodePath : IEquatable<NodePath>
{
    /// <summary>The absolute path of the listed file the path starts from: an assembly, or the package
    /// an assembly was read out of.</summary>
    public required AssemblyPath FilePath { get; init; }

    /// <summary>What each node down from the root is, and its label, the node itself last.</summary>
    public required ImmutableArray<NodePathStep> Steps { get; init; }

    /// <summary>The label of the node this names, as it showed when the path was taken; the file's name
    /// for the root.</summary>
    public string Label => Steps.Length == 0 ? FilePath.FileName : Steps[^1].Label;

    /// <summary>Equal when it starts from the same file and names the same node at each step, whatever
    /// the labels.</summary>
    public bool Equals(NodePath? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is null)
        {
            return false;
        }

        if (other.Steps.Length != Steps.Length)
        {
            return false;
        }

        if (!FilePath.Equals(other.FilePath))
        {
            return false;
        }

        for (var i = Steps.Length - 1; i >= 0; i--)
        {
            if (Steps[i].Segment != other.Steps[i].Segment)
            {
                return false;
            }
        }
        return true;
    }

    public override bool Equals(object? obj)
    {
        return obj is NodePath other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(FilePath);
        foreach (var step in Steps)
        {
            hash.Add(step.Segment);
        }

        return hash.ToHashCode();
    }

    public override string ToString()
    {
        return NodeDefinitions.DescribePath(this);
    }
}
