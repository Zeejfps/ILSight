using System.Reflection.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using ILSight.Core.Metadata;
using ILSight.Core.Resources;

namespace ILSight.Core;

/// <summary>What sort of type a type is.</summary>
public enum TypeShape
{
    Class,
    Interface,
    Struct,
    Enum,
    Delegate,
    Record,
    RecordStruct,
}

/// <summary>What sort of member a member is.</summary>
public enum MemberShape
{
    Field,
    Constant,
    Property,
    Indexer,
    Event,
    Method,
    Constructor,
    Operator,
}

/// <summary>What a symbol an API comparison, an analysis or a diagram lists is: an assembly, a namespace,
/// or a type or member of some shape.</summary>
public abstract record SymbolShape
{
    private SymbolShape()
    {
    }

    public sealed record Assembly : SymbolShape;

    public sealed record Namespace : SymbolShape;

    public sealed record Type(TypeShape Shape) : SymbolShape;

    public sealed record Member(MemberShape Shape) : SymbolShape;
}

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

/// <summary>An assembly <see cref="Module"/> references, as its metadata names it.</summary>
public sealed class AssemblyReferenceInfo
{
    public required LoadedModule Module { get; init; }

    public required ICSharpCode.Decompiler.Metadata.AssemblyReference Reference { get; init; }
}

/// <summary>A type <see cref="Module"/> uses from one of its references: labelled as <c>typeof</c> names
/// an open generic type, and fully named after the type it is nested in, else after its namespace.</summary>
public sealed class TypeReferenceInfo
{
    /// <summary>The name with its namespace, the types it is nested in and how many type parameters it
    /// has, as metadata writes it: <c>System.Collections.Generic.List`1</c>.</summary>
    public required string ReflectionName { get; init; }

    public required LoadedModule Module { get; init; }

    public required TypeReferenceHandle Handle { get; init; }

    public required string Namespace { get; init; }

    public required string Label { get; init; }

    public required string FullName { get; init; }
}

/// <summary>A member <see cref="Module"/> uses of a type from one of its references, as the type
/// declares it: for an accessor, the property or event it belongs to.</summary>
public sealed class MemberReferenceInfo
{
    public required LoadedModule Module { get; init; }

    public required MemberReferenceHandle Handle { get; init; }

    public required IMember Declared { get; init; }

    public required string FullName { get; init; }

    /// <summary>What sort of member is used, as its type declares it.</summary>
    public MemberShape Shape => EntityText.ShapeOf(Declared);
}

/// <summary>A module <see cref="Module"/> references: a native library it calls into, or another module
/// of the assembly, which holds metadata when the assembly's file table says so.</summary>
public sealed class ModuleReferenceInfo
{
    public required LoadedModule Module { get; init; }

    public required string Name { get; init; }

    public required bool ContainsMetadata { get; init; }

    /// <summary>The methods the module imports from it.</summary>
    public required IReadOnlyList<MethodDefinitionHandle> Imports { get; init; }
}

/// <summary>A method imported from a module: <see cref="Shown"/> is the method it is labelled and goes
/// as, which for a stub the compiler generates as a local function is the method declaring it.</summary>
public sealed class ImportedMethodInfo
{
    public required DefinedMember Shown { get; init; }

    /// <summary>The XML documentation id of the method that calls into the module, the stub itself for a
    /// local function.</summary>
    public required string Id { get; init; }

    /// <summary>The method as the tree labels it, with the parameters the module is called with.</summary>
    public required string Label { get; init; }

    public required string DeclaringTypeName { get; init; }

    /// <summary>The name of the function the method calls in the module.</summary>
    public required string EntryPoint { get; init; }

    public required string ModuleName { get; init; }
}

/// <summary>A type <see cref="Module"/> forwards to the assembly <see cref="Destination"/> names.</summary>
public sealed class ForwardedTypeInfo
{
    public required LoadedModule Module { get; init; }

    public required ExportedTypeHandle Handle { get; init; }

    public required string Namespace { get; init; }

    /// <summary>The type as <c>typeof</c> names an open generic type, e.g. <c>Dictionary&lt;,&gt;</c>.</summary>
    public required string Label { get; init; }

    /// <summary>The name without type parameters, which it sorts by, then by <see cref="Arity"/>.</summary>
    public required string SortName { get; init; }

