using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

/// <summary>What we know of the decompiler's types that their declarations leave open. Each function
/// narrows one of them, and throws when what we know turns out false.</summary>
internal static class DecompilerFacts
{
    /// <summary>The module <paramref name="entity"/> belongs to, which every entity of a loaded assembly has.</summary>
    public static IModule GetModule(IEntity entity)
    {
        var module = entity.ParentModule;
        if (module is null)
        {
            throw new UnreachableException($"{entity} belongs to no module, though every entity of a loaded assembly does.");
        }

        return module;
    }

    /// <summary>The module read from metadata that <paramref name="entity"/> belongs to, as every entity of a
    /// loaded assembly does.</summary>
    public static MetadataModule GetMetadataModule(IEntity entity)
    {
        if (entity.ParentModule is not MetadataModule module)
        {
            throw new UnreachableException($"{entity} belongs to no module read from metadata, though every entity of a loaded assembly does.");
        }

        return module;
    }

    /// <summary>The file <paramref name="entity"/> was read from, which every entity of a loaded assembly has.</summary>
    public static MetadataFile GetMetadataFile(IEntity entity)
    {
        var file = entity.ParentModule?.MetadataFile;
        if (file is null)
        {
            throw new UnreachableException($"{entity} was read from no file, though every entity of a loaded assembly was.");
        }

        return file;
    }

    /// <summary>The type that declares <paramref name="member"/>, which every member, as against a type
    /// at the top, has.</summary>
    public static ITypeDefinition GetDeclaringTypeDefinition(IEntity member)
    {
        var declaring = member.DeclaringTypeDefinition;
        if (declaring is null)
        {
            throw new UnreachableException($"{member} has no type declaring it, though every member has.");
        }

        return declaring;
    }

    /// <summary>The row of <paramref name="method"/> in the MethodDef table, which every method of a loaded
    /// assembly has.</summary>
    public static MethodDefinitionHandle GetMethodHandle(IMethod method)
    {
        var token = method.MetadataToken;
        if (token.Kind != HandleKind.MethodDefinition)
        {
            throw new UnreachableException($"{method} has a {token.Kind} token, though every method of a loaded assembly has a MethodDefinition one.");
        }

        return (MethodDefinitionHandle)token;
    }

    /// <summary>The row of <paramref name="type"/> in the TypeDef table, which every type definition of a
    /// loaded assembly has.</summary>
    public static TypeDefinitionHandle GetTypeHandle(ITypeDefinition type)
    {
        var token = type.MetadataToken;
        if (token.Kind != HandleKind.TypeDefinition)
        {
            throw new UnreachableException($"{type} has a {token.Kind} token, though every type of a loaded assembly has a TypeDefinition one.");
        }

        return (TypeDefinitionHandle)token;
    }

    /// <summary>The member <paramref name="handle"/> of <paramref name="module"/> stands for, which the caller
    /// knows to name a member.</summary>
    public static IMember ResolveMember(MetadataModule module, EntityHandle handle)
    {
        if (module.ResolveEntity(handle) is not IMember member)
        {
            throw new UnreachableException($"The {handle.Kind} handle resolves to no member, though it names one.");
        }

        return member;
    }

    /// <summary>The optional header of a portable executable, which every one the decompiler loaded has,
    /// as only object files go without.</summary>
    public static PEHeader GetPEHeader(PEHeaders headers)
    {
        var header = headers.PEHeader;
        if (header is null)
        {
            throw new UnreachableException("A portable executable that was loaded has no optional header, which only object files go without.");
        }

        return header;
    }
}
