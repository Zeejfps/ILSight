using ILSight.Core.Metadata;
using ILSight.Core.Resources;

namespace ILSight.Core;

/// <summary>What a node of the browse tree stands for.</summary>
public abstract class NodeSubject
{
    private NodeSubject()
    {
    }

    /// <summary>A module that was read: an assembly, or a WebCIL module; the root of its tree.</summary>
    public sealed class Module : NodeSubject
    {
        public required LoadedModule Value { get; init; }
    }

    public sealed class Namespace : NodeSubject
    {
        public required NamespaceInfo Value { get; init; }
    }

    /// <summary>A listed file that could not be read as a .NET assembly; the root of its tree.</summary>
    public sealed class FailedAssembly : NodeSubject
    {
        public required Core.FailedAssembly Value { get; init; }
    }

    public sealed class TypeDefinition : NodeSubject
    {
        public required DefinedType Definition { get; init; }
    }

    /// <summary>A field, property, event or method, or the accessor of a property or event.</summary>
    public sealed class MemberDefinition : NodeSubject
    {
        public required DefinedMember Definition { get; init; }
    }

    /// <summary>The folder of the assemblies and modules <see cref="Owner"/> references.</summary>
    public sealed class References : NodeSubject
    {
        public required LoadedModule Owner { get; init; }
    }

    public sealed class AssemblyReference : NodeSubject
    {
        public required AssemblyReferenceInfo Value { get; init; }
    }

    /// <summary>The folder of the types a module uses from the assembly <see cref="Reference"/> names.</summary>
    public sealed class ReferencedTypes : NodeSubject
    {
        public required AssemblyReferenceInfo Reference { get; init; }
    }

    public sealed class TypeReference : NodeSubject
    {
        public required TypeReferenceInfo Value { get; init; }
    }

    public sealed class MemberReference : NodeSubject
    {
        public required MemberReferenceInfo Value { get; init; }
    }

    public sealed class ModuleReference : NodeSubject
    {
        public required ModuleReferenceInfo Value { get; init; }
    }

    public sealed class ImportedMethod : NodeSubject
    {
        public required ImportedMethodInfo Value { get; init; }
    }

    public sealed class ForwardedType : NodeSubject
    {
        public required ForwardedTypeInfo Value { get; init; }
    }

    /// <summary>The folder of the types <see cref="Type"/> derives from directly.</summary>
    public sealed class BaseTypes : NodeSubject
    {
        public required DefinedType Type { get; init; }
    }

    public sealed class BaseType : NodeSubject
    {
        public required BaseTypeInfo Value { get; init; }
    }

    /// <summary>The folder of the types of the open assemblies that derive from <see cref="Type"/> directly.</summary>
    public sealed class DerivedTypes : NodeSubject
    {
        public required DefinedType Type { get; init; }
    }

    public sealed class DerivedType : NodeSubject
    {
        public required DerivedTypeInfo Value { get; init; }
    }

    /// <summary>The folder of <see cref="Owner"/>'s manifest resources.</summary>
    public sealed class Resources : NodeSubject
    {
        public required LoadedModule Owner { get; init; }
    }

    /// <summary>A manifest resource that holds entries: a <c>.resources</c> file, or Avalonia's asset container.</summary>
    public sealed class ResourceContainer : NodeSubject
    {
        public required ResourceContainerInfo Value { get; init; }
    }

    public sealed class ResourceEntry : NodeSubject
    {
        public required ResourceEntryInfo Value { get; init; }
    }

    /// <summary>The raw CLI metadata of a file: a module's own, or its debug metadata.</summary>
    public sealed class Metadata : NodeSubject
    {
        public required MetadataFolderInfo Value { get; init; }
    }

    /// <summary>The folder of the tables of the metadata <see cref="Folder"/> stands for.</summary>
    public sealed class MetadataTables : NodeSubject
    {
        public required MetadataFolderInfo Folder { get; init; }
    }

    /// <summary>A part of a metadata folder: a header, directory, table or heap, or a debug directory entry.</summary>
    public sealed class MetadataTable : NodeSubject
    {
        public required MetadataTableInfo Value { get; init; }
    }

    /// <summary>A listed package or bundle, standing for its root folder, or a folder in it.</summary>
    public sealed class Package : NodeSubject
    {
        public required PackageFolderInfo Value { get; init; }
    }

    /// <summary>A file of a package that is no assembly.</summary>
    public sealed class PackageFile : NodeSubject
    {
        public required PackageFileInfo Value { get; init; }
    }
}
