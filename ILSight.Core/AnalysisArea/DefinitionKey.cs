using System.Reflection.Metadata;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.AnalysisArea;

/// <summary>Which definition an entity names, read out of the library so the analyzers compare definitions outside its
/// read: its token and the file it was read from, the same definition when both are (<see cref="Members.IsSame"/>).</summary>
internal readonly struct DefinitionKey
{
    private DefinitionKey(EntityHandle token, MetadataFile? file)
    {
        Token = token;
        File = file;
    }

    public EntityHandle Token { get; }

    public MetadataFile? File { get; }

    /// <summary>The key of <paramref name="entity"/>: a read of the library's object, made inside a read's catch.</summary>
    public static DefinitionKey Of(IEntity entity)
    {
        return new DefinitionKey(entity.MetadataToken, entity.ParentModule?.MetadataFile);
    }

    /// <summary>Whether both name the same definition: the same token in the same file.</summary>
    public bool Is(DefinitionKey other)
    {
        return Token == other.Token && File == other.File;
    }
}
