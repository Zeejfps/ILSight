using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.CommandArea;
using ILSight.DocumentViewArea;
using ILSight.MainArea;
using ILSight.OmnibarArea;
using ILSight.SearchArea;

namespace ILSight;

/// <summary>The searches that open the layout's views, and the layout's reset once the user confirms it
/// (<see cref="Panels"/> opens them, and puts the layout back as it starts out).</summary>
public static class ScreenLayout
{
    /// <summary>Opens the search panel with its query kept to <paramref name="node"/>'s assembly, or to
    /// the namespace it is.</summary>
    public static void ScopeSearch(Search search, Panels panels, BrowseNode node)
    {
        search.ScopeTo(node);
        panels.OpenSearch();
    }

    /// <summary>Has the active document's omnibar search, its box taking focus; opens the search panel
    /// instead while no document is active or the omnibar is hidden.</summary>
    public static void SearchInOmnibar(Panels panels, ScreenDisplay display, Omnibar omnibar)
    {
        var panel = panels.ActivePanel;
        if (panel is null)
        {
            panels.OpenSearch();
            return;
        }
        if (!display.Options.ShowOmnibar)
        {
            panels.OpenSearch();
            return;
        }
        omnibar.StartSearch(panel);
    }

    /// <summary>Stops asking, resetting the layout when <paramref name="confirmed"/>.</summary>
    public static void FinishLayoutReset(Confirmation layoutReset, Panels panels, bool confirmed)
    {
        layoutReset.Stop();
        if (confirmed)
        {
            panels.Reset();
        }
    }
}
