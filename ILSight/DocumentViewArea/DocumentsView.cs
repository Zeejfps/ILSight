using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.Core.Metadata;
using ILSight.CodeViewArea;
using ILSight.BookmarkArea;
using ILSight.CodeTooltipArea;
using ILSight.OmnibarArea;
using ILSight.CommandArea;
using ILSight.MetadataGridArea;
using ILSight.DiagramArea;
using ILSight.MetadataArea;

namespace ILSight.DocumentViewArea;

/// <summary>An open document's tab content: its omnibar over its code, grid or picture, or a hint while
/// it shows nothing.</summary>
internal static class DocumentsView
{
    /// <summary>The omnibar and content of <paramref name="tab"/>. What its omnibar or grid shows in
    /// <paramref name="navigation"/>'s tabs, a link into <paramref name="session"/>'s nodes among them, shows in this
    /// frame, in <paramref name="setup"/>, in the preview tab while
    /// <see cref="Navigation.PreviewTabs"/>; only its decompile is queued. A comparison it shows compares
    /// <paramref name="assemblies"/> as they are open.</summary>
    public static void Render(Ui<UiState> ui, Navigation navigation, BrowseSession session, AssemblyList assemblies, DecompilerSetup setup, BookmarkList bookmarks, CodeTooltips tooltips, DocumentTab tab)
    {
        using var document = ui.Column(new Box { Height = Sizing.Grow(), Gap = 0f });
        OmnibarView.Render(ui, navigation, assemblies, setup, tab);
        Content(ui, navigation, session, assemblies, setup, bookmarks, tooltips, tab);
    }

    private static void Content(Ui<UiState> ui, Navigation navigation, BrowseSession session, AssemblyList assemblies, DecompilerSetup setup, BookmarkList bookmarks, CodeTooltips tooltips, DocumentTab tab)
    {
        var areaId = ui.GetId("tab-area");
        ui.State.Documents.RecordTabAreaHeight(ui.TryGetRect(areaId, out var area) ? area.Height : ui.Height);
        using var content = ui.Column(areaId, new Box { Height = Sizing.Grow(), Gap = 0f });
        DocumentPanels.GetView(ui.State.Documents, tab).RecordShown(tab);
        ClosedBanner(ui, tab.ClosedAssembly);
        tab.Content.Switch((ui, navigation, session, assemblies, setup, bookmarks, tooltips, tab),
            empty: static (s, _) => Hint(s.ui, s.ui.State.Strings.DocumentsHint),
            loading: static (s, loading) => RenderLoading(s.ui, s.navigation, s.bookmarks, s.tooltips, s.tab, loading),
            code: static (s, _) => RenderCode(s.ui, s.navigation, s.bookmarks, s.tooltips, s.tab),
            table: static (s, table) => RenderGrid(s.ui, s.navigation, s.session, s.setup, s.tab, table.Grid),
            picture: static (s, picture) => ResourceImageView.Render(s.ui, s.navigation, s.bookmarks, s.tooltips, s.tab, picture),
            report: static (s, _) => RenderCode(s.ui, s.navigation, s.bookmarks, s.tooltips, s.tab),
            diagram: static (s, diagram) => DiagramView.Render(s.ui, s.navigation, s.tooltips, s.tab, diagram.Types),
            comparison: static (s, comparison) => ComparisonView.Render(s.ui, s.navigation, s.assemblies, s.tab, comparison.Assemblies),
            failed: static (s, _) => RenderCode(s.ui, s.navigation, s.bookmarks, s.tooltips, s.tab),
            stopped: static (s, _) => RenderCode(s.ui, s.navigation, s.bookmarks, s.tooltips, s.tab),
            reloading: static (s, _) => RenderCode(s.ui, s.navigation, s.bookmarks, s.tooltips, s.tab),
            reopening: static (s, _) => RenderCode(s.ui, s.navigation, s.bookmarks, s.tooltips, s.tab),
            gone: static (s, _) => RenderCode(s.ui, s.navigation, s.bookmarks, s.tooltips, s.tab),
            unreadable: static (s, _) => RenderCode(s.ui, s.navigation, s.bookmarks, s.tooltips, s.tab));
    }

    private static void ClosedBanner(Ui<UiState> ui, string? assembly)
    {
        if (assembly is null)
        {
            return;
        }

        var theme = ui.Theme;
        var text = ui.Memo("closed-banner", (assembly, t: ui.State.Strings), static a => a.t.DocumentClosed(a.assembly));
        using (ui.Row(new Box { Padding = new Spacing(theme.Gap, theme.GapTight), Gap = theme.Gap, Background = theme.PaneBackground }))
        {
            ui.Text(text, color: theme.Warning);
        }
        ui.Separator();
    }

    /// <summary>That <paramref name="closedAssembly"/>, the assembly a tab's document was read from, was closed, as the
    /// tab's bar says it, for what would read it to say why it is unavailable; null while it is open.</summary>
    public static string? FindClosedText(Ui<UiState> ui, string? closedAssembly)
    {
        if (closedAssembly is null)
        {
            return null;
        }

        return ui.Memo("closed", (closedAssembly, t: ui.State.Strings), static a => a.t.DocumentClosed(a.closedAssembly));
    }

