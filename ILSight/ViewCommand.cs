using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.ILSightAppArea;

namespace ILSight;

/// <summary>What a feature running with no view on the stack commands the view to do, which
/// <see cref="ILSightApp"/> runs on the screen, in the order queued, before it draws the next frame.</summary>
public union ViewCommand(ViewCommand.ShowTab, ViewCommand.ShowReopened, ViewCommand.OpenPanel, ViewCommand.RestoreView,
    ViewCommand.RevealLine, ViewCommand.RevealRow, ViewCommand.RevealNode, ViewCommand.ExpandNodes,
    ViewCommand.FocusCard, ViewCommand.ShowAnalysis, ViewCommand.OpenWebPage, ViewCommand.Quit, ViewCommand.OpenFile)
{
    /// <summary>Bring <see cref="Tab"/>'s panel to the front, docked beside the tab <see cref="Beside"/>
    /// when it is not docked yet.</summary>
    public sealed record ShowTab
    {
        public required DocumentTab Tab { get; init; }

        public required DocumentTab? Beside { get; init; }
    }

    /// <summary>Show the reopened <see cref="Tabs"/> in the panels they were docked as: where the layout
    /// still has them, else as <see cref="Layout"/>, the documents' saved arrangement, has them in the place of
    /// the documents; and return their diagrams' views to how they were saved.</summary>
    public sealed record ShowReopened
    {
        public required IReadOnlyList<ReopenedTab> Tabs { get; init; }

        public required DockLayout? Layout { get; init; }
    }

    /// <summary>Dock <see cref="View"/> if it is not, and bring it to the front.</summary>
    public sealed record OpenPanel(DockView View);

    /// <summary>Return <see cref="Tab"/>'s view to <see cref="Position"/>.</summary>
    public sealed record RestoreView
    {
        public required DocumentTab Tab { get; init; }

        public required ViewPosition Position { get; init; }
    }

    /// <summary>Scroll <see cref="Tab"/>'s code to <see cref="Line"/> and flash it.</summary>
    public sealed record RevealLine
    {
        public required DocumentTab Tab { get; init; }

        public required int Line { get; init; }
    }

    /// <summary>Select <see cref="Tab"/>'s grid row <see cref="Row"/> and scroll to it.</summary>
    public sealed record RevealRow
    {
        public required DocumentTab Tab { get; init; }

        public required int Row { get; init; }
    }

    /// <summary>Expand the tree down to <see cref="Node"/>, select it and scroll to it.</summary>
    public sealed record RevealNode(BrowseNode Node);

    /// <summary>Expand the tree's <see cref="Nodes"/>.</summary>
    public sealed record ExpandNodes(IReadOnlyList<BrowseNode> Nodes);

    /// <summary>Select the card of <see cref="Type"/> on the diagram <see cref="Tab"/> shows, and bring it
    /// into view once it is laid out.</summary>
    public sealed record FocusCard
    {
        public required DocumentTab Tab { get; init; }

        public required CodeReference.Entity Type { get; init; }
    }

    /// <summary>Expand the analyzer's <see cref="Root"/> and select it.</summary>
    public sealed record ShowAnalysis(AnalyzerSymbolNode Root);

    /// <summary>Open the web page at <see cref="Url"/> in the browser.</summary>
    public sealed record OpenWebPage(string Url);

    /// <summary>Close the app, as closing its window does.</summary>
    public sealed record Quit;

    /// <summary>Open the file at <see cref="Path"/> in the app the OS opens its kind of file with.</summary>
    public sealed record OpenFile(string Path);
}
