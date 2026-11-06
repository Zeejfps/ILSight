using ZGF.Gui.Immediate;
using ILSight.Core.Metadata;
using ILSight.Localization;

namespace ILSight;

/// <summary>What a metadata grid's column headings are drawn from: each shows its filter text or
/// its name, then the funnel that filters it.</summary>
internal readonly struct MetadataGridHeading
{
    public readonly MetadataGrid Grid;
    public readonly MetadataGridState State;
    public readonly MetadataFilters Flags;
    public readonly Strings Strings;
    public readonly AppIcons Icons;
    public readonly int ListId;

    public MetadataGridHeading(MetadataGrid grid, MetadataGridState state, MetadataFilters flags, Strings strings, AppIcons icons, int listId)
    {
        Grid = grid;
        State = state;
        Flags = flags;
        Strings = strings;
        Icons = icons;
        ListId = listId;
    }
}
