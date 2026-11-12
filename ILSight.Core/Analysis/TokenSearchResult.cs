// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using System.Reflection.Metadata;

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
