using System.Diagnostics;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.Core.Metadata;
using ILSight.CodeView;
using ILSight.Bookmarking;
using ILSight.CodeTooltipping;
using ILSight.Omnibars;
using ILSight.Commanding;
using ILSight.MetadataGridArea;
using ILSight.Diagramming;
using static ILSight.Bookmarking.Bookmarks;
using static ILSight.CodeDisplay;
using static ILSight.Diagramming.Diagrams;

namespace ILSight.DocumentViewing;

/// <summary>An open document's tab content: its omnibar over its code, grid or picture, or a hint while
/// it shows nothing.</summary>
internal static class DocumentsView
{
    /// <summary>The omnibar and content of <paramref name="tab"/>. What its omnibar or grid shows in
    /// <paramref name="navigation"/>'s tabs, a link into <paramref name="session"/>'s nodes among them, shows in this
    /// frame, in <paramref name="language"/> with <paramref name="options"/>, in the preview tab while
    /// <paramref name="previewTabs"/>; only its decompile is queued. A comparison it shows compares
    /// <paramref name="assemblies"/> as they are open.</summary>
    public static void Render(Ui<UiState> ui, Navigation navigation, BrowseSession session, AssemblyList assemblies, DecompilationLanguage language,
        DecompilerOptions options, bool previewTabs, BookmarkList bookmarks, CodeTooltips tooltips, DocumentTab tab)
    {
        using var document = ui.Column(new Box { Height = Sizing.Grow(), Gap = 0f });
        OmnibarView.Render(ui, navigation, assemblies, language, options, previewTabs, tab);
        Content(ui, navigation, session, assemblies, language, options, previewTabs, bookmarks, tooltips, tab);
    }

    private static void Content(Ui<UiState> ui, Navigation navigation, BrowseSession session, AssemblyList assemblies, DecompilationLanguage language,
        DecompilerOptions options, bool previewTabs, BookmarkList bookmarks, CodeTooltips tooltips, DocumentTab tab)
    {
        var t = ui.State.Strings;
        DocumentPanels.RecordShown(DocumentPanels.GetView(ui.State.Documents, tab), tab);
        switch (tab.Content)
        {
            case TabContent.Empty:
                Hint(ui, t.DocumentsHint);
                break;
            case TabContent.Picture picture:
                ResourceImageView.Render(ui, navigation, bookmarks, tooltips, tab, picture);
                break;
            case TabContent.Table table:
                RenderGrid(ui, navigation, session, language, options, previewTabs, tab, table.Grid);
                break;
            case TabContent.Diagram diagram:
                DiagramView.Render(ui, navigation, tooltips, tab, diagram.Types);
                break;
            case TabContent.Comparison comparison:
                ComparisonView.Render(ui, navigation, assemblies, tab, comparison.Assemblies);
                break;
            case TabContent.Loading loading when !NodeContentLoader.DependsOnLanguage(loading.Node):
                Hint(ui, t.MetadataLoading);
                break;
            case TabContent.Code or TabContent.Report or TabContent.Loading or TabContent.Failed or TabContent.Reloading
                or TabContent.Reopening or TabContent.Gone:
                RenderCode(ui, navigation, bookmarks, tooltips, tab);
                break;
            default:
                throw new UnreachableException();
        }
    }

    private static void RenderGrid(Ui<UiState> ui, Navigation navigation, BrowseSession session, DecompilationLanguage language, DecompilerOptions options,
        bool previewTabs, DocumentTab tab, MetadataGrid grid)
    {
        var view = DocumentPanels.GetView(ui.State.Documents, tab);
        var picked = MetadataGridWidget.RenderMetadataGrid(ui, "grid", DocumentPanels.GetGridState(view, grid), ui.State.MetadataFilters);
        var node = tab.Node;
        if (picked.Target is not null && node is not null)
        {
            var load = navigation.ShowMetadataLink(session, node, picked.Target.Value, picked.InNewTab, language, options, previewTabs);
            Commands.DecompileLater(ui.State, load);
        }

        if (picked.Definition != null)
        {
            Commands.FollowLater(ui.State, picked.Definition, navigation.BeginResolving(picked.InNewTab));
        }
    }

    /// <summary>The tab's document in the code view, with its bookmarks, or the note saying why it has none. A reference
    /// followed from it begins in <paramref name="navigation"/> in this frame; only its lookup is queued.</summary>
    public static void RenderCode(Ui<UiState> ui, Navigation navigation, BookmarkList bookmarks, CodeTooltips tooltips, DocumentTab tab)
    {
        var lines = !tab.IsBusy && tab.Node is not null && tab.Document.Error is null ? bookmarks.ListMarkedLines(tab.Document, tab.Node) : null;
        var view = DocumentPanels.GetView(ui.State.Documents, tab);
        var document = GetDocumentToShow(ui.State, view, tab);
        var result = CodeViewWidget.RenderCodeView(ui, "code", document, view.Code, lines, tooltips: tooltips, display: ui.State.Display);
        if (result.Intent is not null)
        {
            Act(ui, navigation, tab, result.Intent);
        }

        Zoom(ui.State, result.Zoom);
    }

