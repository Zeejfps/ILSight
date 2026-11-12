using ZGF.Gui.Immediate;
using ILSight.Core.Metadata;

namespace ILSight.MetadataGridArea;

/// <summary>What a metadata grid's column headings are drawn from: each shows its filter text or
/// its name, then the funnel that filters it.</summary>
internal readonly struct MetadataGridHeading
{
    public required MetadataGrid Grid { get; init; }
    public required MetadataGridState State { get; init; }
    public required MetadataFilters Flags { get; init; }
    public required Strings Strings { get; init; }
    public required AppIcons Icons { get; init; }
    public required int ListId { get; init; }
}
