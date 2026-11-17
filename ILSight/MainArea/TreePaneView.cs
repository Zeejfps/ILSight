using ZGF.Desktop;
using ILSight.ComparisonPickerArea;
using ZGF.Geometry;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.Core.AnalysisArea;
using ILSight.ExportArea;
using ILSight.CommandArea;
using ILSight.DiagramArea;
using ILSight.AssemblyArea;
using ILSight.SearchArea;

namespace ILSight.MainArea;

internal static class TreePaneView
{
    private const string TreeId = "tree";
    private const string MenuId = "tree-menu";
    private const string ToolbarId = "tree-toolbar";

    // The separators count as tools.
    private const int ToolCount = 10;

    /// <summary>The tree and its toolbar. A node picked or opened shows in <paramref name="navigation"/>'s tabs in this
    /// frame, in <paramref name="setup"/>, in the preview tab while
    /// <see cref="Navigation.PreviewTabs"/>; only its decompile is queued.</summary>
    public static void Render(Ui ui, Appearance appearance, IAppCommandWriter commands, Panels panels, BrowseTree tree, AnalyzerRows analyzerRows, ScreenDisplay display, Search search, ComparisonPicker comparisonPicker, AssemblyCloser closer, ExportSetup exportSetup, Navigation navigation, AssemblyList assemblies, DecompilerSetup setup)
    {
        var theme = ui.Theme;
        tree.SetStyle(theme, appearance.NodeIconPalette, appearance.Icons, appearance.Strings);
        tree.ShowTokens(display.Options.TreeTokens);
        tree.ShowEmptyTables(display.Options.ShowEmptyMetadataTables);
        tree.NestNamespaces(display.Options.NestedNamespaces);
        using var pane = ui.Column(new Box { Height = Sizing.Grow(), Gap = 0f });
        Toolbar(ui, appearance, tree, commands, closer, navigation, assemblies);
        ui.Separator();
        if (tree.Roots.Count == 0)
        {
            EmptyHint(ui, appearance);
        }

        var result = ui.TreeView(TreeId, tree.Roots, tree, tree.View, new Box { Padding = new Spacing(0f, theme.GapTight) });
        AnalyzeOnShortcut(ui, commands, panels, tree, analyzerRows);
        CloseOnDelete(ui, tree, commands);
        Respond(ui, commands, tree, navigation, setup, result);
        ContextMenu(ui, appearance, commands, panels, tree, analyzerRows, search, comparisonPicker, exportSetup, navigation, assemblies, setup);
    }

    private static void Toolbar(Ui ui, Appearance appearance, BrowseTree tree, IAppCommandWriter commands, AssemblyCloser closer, Navigation navigation, AssemblyList assemblies)
    {
        var toolbarId = ui.GetId(ToolbarId);
        // Worked out from the width the toolbar had last frame, so the first frame shows them all.
        var shown = CountShownTools(ui, toolbarId);
        using (ui.PaneToolbar(toolbarId, new Box { Width = Sizing.Grow(), Clip = true }))
        {
            for (var i = 0; i < shown; i++)
            {
                Tool(ui, appearance, tree, commands, closer, navigation, assemblies, i, shown, inMenu: false);
            }

            if (shown == ToolCount)
            {
                return;
            }

            using var menu = ui.DropdownButton("more", appearance.Strings.TreeMoreActions, glyph: appearance.Icons.AssembliesMoreActions);
            if (!menu.IsOpen)
            {
                return;
            }

            for (var i = shown; i < ToolCount; i++)
            {
                Tool(ui, appearance, tree, commands, closer, navigation, assemblies, i, shown, inMenu: true);
            }
        }
    }

    private static bool IsToolSeparator(int index)
    {
        return index == 3 || index == 6;
    }

    private static int CountShownTools(Ui ui, int toolbarId)
    {
        if (!ui.TryGetRect(toolbarId, out var rect))
        {
            return ToolCount;
        }

        var theme = ui.Theme;
        var room = rect.Width - 2f * theme.ShellPaddingX;
        var allWidth = MeasureTools(theme, ToolCount);
        if (allWidth <= room)
        {
            return ToolCount;
        }

        var beside = room - theme.GapTight - theme.IconButtonSize;
        var shown = 0;
        while (shown < ToolCount)
        {
            var width = MeasureTools(theme, shown + 1);
            if (width > beside)
            {
                break;
            }

            shown++;
        }

        return shown;
    }

