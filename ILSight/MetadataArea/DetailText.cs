using ILSight.Core;
using ILSight.CodeView;

namespace ILSight.MetadataArea;

/// <summary>A decoded text under a metadata grid's row, as a code view shows it.</summary>
internal sealed record DetailText(DecompiledDocument Document, CodeViewState View);