    private static void RenderLoading(Ui<UiState> ui, Navigation navigation, BookmarkList bookmarks, CodeTooltips tooltips, DocumentTab tab,
        TabContent.Loading loading)
    {
        if (!loading.Node.IsCode)
        {
            Hint(ui, ui.State.Strings.MetadataLoading);
            return;
        }

        RenderCode(ui, navigation, bookmarks, tooltips, tab);
    }

    private static void RenderGrid(Ui<UiState> ui, Navigation navigation, BrowseSession session, DecompilerSetup setup,
        DocumentTab tab, MetadataGrid grid)
    {
        var view = DocumentPanels.GetView(ui.State.Documents, tab);
        var picked = MetadataGridWidget.RenderMetadataGrid(ui, "grid", view.GetGridState(grid), ui.State.MetadataFilters,
            tab.Load.ClosedAssembly);
        var node = tab.Node;
        if (picked.Target is not null && node is not null)
        {
            var load = navigation.ShowMetadataLink(session, node, picked.Target.Value, picked.InNewTab, setup);
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
        var lines = !tab.Load.IsBusy && tab.Node is not null && tab.Document.Error is null ? bookmarks.ListMarkedLines(tab.Document, tab.Node) : null;
        var view = DocumentPanels.GetView(ui.State.Documents, tab);
        var document = GetDocumentToShow(ui.State, view, tab);
        var result = CodeViewWidget.RenderCodeView(ui, "code", document, view.Code, lines, tooltips: tooltips, display: ui.State.Display,
            closedAssembly: tab.Load.ClosedAssembly);
        if (result.Intent is not null)
        {
            Act(ui, navigation, tab, result.Intent);
        }

        Zoom(ui.State, result.Zoom);
    }

    private static void Act(Ui<UiState> ui, Navigation navigation, DocumentTab tab, CodeViewIntent intent)
    {
        var closed = tab.Load.ClosedAssembly;
        if (closed is not null && !intent.CanRunWhenClosed)
        {
            Commands.RefuseLater(ui.State, new Refusal.AssemblyClosed { Assembly = closed });
            return;
        }

        intent.Switch((ui, navigation, tab),
            navigate: static (s, navigate) => Commands.FollowLater(s.ui.State, navigate.Target, s.navigation.BeginResolving(navigate.InNewTab)),
            toggleBookmark: static (s, toggle) => Commands.Later(s.ui.State, (screen: s.ui.State, s.tab, line: toggle.Line, t: s.ui.State.Strings),
                static (a, s) => ToggleLineBookmark(s.screen, a, s.tab, s.line, s.t)),
            analyze: static (s, analyze) => Commands.AnalyzeLater(s.ui.State, analyze.Entity),
            showInMetadata: static (s, metadata) => Commands.ShowInMetadataLater(s.ui.State, metadata.Target, s.navigation.BeginResolving(inNewTab: false)),
            showInApiReference: static (s, reference) => Commands.Start(s.ui.State, nameof(ShowInApiReferenceHandler), reference.Entity,
                static (a, entity, ct) => a.ShowInApiReferenceHandler.HandleAsync(a.ViewCommands, entity, ct)),
            showInDiagram: static (s, diagram) => DiagramView.ShowInDiagramLater(s.ui.State, diagram.Type, s.ui.State.Strings.DiagramTitle(diagram.Type.Name)),
            showInNewDiagram: static (s, diagram) => DiagramView.ShowInNewDiagramLater(s.ui.State, diagram.Type, s.ui.State.Strings.DiagramTitle(diagram.Type.Name)));
    }

    private static void Zoom(UiState uiState, CodeZoom zoom)
    {
        switch (zoom)
        {
            case CodeZoom.In:
                CodeDisplay.ZoomCode(uiState, 1);
                break;
            case CodeZoom.Out:
                CodeDisplay.ZoomCode(uiState, -1);
                break;
            case CodeZoom.Reset:
                CodeDisplay.ResetCodeZoom(uiState);
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

    private static bool IsNote(TabContent content)
    {
        return content is TabContent.Loading or TabContent.Failed or TabContent.Reloading or TabContent.Reopening or TabContent.Gone
            or TabContent.Unreadable;
    }

    private static void ToggleLineBookmark(UiState screen, AppState appState, DocumentTab tab, int line, Strings t)
    {
        var toggle = Bookmarks.ToggleBookmark(appState, tab, line, number => t.BookmarksDefaultName(number));
        screen.BookmarksPanel.TakeToggle(toggle, appState.Bookmarks.Items);
    }

    public static DockTab Tab(UiState screen, Navigation navigation, DocumentTab tab)
    {
        var title = TitleOf(screen, tab);
        var tooltip = tab.Tooltip ?? title;
        var icon = IconOf(screen, tab);
        var font = TitleFontOf(navigation, tab);
        return new DockTab(title, Tooltip: tooltip, Current: tab == navigation.ActiveTab, Icon: icon, TitleFont: font);
    }

    private static string? TitleFontOf(Navigation navigation, DocumentTab tab)
    {
        if (tab != navigation.PreviewTab)
        {
            return null;
        }

        return TextFonts.Italic;
    }

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

        return screen.Tree.GetLabel(node);
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