    private static float MeasureTools(UiTheme theme, int count)
    {
        var width = 0f;
        for (var i = 0; i < count; i++)
        {
            if (i > 0)
            {
                width += theme.GapTight;
            }

            width += IsToolSeparator(i) ? theme.SeparatorWidth : theme.IconButtonSize;
        }
        return width;
    }

    private static void Tool(Ui ui, Appearance appearance, BrowseTree tree, IAppCommandWriter commands, AssemblyCloser closer, Navigation navigation, AssemblyList assemblies, int index, int shown, bool inMenu)
    {
        var t = appearance.Strings;
        var icons = appearance.Icons;
        var any = tree.Roots.Count > 0;
        switch (index)
        {
            case 0:
            {
                var opened = FindOpenedNode(navigation);
                if (DrawTool(ui, inMenu, "select-opened", icons.AssembliesSelectOpened, t.TreeSelectOpenedNode, opened is not null) && opened is not null)
                    {
                        SelectOpenedNode(ui, tree, assemblies, opened);
                    }

                    return;
            }
            case 1:
                if (DrawTool(ui, inMenu, "collapse", icons.AssembliesCollapseAll, t.CodeCollapseAll, any))
                {
                    tree.CollapseAll();
                }

                return;
            case 2:
                if (DrawTool(ui, inMenu, "expand", icons.AssembliesExpandNamespaces, t.TreeExpandNamespaces, any))
                {
                    var expansion = tree.ExpandNamespaces();
                    Commands.ExpandNamespacesLater(commands, tree, expansion);
                }

                return;
            case 3:
            case 6:
                DrawToolSeparator(ui, inMenu, index, shown);
                return;
            case 4:
                if (DrawTool(ui, inMenu, "sort-list", icons.AssembliesSortList, t.TreeSortList, any))
                {
                    Commands.Later(commands, static a => a.ListSession.Assemblies.SortListed());
                }

                return;
            case 5:
            {
                var label = inMenu ? t.TreeReloadAll : ui.Memo("reload-all-tip", t, static s => s.TreeReloadAllTip(Shortcuts.ReloadAll));
                if (DrawTool(ui, inMenu, "reload-all", icons.AssembliesReloadAll, label, any, Shortcuts.ReloadAll))
                    {
                        Commands.ReloadLater(commands, tree.ListExpandedPaths());
                    }

                    return;
            }
            case 7:
                if (DrawTool(ui, inMenu, "close-assembly", icons.AssembliesCloseAssembly, t.TreeCloseAssemblies, any))
                {
                    closer.Open(assemblies, tree.View.Selected?.Assembly);
                }

                return;
            case 8:
                if (DrawTool(ui, inMenu, "close-failed", icons.AssembliesCloseFailed, t.TreeCloseFailed, assemblies.HasFailedAssemblies))
                {
                    Commands.Later(commands, static a => Commands.CloseFailedAssemblies(a));
                }

                return;
            case 9:
                if (DrawTool(ui, inMenu, "clear-list", icons.AssembliesClearList, t.TreeClearList, any))
                {
                    Commands.Later(commands, static a => Commands.CloseAllAssemblies(a));
                }

                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(index), index, "The assemblies panel has no such tool.");
        }
    }

    private static bool DrawTool(Ui ui, bool inMenu, string id, IconGlyph glyph, string label, bool enabled, Shortcut? shortcut = null)
    {
        if (inMenu)
        {
            return ui.MenuItem(label, shortcut, enabled, id: id, icon: new UiIcon(glyph));
        }

        return ui.IconButton(id, glyph, label, enabled);
    }

    // A separator is drawn only between two tools in the same place.
    private static void DrawToolSeparator(Ui ui, bool inMenu, int index, int shown)
    {
        if (inMenu)
        {
            if (index > shown)
            {
                ui.MenuSeparator();
            }

            return;
        }
        if (index + 1 < shown)
        {
            ui.Separator(ui.Theme.IconSize);
        }
    }

    private static BrowseNode? FindOpenedNode(Navigation navigation)
    {
        var tab = navigation.ActiveTab;
        if (tab is null)
        {
            return null;
        }

        return tab.Node;
    }

    private static void SelectOpenedNode(Ui ui, BrowseTree tree, AssemblyList assemblies, BrowseNode node)
    {
        if (!assemblies.IsListed(node.Root.Assembly))
        {
            return;
        }

        tree.RevealNode(node);
        ui.Focus(ui.GetId(TreeId), takesText: false);
    }

    private static BrowseNode? FindPickedAssembly(BrowseTree tree)
    {
        var node = tree.View.Selected;
        if (node is null)
        {
            return null;
        }

        if (!IsListedRoot(node))
        {
            return null;
        }

        return node;
    }

    // An assembly read out of a package is not the root of an item of the list.
    private static bool IsListedRoot(BrowseNode node)
    {
        return node.Parent is null;
    }

    private static void EmptyHint(Ui ui, Appearance appearance)
    {
        var t = appearance.Strings;
        var theme = ui.Theme;
        var text = ui.Memo("empty", t, static s => s.TreeEmpty(Shortcuts.Open));
        ui.Text(text, new Box { Width = Sizing.Grow(), Padding = new Spacing(theme.PanelPadding, theme.Gap) }, color: theme.TextMuted, wrap: true);
    }

    private static void AnalyzeOnShortcut(Ui ui, IAppCommandWriter commands, Panels panels, BrowseTree tree, AnalyzerRows analyzerRows)
    {
        var treeId = ui.GetId(TreeId);
        if (!ui.HasFocus(treeId))
        {
            return;
        }

        if (!ui.IsPressed(Shortcuts.Analyze))
        {
            return;
        }

        var picked = tree.View.Selected;
        if (picked is null)
        {
            return;
        }

        var analysis = picked.AnalysisFacts;
        if (analysis is null)
        {
            return;
        }

        Commands.AnalyzeLater(commands, panels, analyzerRows, analysis);
    }

    private static void CloseOnDelete(Ui ui, BrowseTree tree, IAppCommandWriter commands)
    {
        var treeId = ui.GetId(TreeId);
        if (!ui.HasFocus(treeId))
        {
            return;
        }

        var picked = FindPickedAssembly(tree);
        if (picked is null)
        {
            return;
        }

        if (!ui.IsPressed(Shortcuts.Delete))
        {
            return;
        }

        Commands.Later(commands, picked.Assembly, static (a, assembly) => Commands.CloseAssemblies(a, [assembly]));
    }

    private static void Respond(Ui ui, IAppCommandWriter commands, BrowseTree tree, Navigation navigation, DecompilerSetup setup,
        TreeViewResult<BrowseNode> result)
    {
        var node = result.Node;
        if (node is null)
        {
            return;
        }

        if (IsPicked(result))
        {
            Preview(commands, navigation, node, setup);
        }

        if (result.Activated)
        {
            Open(commands, navigation, node, setup);
        }

        if (result.ContextMenuRequested)
        {
            OpenContextMenu(ui, tree, node, result.ContextMenuAt);
        }
    }

    private static bool IsPicked(TreeViewResult<BrowseNode> result)
    {
        if (!result.SelectionChanged)
        {
            return false;
        }

        return !result.ContextMenuRequested;
    }

    private static void Preview(IAppCommandWriter commands, Navigation navigation, BrowseNode node, DecompilerSetup setup)
    {
        if (!navigation.PreviewTabs)
        {
            return;
        }

        Commands.DecompileLater(commands, navigation.PreviewNode(node, setup));
    }

    private static void Open(IAppCommandWriter commands, Navigation navigation, BrowseNode node, DecompilerSetup setup)
    {
        var activation = node.Activation;
        if (activation is Activation.Navigate jump)
        {
            var resolveTicket = navigation.BeginResolving(inNewTab: false);
            Commands.GoToReferenceLater(commands, jump.Target, resolveTicket);
            return;
        }
        var decompileTicket = navigation.OpenNode(node, setup);
        Commands.DecompileLater(commands, decompileTicket);
    }

    private static void OpenContextMenu(Ui ui, BrowseTree tree, BrowseNode node, PointF at)
    {
        tree.OpenMenuOn(node);
        ui.OpenContextMenu(MenuId, at);
    }

    private static void ContextMenu(Ui ui, Appearance appearance, IAppCommandWriter commands, Panels panels, BrowseTree tree, AnalyzerRows analyzerRows, Search search, ComparisonPicker comparisonPicker, ExportSetup exportSetup, Navigation navigation, AssemblyList assemblies, DecompilerSetup setup)
    {
        using var menu = ui.ContextMenu(MenuId);
        if (!menu.IsOpen)
        {
            return;
        }

        var node = tree.MenuTarget;
        if (node is null)
        {
            return;
        }

        NodeMenu(ui, appearance, commands, panels, analyzerRows, search, comparisonPicker, exportSetup, navigation, assemblies, setup, node);
    }

    private static void NodeMenu(Ui ui, Appearance appearance, IAppCommandWriter commands, Panels panels, AnalyzerRows analyzerRows, Search search, ComparisonPicker comparisonPicker, ExportSetup exportSetup, Navigation navigation, AssemblyList assemblies, DecompilerSetup setup,
        BrowseNode node)
    {
        var t = appearance.Strings;
        var jump = node.Activation is Activation.Navigate navigate ? navigate : null;
        var openKey = jump != null ? null : Shortcuts.Enter;
        if (ui.MenuItem(t.TreeOpen, openKey, id: "open"))
        {
            var decompileTicket = navigation.OpenNode(node, setup);
            Commands.DecompileLater(commands, decompileTicket);
        }

        if (ui.MenuItem(t.TreeOpenInNewTab, id: "open-in-new-tab"))
        {
            var decompileTicket = navigation.ShowNode(node, setup, inNewTab: true);
            Commands.DecompileLater(commands, decompileTicket);
        }

        if (jump != null && ui.MenuItem(t.TreeGoToTarget, Shortcuts.Enter, id: "go-to-target"))
        {
            var resolveTicket = navigation.BeginResolving(inNewTab: false);
            Commands.GoToReferenceLater(commands, jump.Target, resolveTicket);
        }

        if (ui.MenuItem(t.TreeCopyFullName, id: "copy-full-name"))
        {
            ui.ClipboardText = node.FullName;
        }

        DocsItem(ui, appearance, node);
        var analysis = node.AnalysisFacts;
        if (analysis is not null && ui.MenuItem(t.TreeAnalyze, Shortcuts.Analyze, id: "analyze"))
        {
            Commands.AnalyzeLater(commands, panels, analyzerRows, analysis);
        }

        var definition = node.Reference;
        if (definition != null && ui.MenuItem(t.TreeShowInMetadata, id: "show-in-metadata"))
        {
            var resolveTicket = navigation.BeginResolving(inNewTab: false);
            Commands.ShowInMetadataLater(commands, definition, resolveTicket);
        }

        if (node.Subject is NodeSubject.TypeDefinition type)
        {
            DiagramItems(ui, appearance, commands, type.Definition.Reference);
        }

        if (node.HasResource && ui.MenuItem(t.ResourceExport, id: "export-resource", icon: new UiIcon(appearance.Icons.ResourceExport)))
        {
            ResourceImageView.PickExport(ui, appearance, commands, node);
        }

        ExtractItems(ui, appearance, commands, node);
        if (OffersSaveCode(node) && ui.MenuItem(t.FileSaveCode, Shortcuts.SaveCode, id: "save-code", icon: new UiIcon(appearance.Icons.FileSaveCode)))
        {
            SaveDialogs.PickCodeFile(ui, appearance, commands, setup, node);
        }

        if (node.Subject is NodeSubject.Module module)
        {
            AssemblyItems(ui, appearance, exportSetup, commands, comparisonPicker, assemblies, module.Value);
        }

        if (node.Subject is NodeSubject.Module && ui.MenuItem(t.TreeScopeSearchToAssembly, id: "scope-search"))
        {
            ScreenLayout.ScopeSearch(search, panels, node);
        }

        if (node.Subject is NodeSubject.Namespace && ui.MenuItem(t.TreeScopeSearchToNamespace, id: "scope-search"))
        {
            ScreenLayout.ScopeSearch(search, panels, node);
        }

        if (IsListedRoot(node) && ui.MenuItem(t.TreeOpenContainingFolder, id: "open-containing-folder", icon: new UiIcon(appearance.Icons.AssembliesOpenContainingFolder)))
        {
            ui.RevealFile(node.Assembly.FilePath.Text);
        }

        if (IsListedRoot(node))
        {
            ui.MenuSeparator();
            if (ui.MenuItem(t.TreeCloseAssembly, id: "close-assembly"))
            {
                Commands.Later(commands, node.Assembly, static (a, assembly) => Commands.CloseAssemblies(a, [assembly]));
            }
        }
    }

    private static void DiagramItems(Ui ui, Appearance appearance, IAppCommandWriter commands, CodeReference.Entity type)
    {
        var t = appearance.Strings;
        if (ui.MenuItem(t.TreeShowInDiagram, id: "show-in-diagram"))
        {
            DiagramView.ShowInDiagramLater(commands, type, new TabTitle.Diagram(type.Name));
        }

        if (ui.MenuItem(t.TreeShowInNewDiagram, id: "show-in-new-diagram"))
        {
            DiagramView.ShowInNewDiagramLater(commands, type, new TabTitle.Diagram(type.Name));
        }
    }

    private static void DocsItem(Ui ui, Appearance appearance, BrowseNode node)
    {
        var t = appearance.Strings;
        var page = MicrosoftDocs.FindPage(node);
        if (page is null)
        {
            return;
        }

        if (ui.MenuItem(t.TreeShowInApiReference, id: "show-in-api-reference", icon: new UiIcon(appearance.Icons.LinkApiReference)))
        {
            ui.OpenUrl(page);
        }
    }

    private static void ExtractItems(Ui ui, Appearance appearance, IAppCommandWriter commands, BrowseNode node)
    {
        var t = appearance.Strings;
        var file = PackedAssemblyOf(node);
        if (file != null && ui.MenuItem(t.TreeExtractFile, id: "extract-file", icon: new UiIcon(appearance.Icons.AssembliesExtract)))
        {
            SaveDialogs.PickPackageFile(ui, appearance, commands, file);
        }

        var folder = node.FolderInPackage;
        if (folder != null && ui.MenuItem(t.TreeExtractAll, id: "extract-all", icon: new UiIcon(appearance.Icons.AssembliesExtract)))
        {
            SaveDialogs.PickExtractFolder(ui, appearance, commands, node.Text, folder);
        }
    }

    private static PackagePlace? PackedAssemblyOf(BrowseNode node)
    {
        if (node.Subject is not NodeSubject.Module module)
        {
            return null;
        }

        return module.Value.Place;
    }

    private static bool OffersSaveCode(BrowseNode node)
    {
        // A resource's own Export item saves its bytes.
        if (node.HasResource)
        {
            return false;
        }

        return node.IsCode;
    }

    private static void AssemblyItems(Ui ui, Appearance appearance, ExportSetup exportSetup, IAppCommandWriter commands, ComparisonPicker comparisonPicker, AssemblyList assemblies, LoadedModule assembly)
    {
        var t = appearance.Strings;
        if (ui.MenuItem(t.FileExport, id: "export", icon: new UiIcon(appearance.Icons.FileExportProject)))
        {
            exportSetup.Open(assemblies.Assemblies, assembly);
        }

        PdbItems(ui, appearance, commands, assembly);
        if (ui.MenuItem(t.TreeCreateDiagram, id: "create-diagram"))
        {
            SaveDialogs.PickDiagramFolder(ui, appearance, commands, assembly);
        }

        if (ui.MenuItem(t.TreeCompareWith, id: "compare-with"))
        {
            comparisonPicker.Open(assembly);
        }

        TargetFrameworkMenu(ui, appearance, commands, assembly);
        if (ui.MenuItem(t.TreeLoadDependencies, id: "load-dependencies"))
        {
            Commands.OpenDependenciesLater(commands, assembly);
        }
    }

    private static void PdbItems(Ui ui, Appearance appearance, IAppCommandWriter commands, LoadedModule assembly)
    {
        var t = appearance.Strings;
        if (ui.MenuItem(t.TreeGeneratePdb, enabled: assembly.CanGeneratePdb, id: "generate-pdb", icon: new UiIcon(appearance.Icons.FileGeneratePdb)))
        {
            SaveDialogs.PickPdbFile(ui, appearance, commands, assembly);
        }

        if (assembly.CanPickPdb && ui.MenuItem(t.TreeSelectPdb, id: "select-pdb"))
        {
            PickPdbToRead(ui, appearance, commands, assembly);
        }
    }

    private static void TargetFrameworkMenu(Ui ui, Appearance appearance, IAppCommandWriter commands, LoadedModule assembly)
    {
        var t = appearance.Strings;
        using var menu = ui.SubMenu(t.TreeTargetFramework, id: "target-framework");
        if (!menu.IsOpen)
        {
            return;
        }

        var detectedLabel = DetectedFrameworkLabel(t, assembly);
        if (ui.MenuRadioItem(detectedLabel, !assembly.IsTargetFrameworkPicked, id: "detected"))
        {
            Commands.StartReopening(commands, nameof(AssemblyReloader.SetTargetFrameworkAsync), assembly, assembly,
                static (a, m, ct) => a.AssemblyReloader.SetTargetFrameworkAsync(a.ListSession, a.Reopens, a.Decompiler, m, null, ct));
        }

        ui.MenuSeparator();
        var frameworks = FrameworkIds.Common;
        for (var i = 0; i < frameworks.Count; i++)
        {
            var framework = frameworks[i];
            if (ui.MenuRadioItem(framework.Moniker, IsPicked(assembly, framework), id: framework.Text))
            {
                Commands.StartReopening(commands, nameof(AssemblyReloader.SetTargetFrameworkAsync), assembly, (assembly, framework),
                    static (a, s, ct) => a.AssemblyReloader.SetTargetFrameworkAsync(a.ListSession, a.Reopens, a.Decompiler, s.assembly, s.framework, ct));
            }
        }
    }

    private static string DetectedFrameworkLabel(Strings t, LoadedModule assembly)
    {
        if (assembly.DetectedTargetFramework == null)
        {
            return t.TreeTargetFrameworkNone;
        }

        return t.TreeTargetFrameworkDetected(assembly.DetectedTargetFramework.Moniker);
    }

    private static bool IsPicked(LoadedModule assembly, TargetFrameworkId framework)
    {
        if (!assembly.IsTargetFrameworkPicked)
        {
            return false;
        }

        return assembly.TargetFramework == framework;
    }

    private static void PickPdbToRead(Ui ui, Appearance appearance, IAppCommandWriter commands, LoadedModule assembly)
    {
        var t = appearance.Strings;
        FileFilter[] filters = [new FileFilter(t.FileFilterPdb, ["*.pdb"]), new FileFilter(t.FileFilterAll, ["*.*"])];
        var title = t.TreeSelectPdbTitle(assembly.Name);
        ui.PickFile(title, FindFolderOf(assembly), filters, path => Commands.StartReopening(commands, nameof(AssemblyReloader.UsePdbAsync), assembly, (assembly, path),
            static (a, s, ct) => a.AssemblyReloader.UsePdbAsync(a.ListSession, a.Reopens, a.Decompiler, s.assembly, s.path, ct)));
    }

    private static string? FindFolderOf(LoadedModule assembly)
    {
        // The path of an assembly read out of a package runs through the package.
        if (assembly.Place is not null)
        {
            return null;
        }

        return Path.GetDirectoryName(assembly.FilePath.Text);
    }
}