    /// <summary>How many type parameters it has.</summary>
    public required int Arity { get; init; }

    public required string Destination { get; init; }

    public string FullName => Namespace.Length == 0 ? Label : $"{Namespace}.{Label}";

    /// <summary>The name with its namespace and how many type parameters it has, as metadata writes it:
    /// <c>System.Collections.Generic.Dictionary`2</c>.</summary>
    public string ReflectionName
    {
        get
        {
            var name = Arity == 0 ? SortName : $"{SortName}`{Arity}";
            return Namespace.Length == 0 ? name : $"{Namespace}.{name}";
        }
    }
}

/// <summary>A type a type derives from directly, as <see cref="Module"/> names it, and what it resolves to.</summary>
public sealed class BaseTypeInfo
{
    public required LoadedModule Module { get; init; }

    /// <summary>The type as it is derived from, with its type arguments.</summary>
    public required IType Type { get; init; }

    public required BaseTypeTarget Target { get; init; }

    /// <summary>How visible the type is, when it can be navigated to; else as visible as anything is.</summary>
    public required NodeVisibility Visibility { get; init; }
}

/// <summary>What a base type resolves to: a definition that can be navigated to, one that cannot, or none.</summary>
public abstract record BaseTypeTarget
{
    private BaseTypeTarget()
    {
    }

    public sealed record Navigable(ITypeDefinition Definition, CodeReference.Entity Reference) : BaseTypeTarget;

    public sealed record Unaddressed(ITypeDefinition Definition) : BaseTypeTarget;

    public sealed record Unresolved : BaseTypeTarget;
}

/// <summary>A type of the open assemblies that derives directly from the type a folder of derived types
/// belongs to, and the reference it is navigated to by.</summary>
public sealed class DerivedTypeInfo
{
    public required NodeVisibility Visibility { get; init; }

    /// <summary>The module whose tree lists the type, under a type of its own.</summary>
    public required LoadedModule Module { get; init; }

    public required ITypeDefinition Symbol { get; init; }

    public required CodeReference.Entity Reference { get; init; }
}

/// <summary>A type a module defines, with the handle its node is found by and the reference it is navigated to by.</summary>
public sealed class DefinedType
{
    public required LoadedModule Module { get; init; }

    public required ITypeDefinition Symbol { get; init; }

    public required TypeDefinitionHandle Handle { get; init; }

    public required CodeReference.Entity Reference { get; init; }

    public TypeShape Shape => EntityText.ShapeOf(Symbol);

    /// <summary>How visible the type is: compiler-generated when the compiler made it up, or a type it is nested in.</summary>
    public required NodeVisibility Visibility { get; init; }
}

/// <summary>A member a module defines, with the handle its node is found by and the reference it is navigated to by.</summary>
public sealed class DefinedMember
{
    public required LoadedModule Module { get; init; }

    public required IMember Symbol { get; init; }

    public EntityHandle Handle => Reference.Handle;

    public required CodeReference.Entity Reference { get; init; }

    public MemberShape Shape => EntityText.ShapeOf(Symbol);

    /// <summary>How visible the member is: compiler-generated when the compiler made it up, or the property,
    /// event or type that holds it.</summary>
    public required NodeVisibility Visibility { get; init; }
}

/// <summary>A namespace of <see cref="Module"/>: the types it defines there and those it forwards from
/// there to other assemblies. One that holds neither is listed only because others nest in it.</summary>
public sealed class NamespaceInfo
{
    public required LoadedModule Module { get; init; }

    /// <summary>The full name; empty for the global namespace.</summary>
    public required string Name { get; init; }

    public required IReadOnlyList<ITypeDefinition> Types { get; init; }

    public required IReadOnlyList<ForwardedTypeInfo> Forwarded { get; init; }

    /// <summary>The name as the tree shows it: <c>-</c> for the global namespace.</summary>
    public string Label => Name.Length == 0 ? "-" : Name;

    /// <summary>Whether the module puts no types here, listing it only because others nest in it.</summary>
    public bool IsEnclosingOnly => Types.Count == 0 && Forwarded.Count == 0;

    /// <summary>Whether a type here is visible outside the module: one it forwards, or a public one.</summary>
    public required bool IsPublicApi { get; init; }

    /// <summary>Whether everything here is something the compiler made up.</summary>
    public required bool IsCompilerGenerated { get; init; }
}
