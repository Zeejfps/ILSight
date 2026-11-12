using ZGF.Desktop;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.Core.Metadata;

namespace ILSight;

/// <summary>What the settings keep of the screen: the column widths of the bookmarks, search results
/// and export tables, the decompiler options panel's folded groups, what the search looks for, the
/// metadata grids' flags filters, where the export dialog's window was left, a copy of the layout,
/// which shares nothing with the screen's, how the documents draw code, the feed the NuGet
/// dialog last searched and where its window was left, and the panel each open tab is docked as.
/// A part never saved is null.</summary>
public sealed record ViewSettings
{
    public required List<float>? BookmarkColumns { get; init; }

    public required List<float>? SearchColumns { get; init; }

    public required List<float>? ExportColumns { get; init; }

    public required List<string>? FoldedOptionGroups { get; init; }

    public required SearchKinds SearchKinds { get; init; }

    public required Dictionary<string, SavedFilterState>? FlagsFilters { get; init; }

    public required WindowBounds? ExportWindow { get; init; }

    public required DockLayout? Layout { get; init; }

    public required DisplayOptions? Display { get; init; }

    public required SavedNuGetSearch? NuGet { get; init; }

    public required WindowBounds? NuGetWindow { get; init; }

    public required WindowBounds? ProcessWindow { get; init; }

    public required List<float>? ProcessColumns { get; init; }

    public required List<float>? ProcessModuleColumns { get; init; }

    public required WindowBounds? CloseWindow { get; init; }

    public required IReadOnlyList<TabPanel>? Tabs { get; init; }
}
