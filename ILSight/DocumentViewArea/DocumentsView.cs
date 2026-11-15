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
    public static void Render(Ui ui, Appearance appearance, IAppCommandWriter commands, DocumentViews documents, BrowseTree tree, Panels panels, ScreenDisplay display,
        BookmarksPanel bookmarksPanel, Omnibar omnibar, MetadataGridScreenState grids, Navigation navigation, BrowseSession session, AssemblyList assemblies, DecompilerSetup setup, BookmarkList bookmarks, CodeTooltips tooltips, DocumentTab tab)
    {
        using var document = ui.Column(new Box { Height = Sizing.Grow(), Gap = 0f });
        OmnibarView.Render(ui, appearance, commands, documents, tree, display, omnibar, navigation, assemblies, setup, tab);
        Content(ui, appearance, commands, documents, panels, display, bookmarksPanel, grids, navigation, session, assemblies, setup, bookmarks, tooltips, tab);
    }

    private static void Content(Ui ui, Appearance appearance, IAppCommandWriter commands, DocumentViews documents, Panels panels, ScreenDisplay display,
        BookmarksPanel bookmarksPanel, MetadataGridScreenState grids, Navigation navigation, BrowseSession session, AssemblyList assemblies, DecompilerSetup setup, BookmarkList bookmarks, CodeTooltips tooltips, DocumentTab tab)
    {
        var areaId = ui.GetId("tab-area");
        documents.RecordTabAreaHeight(ui.TryGetRect(areaId, out var area) ? area.Height : ui.Height);
        using var content = ui.Column(areaId, new Box { Height = Sizing.Grow(), Gap = 0f });
        documents.GetView(tab).RecordShown(tab);
        ClosedBanner(ui, appearance, tab.ClosedAssembly);
        tab.Content.Switch((ui, appearance, commands, documents, panels, display, bookmarksPanel, grids, navigation, session, assemblies, setup, bookmarks, tooltips, tab),
            empty: static (s, _) => Hint(s.ui, s.appearance.Strings.DocumentsHint),
            loading: static (s, loading) => RenderLoading(s.ui, s.appearance, s.commands, s.documents, s.panels, s.display, s.bookmarksPanel, s.navigation, s.bookmarks, s.tooltips, s.tab, loading),
            code: static (s, _) => RenderCode(s.ui, s.appearance, s.commands, s.documents, s.panels, s.display, s.bookmarksPanel, s.navigation, s.bookmarks, s.tooltips, s.tab),
            table: static (s, table) => RenderGrid(s.ui, s.appearance, s.commands, s.documents, s.display, s.grids, s.navigation, s.session, s.setup, s.tab, table.Grid),
            picture: static (s, picture) => ResourceImageView.Render(s.ui, s.appearance, s.commands, s.documents, s.panels, s.display, s.bookmarksPanel, s.navigation, s.bookmarks, s.tooltips, s.tab, picture),
            report: static (s, _) => RenderCode(s.ui, s.appearance, s.commands, s.documents, s.panels, s.display, s.bookmarksPanel, s.navigation, s.bookmarks, s.tooltips, s.tab),
            diagram: static (s, diagram) => DiagramView.Render(s.ui, s.documents, s.display, s.appearance, s.commands, s.navigation, s.tooltips, s.tab, diagram.Types),
            comparison: static (s, comparison) => ComparisonView.Render(s.ui, s.documents, s.appearance, s.display, s.commands, s.navigation, s.assemblies, s.tab, comparison.Assemblies),
            failed: static (s, _) => RenderCode(s.ui, s.appearance, s.commands, s.documents, s.panels, s.display, s.bookmarksPanel, s.navigation, s.bookmarks, s.tooltips, s.tab),
            stopped: static (s, _) => RenderCode(s.ui, s.appearance, s.commands, s.documents, s.panels, s.display, s.bookmarksPanel, s.navigation, s.bookmarks, s.tooltips, s.tab),
            reloading: static (s, _) => RenderCode(s.ui, s.appearance, s.commands, s.documents, s.panels, s.display, s.bookmarksPanel, s.navigation, s.bookmarks, s.tooltips, s.tab),
            reopening: static (s, _) => RenderCode(s.ui, s.appearance, s.commands, s.documents, s.panels, s.display, s.bookmarksPanel, s.navigation, s.bookmarks, s.tooltips, s.tab),
            gone: static (s, _) => RenderCode(s.ui, s.appearance, s.commands, s.documents, s.panels, s.display, s.bookmarksPanel, s.navigation, s.bookmarks, s.tooltips, s.tab),
            unreadable: static (s, _) => RenderCode(s.ui, s.appearance, s.commands, s.documents, s.panels, s.display, s.bookmarksPanel, s.navigation, s.bookmarks, s.tooltips, s.tab));
    }

    private static void ClosedBanner(Ui ui, Appearance appearance, string? assembly)
    {
        if (assembly is null)
        {
            return;
        }

        var theme = ui.Theme;
        var text = ui.Memo("closed-banner", (assembly, t: appearance.Strings), static a => a.t.DocumentClosed(a.assembly));
        using (ui.Row(new Box { Padding = new Spacing(theme.Gap, theme.GapTight), Gap = theme.Gap, Background = theme.PaneBackground }))
        {
            ui.Text(text, color: theme.Warning);
        }
        ui.Separator();
    }

    /// <summary>That <paramref name="closedAssembly"/>, the assembly a tab's document was read from, was closed, as the
    /// tab's bar says it, for what would read it to say why it is unavailable; null while it is open.</summary>
    public static string? FindClosedText(Ui ui, Appearance appearance, string? closedAssembly)
    {
        if (closedAssembly is null)
        {
            return null;
        }

        return ui.Memo("closed", (closedAssembly, t: appearance.Strings), static a => a.t.DocumentClosed(a.closedAssembly));
    }

    private static void RenderLoading(Ui ui, Appearance appearance, IAppCommandWriter commands, DocumentViews documents, Panels panels, ScreenDisplay display, BookmarksPanel bookmarksPanel, Navigation navigation, BookmarkList bookmarks, CodeTooltips tooltips, DocumentTab tab,
        TabContent.Loading loading)
    {
        if (!loading.Node.IsCode)
        {
            Hint(ui, appearance.Strings.MetadataLoading);
            return;
        }

        RenderCode(ui, appearance, commands, documents, panels, display, bookmarksPanel, navigation, bookmarks, tooltips, tab);
    }

    private static void RenderGrid(Ui ui, Appearance appearance, IAppCommandWriter commands, DocumentViews documents, ScreenDisplay display, MetadataGridScreenState grids, Navigation navigation, BrowseSession session, DecompilerSetup setup,
        DocumentTab tab, MetadataGrid grid)
    {
        var view = documents.GetView(tab);
        var picked = MetadataGridWidget.RenderMetadataGrid(ui, grids, appearance, display, commands, "grid", view.GetGridState(grid),
            tab.Load.ClosedAssembly);
        var node = tab.Node;
        if (picked.Target is not null && node is not null)
        {
            var load = navigation.ShowMetadataLink(session, node, picked.Target.Value, picked.InNewTab, setup);
            Commands.DecompileLater(commands, load);
        }

        if (picked.Definition != null)
        {
            Commands.FollowLater(commands, picked.Definition, navigation.BeginResolving(picked.InNewTab));
        }
    }

    /// <summary>The tab's document in the code view, with its bookmarks, or the note saying why it has none. A reference
    /// followed from it begins in <paramref name="navigation"/> in this frame; only its lookup is queued.</summary>
    public static void RenderCode(Ui ui, Appearance appearance, IAppCommandWriter commands, DocumentViews documents, Panels panels, ScreenDisplay display, BookmarksPanel bookmarksPanel, Navigation navigation, BookmarkList bookmarks, CodeTooltips tooltips, DocumentTab tab)
    {
        var lines = !tab.Load.IsBusy && tab.Node is not null && tab.Document.Error is null ? bookmarks.ListMarkedLines(tab.Document, tab.Node) : null;
        var view = documents.GetView(tab);
        var document = GetDocumentToShow(appearance.Strings, view, tab);
        var result = CodeViewWidget.RenderCodeView(ui, appearance, display, "code", document, view.Code, lines, tooltips: tooltips, display: display.Options,
            closedAssembly: tab.Load.ClosedAssembly);
        if (result.Intent is not null)
        {
            Act(ui, appearance, commands, panels, bookmarksPanel, navigation, tab, result.Intent);
        }

        Zoom(display, appearance, result.Zoom);
    }

    private static void Act(Ui ui, Appearance appearance, IAppCommandWriter commands, Panels panels, BookmarksPanel bookmarksPanel, Navigation navigation, DocumentTab tab, CodeViewIntent intent)
    {
        var closed = tab.Load.ClosedAssembly;
        if (closed is not null && !intent.CanRunWhenClosed)
        {
            Commands.RefuseLater(commands, new Refusal.AssemblyClosed { Assembly = closed });
            return;
        }

        intent.Switch((appearance, commands, panels, bookmarksPanel, navigation, tab),
            navigate: static (s, navigate) => Commands.FollowLater(s.commands, navigate.Target, s.navigation.BeginResolving(navigate.InNewTab)),
            toggleBookmark: static (s, toggle) => Commands.Later(s.commands, (bookmarksPanel: s.bookmarksPanel, s.tab, line: toggle.Line, t: s.appearance.Strings),
                static (a, s) => ToggleLineBookmark(s.bookmarksPanel, a, s.tab, s.line, s.t)),
            analyze: static (s, analyze) => Commands.AnalyzeLater(s.commands, s.panels, analyze.Entity),
            showInMetadata: static (s, metadata) => Commands.ShowInMetadataLater(s.commands, metadata.Target, s.navigation.BeginResolving(inNewTab: false)),
            showInApiReference: static (s, reference) => Commands.Start(s.commands, nameof(ShowInApiReferenceHandler), reference.Entity,
                static (a, entity, ct) => a.ShowInApiReferenceHandler.HandleAsync(a.ViewCommands, entity, ct)),
            showInDiagram: static (s, diagram) => DiagramView.ShowInDiagramLater(s.commands, diagram.Type, s.appearance.Strings.DiagramTitle(diagram.Type.Name)),
            showInNewDiagram: static (s, diagram) => DiagramView.ShowInNewDiagramLater(s.commands, diagram.Type, s.appearance.Strings.DiagramTitle(diagram.Type.Name)));
    }

    private static void Zoom(ScreenDisplay display, Appearance appearance, CodeZoom zoom)
    {
        switch (zoom)
        {
            case CodeZoom.In:
                display.ZoomCode(appearance.Theme, 1);
                break;
            case CodeZoom.Out:
                display.ZoomCode(appearance.Theme, -1);
                break;
            case CodeZoom.Reset:
                display.ResetCodeZoom();
                break;
        }
    }

    private static DecompiledDocument GetDocumentToShow(Strings strings, TabView view, DocumentTab tab)
    {
        var content = tab.Content;
        if (content is TabContent.Report report)
        {
            return view.Report.GetDocument(report.Export, strings);
        }

        if (!IsNote(content))
        {
            return tab.Document;
        }

        return view.Placeholder.GetDocument(content, strings);
    }

    private static bool IsNote(TabContent content)
    {
        return content is TabContent.Loading or TabContent.Failed or TabContent.Reloading or TabContent.Reopening or TabContent.Gone
            or TabContent.Unreadable;
    }

    private static void ToggleLineBookmark(BookmarksPanel bookmarksPanel, AppState appState, DocumentTab tab, int line, Strings t)
    {
        var toggle = Bookmarks.ToggleBookmark(appState, tab, line, number => t.BookmarksDefaultName(number));
        bookmarksPanel.TakeToggle(toggle, appState.Bookmarks.Items);
    }

    public static DockTab Tab(Appearance appearance, BrowseTree tree, Navigation navigation, DocumentTab tab)
    {
        var title = TitleOf(appearance, tree, tab);
        var tooltip = tab.Tooltip ?? title;
        var icon = IconOf(appearance, tab);
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

    private static UiIcon IconOf(Appearance appearance, DocumentTab tab)
    {
        if (tab.Diagram is not null)
        {
            return new UiIcon(appearance.Icons.TabDiagram);
        }

        var node = tab.Node;
        if (node is null)
        {
            return new UiIcon(appearance.Icons.TabPage);
        }

        return NodeIcons.Icon(node, appearance.NodeIconPalette, appearance.Icons);
    }

    private static string TitleOf(Appearance appearance, BrowseTree tree, DocumentTab tab)
    {
        if (tab.Content is TabContent.Report)
        {
            return appearance.Strings.ExportReportTitle;
        }

        var node = tab.Node;
        if (node is null)
        {
            return tab.Title ?? appearance.Strings.DocumentNewTab;
        }

        return tree.GetLabel(node);
    }

    /// <summary>Muted <paramref name="text"/> centered in a pane or area with nothing to show yet.</summary>
    public static void Hint(Ui ui, string text)
    {
        var theme = ui.Theme;
        using (ui.Column(new Box { Height = Sizing.Grow(), Padding = theme.ShellPaddingX, AlignX = Align.Center, AlignY = Align.Center, Background = theme.PaneBackground }))
        {
            ui.Text(text, color: theme.TextMuted, wrap: true, align: Align.Center);
        }
    }
}
