using ILSight.Core;

namespace ILSight;

/// <summary>What the settings keep of the screen is made from: the change counts of the columns,
/// the folded groups, the flags filters, the export window, the layout, the NuGet dialog's search and window,
/// and the open tabs' views, what the search looks for, and the display options.</summary>
public readonly record struct ViewSources(
    int BookmarkColumns,
    int SearchColumns,
    int ExportColumns,
    int FoldedOptionGroups,
    SearchKinds SearchKinds,
    int FlagsFilters,
    int ExportWindow,
    int Layout,
    DisplayOptions Display,
    int NuGetSearch,
    int NuGetWindow,
    int Documents);
