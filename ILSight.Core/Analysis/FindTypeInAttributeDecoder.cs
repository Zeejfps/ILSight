// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using System.Reflection.Metadata;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.Analysis;

/// <summary>What decoding an attribute type found: <see cref="Found"/> when it is the type searched
/// for, plus while decoding an enum its underlying <see cref="PrimitiveTypeCode"/>, or
/// <see cref="SystemType"/> for <see cref="System.Type"/>.</summary>
internal enum TokenSearchResult : byte
{
    NoResult = 0,
    TypeCodeMask = 0b0111_1111,
    Found = 0b1000_0000,
    SystemType = 127,
}

/// <summary>Finds a type in the attribute values of a module.</summary>
/// <remarks>The decompiler's ICustomAttributeTypeProvider demands its methods, which forward.</remarks>
internal sealed class FindTypeInAttributeDecoder : ICustomAttributeTypeProvider<TokenSearchResult>
{
    public required MetadataFile DeclaringModule { get; init; }
    public required MetadataModule CurrentModule { get; init; }
    public required TypeDefinitionHandle Handle { get; init; }
    public required PrimitiveTypeCode PrimitiveType { get; init; }

    public TokenSearchResult GetPrimitiveType(PrimitiveTypeCode typeCode)
    {
        return AnalyzerHelpers.GetPrimitiveType(this, typeCode);
    }

    public TokenSearchResult GetSystemType()
    {
        return AnalyzerHelpers.GetSystemType(this);
    }

    public TokenSearchResult GetSZArrayType(TokenSearchResult elementType)
    {
        return AnalyzerHelpers.GetSZArrayType(this, elementType);
    }

    public TokenSearchResult GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
    {
        return AnalyzerHelpers.GetTypeFromDefinition(this, reader, handle, rawTypeKind);
    }

    public TokenSearchResult GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
    {
        return AnalyzerHelpers.GetTypeFromReference(this, reader, handle, rawTypeKind);
    }

    public TokenSearchResult GetTypeFromSerializedName(string name)
    {
        return AnalyzerHelpers.GetTypeFromSerializedName(this, name);
    }

    public PrimitiveTypeCode GetUnderlyingEnumType(TokenSearchResult type)
    {
        return AnalyzerHelpers.GetUnderlyingEnumType(this, type);
    }

    public bool IsSystemType(TokenSearchResult type)
    {
        return AnalyzerHelpers.IsSystemType(this, type);
    }
}
