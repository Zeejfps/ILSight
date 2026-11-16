// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using System.Reflection.Metadata;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.AnalysisArea;

/// <summary>Finds a type in the attribute values of a module. The app's code System.Reflection.Metadata calls back as
/// it decodes an attribute's value: its own work (the comparisons with the type searched for, the mapping to a
/// <see cref="TokenSearchResult"/>, noting a name it can't parse) runs through <see cref="Callbacks"/>, so a bug in it
/// goes on to the edge; what it reads of the library's objects (a type resolved or parsed and its definition, an enum's
/// underlying type, a known type code) is read before, outside the count. An enum whose underlying type can't be found
/// is the attribute's bad data: the counted work returns that it found none, and
/// <see cref="EnumUnderlyingTypeResolveException"/> is thrown outside the count (CL2).</summary>
internal sealed class FindTypeInAttributeDecoder : ICustomAttributeTypeProvider<TokenSearchResult>
{
    public required MetadataFile DeclaringModule { get; init; }

    /// <summary>The metadata of <see cref="DeclaringModule"/>, read when the decoder was made.</summary>
    public required MetadataReader DeclaringMetadata { get; init; }

    public required MetadataModule CurrentModule { get; init; }

    /// <summary>The file of <see cref="CurrentModule"/>, where a type name the library can't parse is noted.</summary>
    public required MetadataFile CurrentFile { get; init; }

    public required TypeDefinitionHandle Handle { get; init; }
    public required PrimitiveTypeCode PrimitiveType { get; init; }

    /// <summary>Where a type name the library can't parse is noted.</summary>
    public required PassedOver PassedOver { get; init; }

    /// <summary>The count of the decoder's own work, which the catch around the read lets through when raised.</summary>
    public required CallbackFailure Callbacks { get; init; }

    public TokenSearchResult GetPrimitiveType(PrimitiveTypeCode typeCode)
    {
        return Callbacks.Run((this, typeCode), static s => s.typeCode == s.Item1.PrimitiveType ? TokenSearchResult.Found : 0);
    }

    public TokenSearchResult GetSystemType()
    {
        return Callbacks.Run(0, static _ => TokenSearchResult.SystemType);
    }

    public TokenSearchResult GetSZArrayType(TokenSearchResult elementType)
    {
        return Callbacks.Run(elementType, static e => e & TokenSearchResult.Found);
    }

    public TokenSearchResult GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
    {
        var isEnum = handle.IsEnum(reader, out var underlyingType);
        var isSystemType = !isEnum && ((EntityHandle)handle).IsKnownType(reader, KnownTypeCode.Type);
        var found = new DefinitionFound { IsEnum = isEnum, UnderlyingType = underlyingType, IsSystemType = isSystemType, Handle = handle, Reader = reader };
        return Callbacks.Run((this, found), static s => s.Item1.MapDefinition(s.found));
    }

    public TokenSearchResult GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
    {
        var type = CurrentModule.ResolveType(handle, default);
        var resolved = ReadResolved(type);
        return Callbacks.Run((this, resolved), static s => s.Item1.MapResolved(s.resolved));
    }

    public TokenSearchResult GetTypeFromSerializedName(string name)
    {
        if (name is null)
        {
            return Callbacks.Run(0, static _ => TokenSearchResult.NoResult);
        }

        IType type;
        try
        {
            type = ReflectionHelper.ParseReflectionName(name, new SimpleTypeResolveContext(CurrentModule));
        }
        catch (ReflectionNameParseException e)
        {
            return Callbacks.Run((this, e), static s => s.Item1.NoteUnparsed(s.e));
        }

        var resolved = ReadResolved(type);
        return Callbacks.Run((this, resolved), static s => s.Item1.MapResolved(s.resolved));
    }

    public PrimitiveTypeCode GetUnderlyingEnumType(TokenSearchResult type)
    {
        var typeCode = Callbacks.Run(type, static t => FindUnderlyingType(t));
        return typeCode ?? throw new EnumUnderlyingTypeResolveException("The underlying type of an enum in an attribute's value could not be resolved.");
    }

    public bool IsSystemType(TokenSearchResult type)
    {
        return Callbacks.Run(type, static t => (t & TokenSearchResult.TypeCodeMask) == TokenSearchResult.SystemType);
    }

    // The primitive type an enum's result carries; null when it carries none, or System.Type.
    private static PrimitiveTypeCode? FindUnderlyingType(TokenSearchResult type)
    {
        var typeCode = type & TokenSearchResult.TypeCodeMask;
        if (typeCode == 0 || typeCode == TokenSearchResult.SystemType)
        {
            return null;
        }

        return (PrimitiveTypeCode)typeCode;
    }

    private TokenSearchResult NoteUnparsed(ReflectionNameParseException e)
    {
        PassedOver.Note(CurrentFile, e);
        return TokenSearchResult.NoResult;
    }

    private TokenSearchResult MapDefinition(DefinitionFound found)
    {
        var result = TokenSearchResult.NoResult;
        if (found.IsEnum)
        {
            result = (TokenSearchResult)found.UnderlyingType;
        }
        else if (found.IsSystemType)
        {
            result = TokenSearchResult.SystemType;
        }

        if (Handle == found.Handle && found.Reader == DeclaringMetadata)
        {
            result |= TokenSearchResult.Found;
        }

        return result;
    }

    // What the result of a type resolved is made from, read out of its definition; no definition for a type with none.
    private static ResolvedType ReadResolved(IType type)
    {
        var definition = type.GetDefinition();
        if (definition is null)
        {
            return new ResolvedType { HasDefinition = false, UnderlyingType = null, KnownTypeCode = KnownTypeCode.None, Token = default, File = null };
        }

        var underlying = definition.EnumUnderlyingType?.GetDefinition();
        return new ResolvedType
        {
            HasDefinition = true, UnderlyingType = underlying?.KnownTypeCode.ToPrimitiveTypeCode(), KnownTypeCode = definition.KnownTypeCode,
            Token = definition.MetadataToken, File = definition.ParentModule?.MetadataFile,
        };
    }

    private TokenSearchResult MapResolved(ResolvedType resolved)
    {
        if (!resolved.HasDefinition)
        {
            return TokenSearchResult.NoResult;
        }

        var result = TokenSearchResult.NoResult;
        if (resolved.UnderlyingType is not null)
        {
            result = (TokenSearchResult)resolved.UnderlyingType.Value;
        }
        else if (resolved.KnownTypeCode == KnownTypeCode.Type)
        {
            result = TokenSearchResult.SystemType;
        }

        if (resolved.Token == Handle && resolved.File == DeclaringModule)
        {
            result |= TokenSearchResult.Found;
        }

        return result;
    }

    private readonly record struct DefinitionFound
    {
        public required bool IsEnum { get; init; }
        public required PrimitiveTypeCode UnderlyingType { get; init; }
        public required bool IsSystemType { get; init; }
        public required TypeDefinitionHandle Handle { get; init; }
        public required MetadataReader Reader { get; init; }
    }

    private readonly record struct ResolvedType
    {
        public required bool HasDefinition { get; init; }
        public required PrimitiveTypeCode? UnderlyingType { get; init; }
        public required KnownTypeCode KnownTypeCode { get; init; }
        public required EntityHandle Token { get; init; }
        public required MetadataFile? File { get; init; }
    }
}
