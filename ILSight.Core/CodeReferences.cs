using System.Reflection.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

/// <summary>Making <see cref="CodeReference"/>s of what the decompiler and metadata name.</summary>
internal static class CodeReferences
{
    /// <summary>A reference to the definition <paramref name="entity"/>, named <paramref name="name"/>
    /// or else by its own name; null when it has no metadata file or token.</summary>
    public static CodeReference.Entity? Find(IEntity entity, string? name = null)
    {
        var file = entity.ParentModule?.MetadataFile;
        if (file is null || entity.MetadataToken.IsNil)
        {
            return null;
        }

        return new CodeReference.Entity { Kind = GetKind(entity.SymbolKind), Name = name ?? entity.Name, FilePath = AssemblyPath.Of(file.FileName), Target = entity.MetadataToken };
    }

    /// <summary>A reference to what <paramref name="handle"/> of <paramref name="metadata"/> names, read
    /// from the file at <paramref name="filePath"/>, or null for a handle no reference is made to.</summary>
    public static CodeReference.Navigable? Find(MetadataReader metadata, Handle handle, string name, AssemblyPath filePath)
    {
        return handle.Kind switch
        {
            HandleKind.AssemblyDefinition => CreateAssembly(name, filePath),
            HandleKind.AssemblyReference => CreateAssembly(name, filePath, (AssemblyReferenceHandle)handle),
            HandleKind.TypeDefinition or HandleKind.TypeReference or HandleKind.TypeSpecification =>
                new CodeReference.Entity { Kind = EntityKind.Type, Name = name, FilePath = filePath, Target = (EntityHandle)handle },
            HandleKind.MethodDefinition or HandleKind.MethodSpecification => new CodeReference.Entity { Kind = EntityKind.Method, Name = name, FilePath = filePath, Target = (EntityHandle)handle },
            HandleKind.FieldDefinition => new CodeReference.Entity { Kind = EntityKind.Field, Name = name, FilePath = filePath, Target = (EntityHandle)handle },
            HandleKind.PropertyDefinition => new CodeReference.Entity { Kind = EntityKind.Property, Name = name, FilePath = filePath, Target = (EntityHandle)handle },
            HandleKind.EventDefinition => new CodeReference.Entity { Kind = EntityKind.Event, Name = name, FilePath = filePath, Target = (EntityHandle)handle },
            HandleKind.MemberReference => new CodeReference.Entity { Kind = GetMemberKind(metadata, (MemberReferenceHandle)handle), Name = name, FilePath = filePath, Target = (EntityHandle)handle },
            _ => null,
        };
    }

    private static EntityKind GetMemberKind(MetadataReader metadata, MemberReferenceHandle handle)
    {
        var kind = metadata.GetMemberReference(handle).GetKind();
        return kind == MemberReferenceKind.Field ? EntityKind.Field : EntityKind.Method;
    }

    public static EntityKind GetKind(SymbolKind kind)
    {
        return kind switch
        {
            SymbolKind.TypeDefinition => EntityKind.Type,
            SymbolKind.Field => EntityKind.Field,
            SymbolKind.Property or SymbolKind.Indexer => EntityKind.Property,
            SymbolKind.Event => EntityKind.Event,
            _ => EntityKind.Method,
        };
    }

    /// <summary>A reference to the assembly in the file at <paramref name="filePath"/> itself.</summary>
    public static CodeReference.Assembly CreateAssembly(string name, AssemblyPath filePath)
    {
        return CreateAssembly(name, filePath, (AssemblyReferenceHandle?)null);
    }

    /// <summary>A reference to the assembly the file at <paramref name="filePath"/> references by <paramref name="handle"/>.</summary>
    public static CodeReference.Assembly CreateAssembly(string name, AssemblyPath filePath, AssemblyReferenceHandle handle)
    {
        return CreateAssembly(name, filePath, (AssemblyReferenceHandle?)handle);
    }

    private static CodeReference.Assembly CreateAssembly(string name, AssemblyPath filePath, AssemblyReferenceHandle? referenced)
    {
        return new CodeReference.Assembly { Name = name, FilePath = filePath, Referenced = referenced };
    }

    public static CodeReference.Entity CreateEntity(EntityKind kind, string name, AssemblyPath filePath, EntityHandle handle)
    {
        return new CodeReference.Entity { Kind = kind, Name = name, FilePath = filePath, Target = handle };
    }
}
