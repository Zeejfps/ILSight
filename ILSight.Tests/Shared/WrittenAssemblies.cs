using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace ILSight.Tests;

/// <summary>Small assemblies a test writes, version 1.0, each of public classes in the namespace of its own name,
/// deriving from a class of its own or of another such assembly.</summary>
internal static class WrittenAssemblies
{
    private static readonly AssemblyName Runtime = typeof(WrittenAssemblies).Assembly.GetReferencedAssemblies()
        .Single(static a => a.Name == "System.Runtime");

    private static readonly Version Version = new(1, 0, 0, 0);

    /// <summary>Writes <c>{name}.dll</c>, defining the class <paramref name="baseClass"/>, deriving from
    /// <see cref="object"/>, and <paramref name="derivedClass"/>, deriving from it.</summary>
    public static string WriteWithBase(TempDir temp, string name, string baseClass, string derivedClass)
    {
        var metadata = Begin(name);
        var objectType = metadata.AddTypeReference(ReferTo(metadata, Runtime), metadata.GetOrAddString("System"), metadata.GetOrAddString("Object"));
        var baseType = Define(metadata, name, baseClass, objectType);
        Define(metadata, name, derivedClass, baseType);
        return Save(temp, name, metadata);
    }

    /// <summary>Writes <c>{name}.dll</c>, defining the class <paramref name="derivedClass"/>, deriving from the class
    /// <paramref name="baseClass"/> of the assembly <paramref name="baseAssembly"/> (<see cref="WriteWithBase"/>).</summary>
    public static string WriteDeriving(TempDir temp, string name, string baseAssembly, string baseClass, string derivedClass)
    {
        var metadata = Begin(name);
        ReferTo(metadata, Runtime);
        var reference = ReferTo(metadata, new AssemblyName(baseAssembly) { Version = Version });
        var baseType = metadata.AddTypeReference(reference, metadata.GetOrAddString(baseAssembly), metadata.GetOrAddString(baseClass));
        Define(metadata, name, derivedClass, baseType);
        return Save(temp, name, metadata);
    }

    private static MetadataBuilder Begin(string name)
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(0, metadata.GetOrAddString($"{name}.dll"), metadata.GetOrAddGuid(Guid.NewGuid()), default, default);
        metadata.AddAssembly(metadata.GetOrAddString(name), Version, default, default, 0, AssemblyHashAlgorithm.None);
        metadata.AddTypeDefinition(default, default, metadata.GetOrAddString("<Module>"), default,
            MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        return metadata;
    }

    private static AssemblyReferenceHandle ReferTo(MetadataBuilder metadata, AssemblyName assembly)
    {
        var name = assembly.Name ?? throw new ArgumentException("The assembly to refer to has no name.", nameof(assembly));
        var version = assembly.Version ?? throw new ArgumentException($"{name} has no version.", nameof(assembly));
        var token = assembly.GetPublicKeyToken() ?? [];
        var publicKey = token.Length == 0 ? default : metadata.GetOrAddBlob(token);
        return metadata.AddAssemblyReference(metadata.GetOrAddString(name), version, default, publicKey, default, default);
    }

    private static TypeDefinitionHandle Define(MetadataBuilder metadata, string ns, string name, EntityHandle baseType)
    {
        return metadata.AddTypeDefinition(TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.BeforeFieldInit,
            metadata.GetOrAddString(ns), metadata.GetOrAddString(name), baseType,
            MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
    }

    private static string Save(TempDir temp, string name, MetadataBuilder metadata)
    {
        var pe = new ManagedPEBuilder(PEHeaderBuilder.CreateLibraryHeader(), new MetadataRootBuilder(metadata), new BlobBuilder());
        var image = new BlobBuilder();
        pe.Serialize(image);
        return temp.Write($"{name}.dll", image.ToArray());
    }
}
