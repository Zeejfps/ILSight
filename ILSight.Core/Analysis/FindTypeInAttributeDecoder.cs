// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using System.Reflection.Metadata;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.Analysis;

/// <summary>Finds a type in the attribute values of a module.</summary>
internal sealed class FindTypeInAttributeDecoder : ICustomAttributeTypeProvider<TokenSearchResult>
{
    public required MetadataFile DeclaringModule { get; init; }
    public required MetadataModule CurrentModule { get; init; }
    public required TypeDefinitionHandle Handle { get; init; }
    public required PrimitiveTypeCode PrimitiveType { get; init; }

    /// <summary>Where a type name the library can't parse is noted.</summary>
    public required PassedOver PassedOver { get; init; }

    /// <summary>A decoder finding <paramref name="type"/> in the attribute values of <paramref name="currentModule"/>,
    /// noting in <paramref name="passedOver"/> each type name it can't parse.</summary>
    public static FindTypeInAttributeDecoder Create(MetadataModule currentModule, ITypeDefinition type, PassedOver passedOver)
    {
        return new FindTypeInAttributeDecoder
        {
            PassedOver = passedOver,
            CurrentModule = currentModule,
            DeclaringModule = type.ParentModule?.MetadataFile ?? throw new InvalidOperationException("Cannot use MetadataModule without PEFile as context."),
            Handle = DecompilerFacts.GetTypeHandle(type),
            PrimitiveType = type.KnownTypeCode == KnownTypeCode.None ? 0 : type.KnownTypeCode.ToPrimitiveTypeCode(),
        };
    }

    public TokenSearchResult GetPrimitiveType(PrimitiveTypeCode typeCode)
    {
        return typeCode == PrimitiveType ? TokenSearchResult.Found : 0;
    }

    public TokenSearchResult GetSystemType()
    {
        return TokenSearchResult.SystemType;
    }

    public TokenSearchResult GetSZArrayType(TokenSearchResult elementType)
    {
        return elementType & TokenSearchResult.Found;
    }

    public TokenSearchResult GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
    {
        var result = TokenSearchResult.NoResult;
        if (handle.IsEnum(reader, out var underlyingType))
        {
            result = (TokenSearchResult)underlyingType;
        }
        else if (((EntityHandle)handle).IsKnownType(reader, KnownTypeCode.Type))
        {
            result = TokenSearchResult.SystemType;
        }

        if (Handle == handle && reader == DeclaringModule.Metadata)
        {
            result |= TokenSearchResult.Found;
        }

        return result;
    }

    public TokenSearchResult GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
    {
        return GetResultFromResolvedType(CurrentModule.ResolveType(handle, default));
    }

    public TokenSearchResult GetTypeFromSerializedName(string name)
    {
        if (name is null)
        {
            return TokenSearchResult.NoResult;
        }

        try
        {
            return GetResultFromResolvedType(ReflectionHelper.ParseReflectionName(name, new SimpleTypeResolveContext(CurrentModule)));
        }
        catch (ReflectionNameParseException e)
        {
            PassedOver.Note(CurrentModule.MetadataFile, e);
            return TokenSearchResult.NoResult;
        }
    }

    public PrimitiveTypeCode GetUnderlyingEnumType(TokenSearchResult type)
    {
        var typeCode = type & TokenSearchResult.TypeCodeMask;
        if (typeCode == 0 || typeCode == TokenSearchResult.SystemType)
        {
            throw new EnumUnderlyingTypeResolveException("The underlying type of an enum in an attribute's value could not be resolved.");
        }

        return (PrimitiveTypeCode)typeCode;
    }

    public bool IsSystemType(TokenSearchResult type)
    {
        return (type & TokenSearchResult.TypeCodeMask) == TokenSearchResult.SystemType;
    }

    private TokenSearchResult GetResultFromResolvedType(IType type)
    {
        var definition = type.GetDefinition();
        if (definition is null)
        {
            return TokenSearchResult.NoResult;
        }

        var result = TokenSearchResult.NoResult;
        var underlyingType = definition.EnumUnderlyingType?.GetDefinition();
        if (underlyingType is not null)
        {
            result = (TokenSearchResult)underlyingType.KnownTypeCode.ToPrimitiveTypeCode();
        }
        else if (definition.KnownTypeCode == KnownTypeCode.Type)
        {
            result = TokenSearchResult.SystemType;
        }

        if (definition.MetadataToken == Handle && definition.ParentModule?.MetadataFile == DeclaringModule)
        {
            result |= TokenSearchResult.Found;
        }

        return result;
    }
}
