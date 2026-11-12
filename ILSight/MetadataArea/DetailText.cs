using ILSight.Core;
using ILSight.CodeViewArea;

namespace ILSight.MetadataArea;

/// <summary>A decoded text under a metadata grid's row, as a code view shows it.</summary>
internal sealed record DetailText
{
    public required DecompiledDocument Document { get; init; }

    public required CodeViewState View { get; init; }
}
