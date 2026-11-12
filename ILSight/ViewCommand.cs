using System.Diagnostics;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.ILSightAppArea;

namespace ILSight;

/// <summary>What a feature running with no view on the stack commands the view to do, which
/// <see cref="ILSightApp"/> runs on the screen, in the order queued, before it draws the next frame.</summary>
public abstract record ViewCommand
{
    private ViewCommand()
    {
    }

    /// <summary>Bring <see cref="Tab"/>'s panel to the front, docked beside the tab <see cref="Beside"/>
    /// when it is not docked yet.</summary>
    public sealed record ShowTab(DocumentTab Tab, DocumentTab? Beside) : ViewCommand;

    /// <summary>Show the reopened <see cref="Tabs"/> in the panels they were docked as: where the layout
    /// still has them, else as <see cref="Layout"/>, the documents' saved arrangement, has them in the place of
    /// the documents; and return their diagrams' views to how they were saved.</summary>
    public sealed record ShowReopened(IReadOnlyList<ReopenedTab> Tabs, DockLayout? Layout) : ViewCommand;

    /// <summary>Dock <see cref="View"/> if it is not, and bring it to the front.</summary>
    public sealed record OpenPanel(DockView View) : ViewCommand;

    /// <summary>Return <see cref="Tab"/>'s view to <see cref="Position"/>.</summary>
    public sealed record RestoreView(DocumentTab Tab, ViewPosition Position) : ViewCommand;

    /// <summary>Scroll <see cref="Tab"/>'s code to <see cref="Line"/> and flash it.</summary>
    public sealed record RevealLine(DocumentTab Tab, int Line) : ViewCommand;

    /// <summary>Select <see cref="Tab"/>'s grid row <see cref="Row"/> and scroll to it.</summary>
    public sealed record RevealRow(DocumentTab Tab, int Row) : ViewCommand;

    /// <summary>Expand the tree down to <see cref="Node"/>, select it and scroll to it.</summary>
    public sealed record RevealNode(BrowseNode Node) : ViewCommand;

    /// <summary>Expand the tree's <see cref="Nodes"/>.</summary>
    public sealed record ExpandNodes(IReadOnlyList<BrowseNode> Nodes) : ViewCommand;

    /// <summary>Select the card of <see cref="Type"/> on the diagram <see cref="Tab"/> shows, and bring it
    /// into view once it is laid out.</summary>
    public sealed record FocusCard(DocumentTab Tab, CodeReference.Entity Type) : ViewCommand;

    /// <summary>Expand the analyzer's <see cref="Root"/> and select it.</summary>
    public sealed record ShowAnalysis(AnalyzerSymbolNode Root) : ViewCommand;

    /// <summary>Open the web page at <see cref="Url"/> in the browser.</summary>
    public sealed record OpenWebPage(string Url) : ViewCommand;

    /// <summary>Close the app, as closing its window does.</summary>
    public sealed record Quit : ViewCommand;

    /// <summary>Open the file at <see cref="Path"/> in the app the OS opens its kind of file with.</summary>
    public sealed record OpenFile(string Path) : ViewCommand;

    /// <summary>Does what the handler of whichever case this is does with it and <paramref name="state"/>, which it
    /// is handed, so that a handler that needs the caller's values can be static and allocate nothing.</summary>
    public void Switch<TState>(TState state, Action<TState, ShowTab> showTab, Action<TState, ShowReopened> showReopened,
        Action<TState, OpenPanel> openPanel, Action<TState, RestoreView> restoreView,
        Action<TState, RevealLine> revealLine, Action<TState, RevealRow> revealRow,
        Action<TState, RevealNode> revealNode, Action<TState, ExpandNodes> expandNodes,
        Action<TState, FocusCard> focusCard, Action<TState, ShowAnalysis> showAnalysis,
        Action<TState, OpenWebPage> openWebPage, Action<TState, Quit> quit, Action<TState, OpenFile> openFile)
    {
        switch (this)
        {
            case ShowTab c:
                showTab(state, c);
                return;
            case ShowReopened c:
                showReopened(state, c);
                return;
            case OpenPanel c:
                openPanel(state, c);
                return;
            case RestoreView c:
                restoreView(state, c);
                return;
            case RevealLine c:
                revealLine(state, c);
                return;
            case RevealRow c:
                revealRow(state, c);
                return;
            case RevealNode c:
                revealNode(state, c);
                return;
            case ExpandNodes c:
                expandNodes(state, c);
                return;
            case FocusCard c:
                focusCard(state, c);
                return;
            case ShowAnalysis c:
                showAnalysis(state, c);
                return;
            case OpenWebPage c:
                openWebPage(state, c);
                return;
            case Quit c:
                quit(state, c);
                return;
            case OpenFile c:
                openFile(state, c);
                return;
            default:
                throw new UnreachableException($"ViewCommand.Switch does not handle {GetType().Name}.");
        }
    }
}