    /// <summary>Does what the user asked the code view of <paramref name="tab"/> to do.</summary>
    private static void Act(Ui<UiState> ui, Navigation navigation, DocumentTab tab, CodeViewIntent intent)
    {
        var t = ui.State.Strings;
        switch (intent)
        {
            case CodeViewIntent.Navigate navigate:
                Commands.FollowLater(ui.State, navigate.Target, navigation.BeginResolving(navigate.InNewTab));
                break;
            case CodeViewIntent.ToggleBookmark toggle:
                Commands.Later(ui.State, (screen: ui.State, tab, line: toggle.Line, t), static (a, s) => ToggleLineBookmark(s.screen, a, s.tab, s.line, s.t));
                break;
            case CodeViewIntent.Analyze analyze:
                Commands.AnalyzeLater(ui.State, analyze.Entity);
                break;
            case CodeViewIntent.ShowInMetadata metadata:
                Commands.ShowInMetadataLater(ui.State, metadata.Target, navigation.BeginResolving(inNewTab: false));
                break;
            case CodeViewIntent.ShowInApiReference reference:
                Commands.Start(ui.State, nameof(ShowInApiReferenceHandler), reference.Entity, static (a, entity, ct) => a.ShowInApiReferenceHandler.HandleAsync(a.ViewCommands, entity, ct));
                break;
            case CodeViewIntent.ShowInDiagram diagram:
                DiagramView.ShowInDiagramLater(ui.State, diagram.Type, t.DiagramTitle(diagram.Type.Name));
                break;
            case CodeViewIntent.ShowInNewDiagram diagram:
                DiagramView.ShowInNewDiagramLater(ui.State, diagram.Type, t.DiagramTitle(diagram.Type.Name));
                break;
            default:
                throw new UnreachableException();
        }
    }

    private static void Zoom(UiState uiState, CodeZoom zoom)
    {
        switch (zoom)
        {
            case CodeZoom.In:
                ZoomCode(uiState, 1);
                break;
            case CodeZoom.Out:
                ZoomCode(uiState, -1);
                break;
            case CodeZoom.Reset:
                ResetCodeZoom(uiState);
                break;
        }
    }

    private static DecompiledDocument GetDocumentToShow(UiState screen, TabView view, DocumentTab tab)
    {
        var content = tab.Content;
        if (content is TabContent.Report report)
        {
            return ShownReports.GetDocument(view.Report, report.Export, screen.Strings);
        }

        if (!IsNote(content))
        {
            return tab.Document;
        }

        return TabPlaceholders.GetDocument(view.Placeholder, content, screen.Strings);
    }

    /// <summary>Whether <paramref name="content"/> is shown as a note saying why there is no code.</summary>
    private static bool IsNote(TabContent content)
    {
        return content is TabContent.Loading or TabContent.Failed or TabContent.Reloading or TabContent.Reopening or TabContent.Gone;
    }

    /// <summary>Toggles the bookmark on <paramref name="line"/>, selecting the one made, or passing the
    /// selection of the one removed on.</summary>
    private static void ToggleLineBookmark(UiState screen, AppState appState, DocumentTab tab, int line, Strings t)
    {
        var toggle = ToggleBookmark(appState, tab, line, number => t.BookmarksDefaultName(number));
        TakeToggle(screen.BookmarksPanel, toggle, appState.Bookmarks.Items);
    }

    public static DockTab Tab(UiState screen, DocumentTabs tabs, DocumentTab tab)
    {
        var title = TitleOf(screen, tab);
        var tooltip = tab.Tooltip ?? title;
        var icon = IconOf(screen, tab);
        var font = TitleFontOf(tabs, tab);
        return new DockTab(title, Tooltip: tooltip, Current: tab == tabs.Active, Icon: icon, TitleFont: font);
    }

    /// <summary>Italics for the preview tab's title, the theme's font for any other.</summary>
    private static string? TitleFontOf(DocumentTabs tabs, DocumentTab tab)
    {
        if (tab != tabs.Preview)
        {
            return null;
        }

        return TextFonts.Italic;
    }

    /// <summary>The tab's icon: its node's as the tree shows it, a diagram's, else a page.</summary>
    private static UiIcon IconOf(UiState screen, DocumentTab tab)
    {
        if (tab.Diagram is not null)
        {
            return new UiIcon(screen.Icons.TabDiagram);
        }

        var node = tab.Node;
        if (node is null)
        {
            return new UiIcon(screen.Icons.TabPage);
        }

        return NodeIcons.Icon(node, screen.NodeIconPalette, screen.Icons);
    }

    /// <summary>The tab's title: its node's name as the tree labels it, a report's, else its own title, else a new tab's.</summary>
    private static string TitleOf(UiState screen, DocumentTab tab)
    {
        if (tab.Content is TabContent.Report)
        {
            return screen.Strings.ExportReportTitle;
        }

        var node = tab.Node;
        if (node is null)
        {
            return tab.Title ?? screen.Strings.DocumentNewTab;
        }

        return Trees.GetLabel(screen.Tree, node);
    }

    /// <summary>Muted <paramref name="text"/> centered in a pane or area with nothing to show yet.</summary>
    public static void Hint(Ui<UiState> ui, string text)
    {
        var theme = ui.Theme;
        using (ui.Column(new Box { Height = Sizing.Grow(), Padding = theme.ShellPaddingX, AlignX = Align.Center, AlignY = Align.Center, Background = theme.PaneBackground }))
        {
            ui.Text(text, color: theme.TextMuted, wrap: true, align: Align.Center);
        }
    }
}
