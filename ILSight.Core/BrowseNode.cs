using ICSharpCode.Decompiler.CSharp.ProjectDecompiler;
using System.Reflection.Metadata.Ecma335;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

/// <summary>
/// A node of the browse tree: what it stands for and where it sits, and the values it shows, read from the library's
/// objects when it was made. Its children are built and kept by a <see cref="BrowseSession"/>, so the same entity is
/// the same node object for as long as its <see cref="Assembly"/> lives: make one only through
/// <see cref="BrowseNodes"/>, as the session does. A node never changes after it is made.
/// </summary>
public sealed class BrowseNode
{
    /// <summary>Where this node sits, for finding it again after its assembly is reloaded.</summary>
    public required NodePath Path { get; init; }

    /// <summary>What the node is among its siblings, as a path names it; null for the root of a tree.</summary>
    public required NodePathSegment? Segment { get; init; }

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
            while (root.Parent is not null)
            {
                root = root.Parent;
            }

            return root;
        }
    }

    /// <summary>The assembly the node is read from.</summary>
    public required LoadedAssembly Assembly { get; init; }

    /// <summary>What the node stands for: its label, flags and children all follow from that.</summary>
    public required NodeSubject Subject { get; init; }

    /// <summary>A qualified name for copying, e.g. <c>System.Collections.Generic.List&lt;T&gt;.Add(T)</c>, worked out
    /// when the node was made.</summary>
    public required string FullName { get; init; }

    /// <summary>How visible what the node stands for is, worked out when the node was made.</summary>
    public required NodeVisibility Visibility { get; init; }

    /// <summary>The type or member the node defines; null for a node that defines none.</summary>
    public required CodeReference.Entity? Reference { get; init; }

    /// <summary>The metadata token of the type or member the node defines; null for a node that defines none.</summary>
    public int? Token => Reference is null ? null : MetadataTokens.GetToken(Reference.Handle);

    /// <summary>What activating the node does, worked out when the node was made.</summary>
    public required Activation Activation { get; init; }

    /// <summary>What analyses run on for the node: the type or member it stands for, or for a base or derived type
    /// entry the type it names; null for any other node. Read when the node was made.</summary>
    public required Analysis.AnalysisSymbol? Analysis { get; init; }

    /// <summary>Whether the node may have children, guessed when it was made without building them.</summary>
    public required bool CanHaveChildren { get; init; }

    /// <summary>The name of what the node stands for: a module's assembly's, a type's or member's own (without its
    /// signature), else the node's label.</summary>
    public required string Name { get; init; }

    /// <summary>Whether the node's content is decompiled code, which changes with the language and options, rather
    /// than a grid, a picture or why its file failed to load, which do not.</summary>
    public required bool IsCode { get; init; }

    /// <summary>The file name a resource the node stands for is saved under; null for a node of anything else.</summary>
    public required string? ResourceFileName { get; init; }

    /// <summary>The name ILSpy's tree gives the node standing for this one (<see cref="ILSpyTree.NameOf"/>); null for a
    /// node it has no named counterpart of. Read when the node was made.</summary>
    public required string? ILSpyName { get; init; }

    /// <summary>The ILSpy full name of the type or member the node defines (<c>Namespace.Type.Member</c>); null for a
    /// node that defines none. Read when the node was made.</summary>
    public required string? EntityFullName { get; init; }

    /// <summary>The file name the node's code is saved under in <paramref name="language"/>.</summary>
    public string SuggestCodeFileName(DecompilationLanguage language)
    {
        var extension = DecompilationLanguages.IsIL(language) ? ".il" : ".cs";
        return WholeProjectDecompiler.CleanUpFileName(Name, extension);
    }

    /// <summary>The module the node is read from; null for a node of a package's folders and files.</summary>
    internal LoadedModule? SourceModule => Assembly as LoadedModule;

    /// <summary>Whether this node stands for a resource's bytes, which <see cref="BrowseNodes.ReadResource"/>
    /// reads, to save them.</summary>
    public bool HasResource => Subject is NodeSubject.ResourceContainer or NodeSubject.ResourceEntry or NodeSubject.PackageFile;

    /// <summary>The folder of a package the node lists, whose files can be extracted: the root folder for
    /// a package's own node; null for a node that lists none.</summary>
    public PackageFolderPlace? FolderInPackage => Subject.Match<PackageFolderPlace?>(
        module: static _ => null,
        @namespace: static _ => null,
        failedAssembly: static _ => null,
        typeDefinition: static _ => null,
        memberDefinition: static _ => null,
        references: static _ => null,
        assemblyReference: static _ => null,
        referencedTypes: static _ => null,
        typeReference: static _ => null,
        memberReference: static _ => null,
        moduleReference: static _ => null,
        importedMethod: static _ => null,
        forwardedType: static _ => null,
        baseTypes: static _ => null,
        baseType: static _ => null,
        derivedTypes: static _ => null,
        derivedType: static _ => null,
        resources: static _ => null,
        resourceContainer: static _ => null,
        resourceEntry: static _ => null,
        metadata: static _ => null,
        metadataTables: static _ => null,
        metadataTable: static _ => null,
        package: static package => new PackageFolderPlace
        {
            Container = package.Value.Package,
            Folder = package.Value.Folder,
        },
        packageFile: static _ => null);

    /// <summary>The type or member the node defines; null for a node of anything else.</summary>
    internal IEntity? Entity => Subject.Match<IEntity?>(
        module: static _ => null,
        @namespace: static _ => null,
        failedAssembly: static _ => null,
        typeDefinition: static type => NodeDefinitions.GetSymbol(type.Definition),
        memberDefinition: static member => NodeDefinitions.GetSymbol(member.Definition),
        references: static _ => null,
        assemblyReference: static _ => null,
        referencedTypes: static _ => null,
        typeReference: static _ => null,
        memberReference: static _ => null,
        moduleReference: static _ => null,
        importedMethod: static _ => null,
        forwardedType: static _ => null,
        baseTypes: static _ => null,
        baseType: static _ => null,
        derivedTypes: static _ => null,
        derivedType: static _ => null,
        resources: static _ => null,
        resourceContainer: static _ => null,
        resourceEntry: static _ => null,
        metadata: static _ => null,
        metadataTables: static _ => null,
        metadataTable: static _ => null,
        package: static _ => null,
        packageFile: static _ => null);

    public override string ToString()
    {
        return Text;
    }
}
