using ZGF.Gui.Immediate.Widgets;

namespace ILSight.SearchArea;

/// <summary>The search panel's own state beside its search (<see cref="Search"/>, which the omnibar has one of too):
/// its results table's columns. Its owner is the panel, its lifetime the settings' (the settings' save reads it here).
/// The panel is shown in one place, a dock panel, so it is held once (<see cref="UiState"/>); its places would be
/// independent: were it shown in a second place, each would keep its own column widths.</summary>
public sealed class SearchPanel
{
    /// <summary>The columns of the results: name, location and assembly.</summary>
    public TableColumns Columns { get; } = new(
        new TableColumn(3f, 120f),
        new TableColumn(3f, 80f),
        new TableColumn(2f, 60f));
}
