using ILSight.Core.MetadataArea;
using ILSight.Core.ResourceArea;

namespace ILSight.Core;

/// <summary>What a <see cref="BrowseNode"/> shows: a <see cref="Code"/> document, a
/// <see cref="Table"/> grid or a <see cref="Picture"/>.</summary>
public union NodeContent(NodeContent.Code, NodeContent.Table, NodeContent.Picture)
{
    public sealed record Code(DecompiledDocument Document);

    public sealed record Table(MetadataGrid Grid);

    /// <summary>A picture, and for one stored as markup, such as SVG, that markup as a document.</summary>
    public sealed record Picture
    {
        public required ResourceImage Image { get; init; }

        public DecompiledDocument? Source { get; init; }
    }
}
