using System.Diagnostics;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

/// <summary>The declared accessibility of the entity a <see cref="BrowseNode"/> stands for.</summary>
public enum NodeAccessibility
{
    None,
    Public,
    Protected,
    ProtectedInternal,
    Internal,
    PrivateProtected,
    Private,
}

/// <summary>
/// A node of the browse tree: what it stands for and where it sits. Its children are built and kept by
/// a <see cref="BrowseSession"/>, so the same entity is the same node object for as long as its
/// <see cref="Assembly"/> lives: make one only through <see cref="BrowseNodes"/>, as the session does. All
/// of <see cref="BrowseNodes"/> is thread-safe, and a node changes after it is made only as
/// <see cref="PathMemo"/> and <see cref="SegmentMemo"/> fill.
/// </summary>
public sealed class BrowseNode
{
    /// <summary>Where this node sits, once <see cref="BrowseNodes.GetPath"/> worked it out.</summary>
    internal NodePath? PathMemo;

    /// <summary>What the node is among its siblings, once <see cref="BrowseNodes.GetSegment"/> worked it out.</summary>
    internal NodePathSegment? SegmentMemo;

    /// <summary>The label: a name, or for members an ILSpy-style signature such as <c>Add(T) : void</c>.</summary>
    public required string Text { get; init; }

    /// <summary>For a namespace, the one whose name it extends by a segment, which holds it when the
    /// tree nests namespaces; null for an outermost namespace and for any other node.</summary>
    public required BrowseNode? EnclosingNamespace { get; init; }

    public required BrowseNode? Parent { get; init; }

    /// <summary>The root of the tree the node is in: the listed assembly, package or file it was found
    /// under, whose <see cref="Assembly"/> differs from the node's under a reference's references.</summary>
    public BrowseNode Root
    {
        get
        {
            var root = this;
            while (root.Parent != null)
                root = root.Parent;
            return root;
        }
    }

    /// <summary>The assembly the node is read from.</summary>
    public required LoadedAssembly Assembly { get; init; }

    /// <summary>What the node stands for: its label, flags and children all follow from that.</summary>
    public required NodeSubject Subject { get; init; }

    /// <summary>The module the node is read from; null for a node of a package's folders and files.</summary>
    internal LoadedModule? SourceModule => Assembly as LoadedModule;

    /// <summary>Whether this node stands for a resource's bytes, which <see cref="BrowseNodes.ReadResource"/>
    /// reads, to save them.</summary>
    public bool HasResource => Subject is NodeSubject.ResourceContainer or NodeSubject.ResourceEntry or NodeSubject.PackageFile;

    /// <summary>The folder of a package the node lists, whose files can be extracted: the root folder for
    /// a package's own node; null for a node that lists none.</summary>
    public PackageFolderPlace? FolderInPackage => Subject switch
    {
        NodeSubject.Package package => new PackageFolderPlace(package.Value.Package, package.Value.Folder),
        NodeSubject.TypeDefinition or NodeSubject.MemberDefinition or NodeSubject.References or NodeSubject.AssemblyReference
            or NodeSubject.ReferencedTypes or NodeSubject.TypeReference or NodeSubject.MemberReference or NodeSubject.ModuleReference
            or NodeSubject.ImportedMethod or NodeSubject.ForwardedType or NodeSubject.BaseTypes or NodeSubject.BaseType
            or NodeSubject.DerivedTypes or NodeSubject.DerivedType or NodeSubject.Resources or NodeSubject.ResourceContainer
            or NodeSubject.ResourceEntry or NodeSubject.Metadata or NodeSubject.MetadataTables or NodeSubject.MetadataTable
            or NodeSubject.PackageFile or NodeSubject.Module or NodeSubject.Namespace or NodeSubject.FailedAssembly => null,
        _ => throw new UnreachableException(),
    };

    /// <summary>The type or member the node defines; null for a node of anything else.</summary>
    internal IEntity? Entity => Subject switch
    {
        NodeSubject.TypeDefinition type => type.Definition.Symbol,
        NodeSubject.MemberDefinition member => member.Definition.Symbol,
        NodeSubject.References or NodeSubject.AssemblyReference or NodeSubject.ReferencedTypes or NodeSubject.TypeReference
            or NodeSubject.MemberReference or NodeSubject.ModuleReference or NodeSubject.ImportedMethod or NodeSubject.ForwardedType
            or NodeSubject.BaseTypes or NodeSubject.BaseType or NodeSubject.DerivedTypes or NodeSubject.DerivedType or NodeSubject.Resources
            or NodeSubject.ResourceContainer or NodeSubject.ResourceEntry or NodeSubject.Metadata or NodeSubject.MetadataTables
            or NodeSubject.MetadataTable or NodeSubject.Package or NodeSubject.PackageFile or NodeSubject.Module or NodeSubject.Namespace or NodeSubject.FailedAssembly => null,
        _ => throw new UnreachableException(),
    };

    public override string ToString() => Text;
}
