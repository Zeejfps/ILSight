using System.Diagnostics;
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

    /// <summary>A module the search for a type's derived types reached that the library could not read, named under the
    /// folder beside the types found in the others, with why.</summary>
    public sealed class UnreadableModule : NodeSubject
    {
        public required Core.UnreadableModule Value { get; init; }
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

    /// <summary>What the handler of whichever case this is makes of it.</summary>
    public TResult Match<TResult>(Func<Module, TResult> module, Func<Namespace, TResult> @namespace,
        Func<FailedAssembly, TResult> failedAssembly, Func<TypeDefinition, TResult> typeDefinition,
        Func<MemberDefinition, TResult> memberDefinition, Func<References, TResult> references,
        Func<AssemblyReference, TResult> assemblyReference, Func<ReferencedTypes, TResult> referencedTypes,
        Func<TypeReference, TResult> typeReference, Func<MemberReference, TResult> memberReference,
        Func<ModuleReference, TResult> moduleReference, Func<ImportedMethod, TResult> importedMethod,
        Func<ForwardedType, TResult> forwardedType, Func<BaseTypes, TResult> baseTypes,
        Func<BaseType, TResult> baseType, Func<DerivedTypes, TResult> derivedTypes,
        Func<DerivedType, TResult> derivedType, Func<UnreadableModule, TResult> unreadableModule, Func<Resources, TResult> resources,
        Func<ResourceContainer, TResult> resourceContainer, Func<ResourceEntry, TResult> resourceEntry,
        Func<Metadata, TResult> metadata, Func<MetadataTables, TResult> metadataTables,
        Func<MetadataTable, TResult> metadataTable, Func<Package, TResult> package,
        Func<PackageFile, TResult> packageFile)
    {
        return this switch
        {
            Module c => module(c),
            Namespace c => @namespace(c),
            FailedAssembly c => failedAssembly(c),
            TypeDefinition c => typeDefinition(c),
            MemberDefinition c => memberDefinition(c),
            References c => references(c),
            AssemblyReference c => assemblyReference(c),
            ReferencedTypes c => referencedTypes(c),
            TypeReference c => typeReference(c),
            MemberReference c => memberReference(c),
            ModuleReference c => moduleReference(c),
            ImportedMethod c => importedMethod(c),
            ForwardedType c => forwardedType(c),
            BaseTypes c => baseTypes(c),
            BaseType c => baseType(c),
            DerivedTypes c => derivedTypes(c),
            DerivedType c => derivedType(c),
            UnreadableModule c => unreadableModule(c),
            Resources c => resources(c),
            ResourceContainer c => resourceContainer(c),
            ResourceEntry c => resourceEntry(c),
            Metadata c => metadata(c),
            MetadataTables c => metadataTables(c),
            MetadataTable c => metadataTable(c),
            Package c => package(c),
            PackageFile c => packageFile(c),
            _ => throw new UnreachableException($"NodeSubject.Match does not handle {GetType().Name}."),
        };
    }

    /// <summary>What the handler of whichever case this is makes of it and <paramref name="state"/>, which it is
    /// handed, so that a handler that needs the caller's values can be static and allocate nothing.</summary>
    public TResult Match<TState, TResult>(TState state, Func<TState, Module, TResult> module,
        Func<TState, Namespace, TResult> @namespace, Func<TState, FailedAssembly, TResult> failedAssembly,
        Func<TState, TypeDefinition, TResult> typeDefinition, Func<TState, MemberDefinition, TResult> memberDefinition,
        Func<TState, References, TResult> references, Func<TState, AssemblyReference, TResult> assemblyReference,
        Func<TState, ReferencedTypes, TResult> referencedTypes, Func<TState, TypeReference, TResult> typeReference,
        Func<TState, MemberReference, TResult> memberReference, Func<TState, ModuleReference, TResult> moduleReference,
        Func<TState, ImportedMethod, TResult> importedMethod, Func<TState, ForwardedType, TResult> forwardedType,
        Func<TState, BaseTypes, TResult> baseTypes, Func<TState, BaseType, TResult> baseType,
        Func<TState, DerivedTypes, TResult> derivedTypes, Func<TState, DerivedType, TResult> derivedType,
        Func<TState, UnreadableModule, TResult> unreadableModule, Func<TState, Resources, TResult> resources, Func<TState, ResourceContainer, TResult> resourceContainer,
        Func<TState, ResourceEntry, TResult> resourceEntry, Func<TState, Metadata, TResult> metadata,
        Func<TState, MetadataTables, TResult> metadataTables, Func<TState, MetadataTable, TResult> metadataTable,
        Func<TState, Package, TResult> package, Func<TState, PackageFile, TResult> packageFile)
    {
        return this switch
        {
            Module c => module(state, c),
            Namespace c => @namespace(state, c),
            FailedAssembly c => failedAssembly(state, c),
            TypeDefinition c => typeDefinition(state, c),
            MemberDefinition c => memberDefinition(state, c),
            References c => references(state, c),
            AssemblyReference c => assemblyReference(state, c),
            ReferencedTypes c => referencedTypes(state, c),
            TypeReference c => typeReference(state, c),
            MemberReference c => memberReference(state, c),
            ModuleReference c => moduleReference(state, c),
            ImportedMethod c => importedMethod(state, c),
            ForwardedType c => forwardedType(state, c),
            BaseTypes c => baseTypes(state, c),
            BaseType c => baseType(state, c),
            DerivedTypes c => derivedTypes(state, c),
            DerivedType c => derivedType(state, c),
            UnreadableModule c => unreadableModule(state, c),
            Resources c => resources(state, c),
            ResourceContainer c => resourceContainer(state, c),
            ResourceEntry c => resourceEntry(state, c),
            Metadata c => metadata(state, c),
            MetadataTables c => metadataTables(state, c),
            MetadataTable c => metadataTable(state, c),
            Package c => package(state, c),
            PackageFile c => packageFile(state, c),
            _ => throw new UnreachableException($"NodeSubject.Match does not handle {GetType().Name}."),
        };
    }
}
