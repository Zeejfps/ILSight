using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

/// <summary>The definition a node of a type or member stands for, as documents and metadata name it.</summary>
public static class NodeDefinitions
{
    /// <summary>The base type <paramref name="type"/> as <paramref name="module"/> names it, navigable when
    /// its definition is found and can be referenced.</summary>
    internal static BaseTypeInfo CreateBaseType(LoadedModule module, IType type)
    {
        var definition = type.GetDefinition();
        if (definition == null)
            return new BaseTypeInfo(module, type, new BaseTypeTarget.Unresolved(), NodeVisibility.Open);
        var reference = CodeReferences.Find(definition);
        if (reference == null)
            return new BaseTypeInfo(module, type, new BaseTypeTarget.Unaddressed(definition), NodeVisibility.Open);
        return new BaseTypeInfo(module, type, new BaseTypeTarget.Navigable(definition, reference), NodeVisibility.OfReferenced(definition));
    }

    /// <summary>The derived type <paramref name="type"/>; null when it has no reference to be navigated to.</summary>
    internal static DerivedTypeInfo? FindDerivedType(LoadedModule module, ITypeDefinition type)
    {
        var reference = CodeReferences.Find(type);
        if (reference == null)
            return null;
        return new DerivedTypeInfo(module, type, reference);
    }

    /// <summary>The definition of <paramref name="type"/> in <paramref name="module"/>; null when it has no
    /// metadata file or token, as a type the decompiler made up has not.</summary>
    internal static DefinedType? FindType(LoadedModule module, ITypeDefinition type)
    {
        if (type.MetadataToken.Kind != HandleKind.TypeDefinition)
            return null;
        var reference = CodeReferences.Find(type);
        if (reference == null)
            return null;
        return new DefinedType(module, type, (TypeDefinitionHandle)type.MetadataToken, reference);
    }

    /// <summary>The definition of <paramref name="member"/> in <paramref name="module"/>; null when it has
    /// no metadata file or token.</summary>
    internal static DefinedMember? FindMember(LoadedModule module, IMember member)
    {
        var reference = CodeReferences.Find(member);
        if (reference == null)
            return null;
        return new DefinedMember(module, member, reference);
    }

    /// <summary>A reference to the type or member a node of <paramref name="subject"/> defines, as documents
    /// point at it; null for a node that defines none.</summary>
    public static CodeReference.Entity? ReferenceOf(NodeSubject subject) => subject switch
    {
        NodeSubject.TypeDefinition type => type.Definition.Reference,
        NodeSubject.MemberDefinition member => member.Definition.Reference,
        NodeSubject.References or NodeSubject.AssemblyReference or NodeSubject.ReferencedTypes or NodeSubject.TypeReference
            or NodeSubject.MemberReference or NodeSubject.ModuleReference or NodeSubject.ImportedMethod or NodeSubject.ForwardedType
            or NodeSubject.BaseTypes or NodeSubject.BaseType or NodeSubject.DerivedTypes or NodeSubject.DerivedType or NodeSubject.Resources
            or NodeSubject.ResourceContainer or NodeSubject.ResourceEntry or NodeSubject.Metadata or NodeSubject.MetadataTables
            or NodeSubject.MetadataTable or NodeSubject.Package or NodeSubject.PackageFile or NodeSubject.Module or NodeSubject.Namespace
            or NodeSubject.FailedAssembly => null,
        _ => throw new UnreachableException(),
    };

    /// <summary>The metadata token of the type or member a node of <paramref name="subject"/> defines;
    /// null for a node that defines none.</summary>
    public static int? TokenOf(NodeSubject subject)
    {
        var reference = ReferenceOf(subject);
        if (reference == null)
            return null;
        return MetadataTokens.GetToken(reference.Handle);
    }
}
