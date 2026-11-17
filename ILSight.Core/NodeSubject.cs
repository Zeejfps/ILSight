using ILSight.Core.MetadataArea;
using ILSight.Core.ResourceArea;

namespace ILSight.Core;

/// <summary>What a node of the browse tree stands for. Its cases are classes, compared by reference, as they were
/// before it was a union.</summary>
public union NodeSubject(NodeSubject.Module, NodeSubject.Namespace, NodeSubject.FailedAssembly,
    NodeSubject.TypeDefinition, NodeSubject.MemberDefinition, NodeSubject.References, NodeSubject.AssemblyReference,
    NodeSubject.ReferencedTypes, NodeSubject.TypeReference, NodeSubject.MemberReference, NodeSubject.ModuleReference,
    NodeSubject.ImportedMethod, NodeSubject.ForwardedType, NodeSubject.BaseTypes, NodeSubject.BaseType,
    NodeSubject.DerivedTypes, NodeSubject.DerivedType, NodeSubject.UnreadableModule, NodeSubject.Resources,
    NodeSubject.ResourceContainer, NodeSubject.ResourceEntry, NodeSubject.Metadata, NodeSubject.MetadataTables,
    NodeSubject.MetadataTable, NodeSubject.Package, NodeSubject.PackageFile)
{
    /// <summary>A module that was read: an assembly, or a WebCIL module; the root of its tree.</summary>
    public sealed class Module
    {
        public required LoadedModule Value { get; init; }
    }

    public sealed class Namespace
    {
        public required NamespaceInfo Value { get; init; }
    }

    /// <summary>A listed file that could not be read as a .NET assembly; the root of its tree.</summary>
    public sealed class FailedAssembly
    {
        public required Core.FailedAssembly Value { get; init; }
    }

    public sealed class TypeDefinition
    {
        public required DefinedType Definition { get; init; }
    }

    /// <summary>A field, property, event or method, or the accessor of a property or event.</summary>
    public sealed class MemberDefinition
    {
        public required DefinedMember Definition { get; init; }
    }

    /// <summary>The folder of the assemblies and modules <see cref="Owner"/> references.</summary>
    public sealed class References
    {
        public required LoadedModule Owner { get; init; }
    }

    public sealed class AssemblyReference
    {
        public required AssemblyReferenceInfo Value { get; init; }
    }

    /// <summary>The folder of the types a module uses from the assembly <see cref="Reference"/> names.</summary>
    public sealed class ReferencedTypes
    {
        public required AssemblyReferenceInfo Reference { get; init; }
    }

    public sealed class TypeReference
    {
        public required TypeReferenceInfo Value { get; init; }
    }

    public sealed class MemberReference
    {
        public required MemberReferenceInfo Value { get; init; }
    }

    public sealed class ModuleReference
    {
        public required ModuleReferenceInfo Value { get; init; }
    }

    public sealed class ImportedMethod
    {
        public required ImportedMethodInfo Value { get; init; }
    }

    public sealed class ForwardedType
    {
        public required ForwardedTypeInfo Value { get; init; }
    }

    /// <summary>The folder of the types <see cref="Type"/> derives from directly.</summary>
    public sealed class BaseTypes
    {
        public required DefinedType Type { get; init; }
    }

    public sealed class BaseType
    {
        public required BaseTypeInfo Value { get; init; }
    }

    /// <summary>The folder of the types of the open assemblies that derive from <see cref="Type"/> directly.</summary>
    public sealed class DerivedTypes
    {
        public required DefinedType Type { get; init; }
    }

    public sealed class DerivedType
    {
        public required DerivedTypeInfo Value { get; init; }
    }

    /// <summary>A module the search for a type's derived types reached that the library could not read, named under the
    /// folder beside the types found in the others, with why.</summary>
    public sealed class UnreadableModule
    {
        public required Core.UnreadableModule Value { get; init; }
    }

    /// <summary>The folder of <see cref="Owner"/>'s manifest resources.</summary>
    public sealed class Resources
    {
        public required LoadedModule Owner { get; init; }
    }

    /// <summary>A manifest resource that holds entries: a <c>.resources</c> file, or Avalonia's asset container.</summary>
    public sealed class ResourceContainer
    {
        public required ResourceContainerInfo Value { get; init; }
    }

    public sealed class ResourceEntry
    {
        public required ResourceEntryInfo Value { get; init; }
    }

    /// <summary>The raw CLI metadata of a file: a module's own, or its debug metadata.</summary>
    public sealed class Metadata
    {
        public required MetadataFolderInfo Value { get; init; }
    }

    /// <summary>The folder of the tables of the metadata <see cref="Folder"/> stands for.</summary>
    public sealed class MetadataTables
    {
        public required MetadataFolderInfo Folder { get; init; }
    }

    /// <summary>A part of a metadata folder: a header, directory, table or heap, or a debug directory entry.</summary>
    public sealed class MetadataTable
    {
        public required MetadataTableInfo Value { get; init; }
    }

    /// <summary>A listed package or bundle, standing for its root folder, or a folder in it.</summary>
    public sealed class Package
    {
        public required PackageFolderInfo Value { get; init; }
    }

    /// <summary>A file of a package that is no assembly.</summary>
    public sealed class PackageFile
    {
        public required PackageFileInfo Value { get; init; }
    }
}
