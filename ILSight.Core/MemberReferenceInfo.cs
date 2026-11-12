using System.Reflection.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

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
