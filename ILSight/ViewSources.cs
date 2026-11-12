using ILSight.Core;

namespace ILSight;

/// <summary>What the settings keep of the screen is made from: the change counts of the columns,
/// the folded groups, the flags filters, the export window, the layout, the NuGet dialog's search and window,
/// and the open tabs' views, what the search looks for, and the display options.</summary>
public readonly record struct ViewSources
{
    public required int BookmarkColumns { get; init; }

    public required int SearchColumns { get; init; }

    public required int ExportColumns { get; init; }

    public required int FoldedOptionGroups { get; init; }

    public required SearchKinds SearchKinds { get; init; }

    public required int FlagsFilters { get; init; }

    public required int ExportWindow { get; init; }

    public required int Layout { get; init; }

    public required DisplayOptions Display { get; init; }

    public required int NuGetSearch { get; init; }

    public required int NuGetWindow { get; init; }

    public required int ProcessWindow { get; init; }

    public required int ProcessColumns { get; init; }

    public required int ProcessModuleColumns { get; init; }

    public required int CloseWindow { get; init; }

    public required int Documents { get; init; }
}
