using System.Diagnostics;
using ILSight.Core.Metadata;

namespace ILSight.Core;

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

    /// <summary>A module the search for derived types reached that the library could not read, by its file's path.</summary>
    public sealed record UnreadableModule(string Path) : NodePathSegment;

    /// <summary>A manifest resource by its name, or an entry of a container by its path there.</summary>
    public sealed record Resource(string Name) : NodePathSegment;

    /// <summary>A folder of a package by its full path there, whatever folders it is merged with.</summary>
    public sealed record PackageFolder(string Path) : NodePathSegment;

    public sealed record PackageFile(string Path) : NodePathSegment;

    /// <summary>An assembly read out of a package, by the full path of its file there.</summary>
    public sealed record EmbeddedModule(string Path) : NodePathSegment;

    /// <summary>Whether this names an entry of a package by its full path, from the package's root.</summary>
    internal bool IsPackageEntry => this is PackageFolder or PackageFile or EmbeddedModule;

    /// <summary>What the handler of whichever case this is makes of it.</summary>
    public TResult Match<TResult>(Func<Namespace, TResult> @namespace, Func<Definition, TResult> definition,
        Func<Folder, TResult> folder, Func<Metadata, TResult> metadata, Func<MetadataPart, TResult> metadataPart,
        Func<AssemblyReference, TResult> assemblyReference, Func<ModuleReference, TResult> moduleReference,
        Func<ImportedMethod, TResult> importedMethod, Func<UsedType, TResult> usedType,
        Func<UsedMember, TResult> usedMember, Func<ForwardedType, TResult> forwardedType,
        Func<BaseType, TResult> baseType, Func<DerivedType, TResult> derivedType,
        Func<UnreadableModule, TResult> unreadableModule, Func<Resource, TResult> resource, Func<PackageFolder, TResult> packageFolder, Func<PackageFile, TResult> packageFile,
        Func<EmbeddedModule, TResult> embeddedModule)
    {
        return this switch
        {
            Namespace c => @namespace(c),
            Definition c => definition(c),
            Folder c => folder(c),
            Metadata c => metadata(c),
            MetadataPart c => metadataPart(c),
            AssemblyReference c => assemblyReference(c),
            ModuleReference c => moduleReference(c),
            ImportedMethod c => importedMethod(c),
            UsedType c => usedType(c),
            UsedMember c => usedMember(c),
            ForwardedType c => forwardedType(c),
            BaseType c => baseType(c),
            DerivedType c => derivedType(c),
            UnreadableModule c => unreadableModule(c),
            Resource c => resource(c),
            PackageFolder c => packageFolder(c),
            PackageFile c => packageFile(c),
            EmbeddedModule c => embeddedModule(c),
            _ => throw new UnreachableException($"NodePathSegment.Match does not handle {GetType().Name}."),
        };
    }
}
