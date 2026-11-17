using ILSight.Core.MetadataArea;

namespace ILSight.Core;

/// <summary>What a node is among its siblings, as a <see cref="NodePath"/> names it: by what it
/// stands for, not by the label its row shows, so that a renamed generic parameter, a table's row count or
/// a class turned record leaves it the same.</summary>
public union NodePathSegment(NodePathSegment.Namespace, NodePathSegment.Definition, NodePathSegment.Folder, NodePathSegment.Metadata,
    NodePathSegment.MetadataPart, NodePathSegment.AssemblyReference, NodePathSegment.ModuleReference, NodePathSegment.ImportedMethod,
    NodePathSegment.UsedType, NodePathSegment.UsedMember, NodePathSegment.ForwardedType, NodePathSegment.BaseType, NodePathSegment.DerivedType,
    NodePathSegment.UnreadableModule, NodePathSegment.Resource, NodePathSegment.PackageFolder, NodePathSegment.PackageFile,
    NodePathSegment.EmbeddedModule)
{
    /// <summary>A namespace by its full name; empty for the global namespace.</summary>
    public sealed record Namespace(string Name);

    /// <summary>A type or member the module defines, by its XML documentation id, such as <c>T:Ns.Calculator`1</c>.</summary>
    public sealed record Definition(string Id);

    public sealed record Folder(NodeFolder Role);

    public sealed record Metadata(MetadataSource Which);

    public sealed record MetadataPart(MetadataPartKey Part);

    /// <summary>A reference by the simple name of the assembly it names.</summary>
    public sealed record AssemblyReference(string Name);

    public sealed record ModuleReference(string Name);

    /// <summary>An import by the documentation id of the method that calls into the module.</summary>
    public sealed record ImportedMethod(string Id);

    /// <summary>A type used from a reference by its reflection name: <c>System.Collections.Generic.List`1</c>.</summary>
    public sealed record UsedType(string FullName);

    /// <summary>A member used of a type by the documentation id of the member as its type declares it.</summary>
    public sealed record UsedMember(string Id);

    /// <summary>A forwarded type by its reflection name.</summary>
    public sealed record ForwardedType(string FullName);

    /// <summary>A base type by its reflection name, type arguments included.</summary>
    public sealed record BaseType(string Name);

    /// <summary>A derived type by the documentation id of the type that derives.</summary>
    public sealed record DerivedType(string Id);

    /// <summary>A module the search for derived types reached that the library could not read, by its file's path.</summary>
    public sealed record UnreadableModule(string Path);

    /// <summary>A manifest resource by its name, or an entry of a container by its path there.</summary>
    public sealed record Resource(string Name);

    /// <summary>A folder of a package by its full path there, whatever folders it is merged with.</summary>
    public sealed record PackageFolder(string Path);

    public sealed record PackageFile(string Path);

    /// <summary>An assembly read out of a package, by the full path of its file there.</summary>
    public sealed record EmbeddedModule(string Path);

    /// <summary>Whether this names an entry of a package by its full path, from the package's root.</summary>
    internal bool IsPackageEntry => this switch
    {
        PackageFolder or PackageFile or EmbeddedModule => true,
        Namespace or Definition or Folder or Metadata or MetadataPart or AssemblyReference or ModuleReference or ImportedMethod or UsedType
            or UsedMember or ForwardedType or BaseType or DerivedType or UnreadableModule or Resource => false,
    };
}
