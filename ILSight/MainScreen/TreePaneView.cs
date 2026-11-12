using ZGF.Desktop;
using ZGF.Geometry;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.Core.Analysis;
using ILSight.Exporting;
using ILSight.Commanding;
using ILSight.Diagramming;
using ILSight.AssemblyArea;
using static ILSight.AssemblyArea.Assemblies;
using static ILSight.Diagramming.Diagrams;
using static ILSight.ScreenLayout;

namespace ILSight.MainScreen;

internal static class TreePaneView
{
    private const string TreeId = "tree";
    private const string MenuId = "tree-menu";
    private const string ToolbarId = "tree-toolbar";

    /// <summary>How many tools <see cref="Tool"/> draws; the separators count as tools.</summary>
    private const int ToolCount = 10;

    /// <summary>The tree and its toolbar. A node picked or opened shows in <paramref name="navigation"/>'s tabs in this
    /// frame, in <paramref name="setup"/>, in the preview tab while
    /// <see cref="Navigation.PreviewTabs"/>; only its decompile is queued.</summary>
    public static void Render(Ui<UiState> ui, Navigation navigation, AssemblyList assemblies, DecompilerSetup setup)
    {
        var theme = ui.Theme;
        var tree = ui.State.Tree;
        Trees.SetTreeStyle(tree, theme, ui.State.NodeIconPalette, ui.State.Icons);
        Trees.ShowTokens(tree, ui.State.Display.TreeTokens);
        Trees.ShowEmptyTables(tree, ui.State.Display.ShowEmptyMetadataTables);
        Trees.NestNamespaces(tree, ui.State.Display.NestedNamespaces);
        using var pane = ui.Column(new Box { Height = Sizing.Grow(), Gap = 0f });
        Toolbar(ui, navigation, assemblies);
        ui.Separator();
        if (tree.Roots.Length == 0)
        {
            EmptyHint(ui);
        }

        var result = ui.TreeView(TreeId, tree.Roots, tree, tree.View, new Box { Padding = new Spacing(0f, theme.GapTight) });
        AnalyzeOnShortcut(ui);
        CloseOnDelete(ui);
        Respond(ui, navigation, setup, result);
        ContextMenu(ui, navigation, assemblies, setup);
    }

    /// <summary>The panel's toolbar: as many of its tools as fit, in order, and the rest in the menu of a
    /// "more" button after them. What fits is worked out from the width the toolbar had last frame, so
    /// the first frame shows them all.</summary>
    private static void Toolbar(Ui<UiState> ui, Navigation navigation, AssemblyList assemblies)
    {
        var toolbarId = ui.GetId(ToolbarId);
        var shown = CountShownTools(ui, toolbarId);
        using (ui.PaneToolbar(toolbarId, new Box { Width = Sizing.Grow(), Clip = true }))
        {
            for (var i = 0; i < shown; i++)
            {
                Tool(ui, navigation, assemblies, i, shown, inMenu: false);
            }

            if (shown == ToolCount)
            {
                return;
            }

            using var menu = ui.DropdownButton("more", ui.State.Strings.TreeMoreActions, glyph: ui.State.Icons.AssembliesMoreActions);
            if (!menu.IsOpen)
            {
                return;
            }

            for (var i = shown; i < ToolCount; i++)
            {
                Tool(ui, navigation, assemblies, i, shown, inMenu: true);
            }
        }
    }

    private static bool IsToolSeparator(int index)
    {
        return index == 3 || index == 6;
    }

    /// <summary>How many tools, from the first, fit in the toolbar as wide as it was last frame: all of
    /// them if they fit together, else as many as fit beside the "more" button.</summary>
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

    /// <summary>How wide the first <paramref name="count"/> tools are in the toolbar, with the gaps
    /// between them.</summary>
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

    /// <summary>The tool at <paramref name="index"/>, as a button in the toolbar or an item of the "more"
    /// menu, the first <paramref name="shown"/> being in the toolbar. A separator is drawn only between
    /// two tools in the same place.</summary>
    private static void Tool(Ui<UiState> ui, Navigation navigation, AssemblyList assemblies, int index, int shown, bool inMenu)
    {
        var t = ui.State.Strings;
        var icons = ui.State.Icons;
        var tree = ui.State.Tree;
        var any = tree.Roots.Length > 0;
        switch (index)
        {
            case 0:
            {
                var opened = FindOpenedNode(navigation);
                if (DrawTool(ui, inMenu, "select-opened", icons.AssembliesSelectOpened, t.TreeSelectOpenedNode, opened is not null) && opened is not null)
                    {
                        SelectOpenedNode(ui, assemblies, opened);
                    }

                    return;
            }
            case 1:
                if (DrawTool(ui, inMenu, "collapse", icons.AssembliesCollapseAll, t.CodeCollapseAll, any))
                {
                    Trees.CollapseTree(tree);
                }

                return;
            case 2:
                if (DrawTool(ui, inMenu, "expand", icons.AssembliesExpandNamespaces, t.TreeExpandNamespaces, any))
                {
                    Commands.ExpandNamespacesLater(ui.State, tree, Trees.ExpandNamespaces(tree));
                }

                return;
            case 3:
            case 6:
                DrawToolSeparator(ui, inMenu, index, shown);
                return;
            case 4:
                if (DrawTool(ui, inMenu, "sort-list", icons.AssembliesSortList, t.TreeSortList, any))
                {
                    Commands.Later(ui.State, static a => AssemblyListing.SortListed(a.Assemblies));
                }

                return;
            case 5:
            {
                var label = inMenu ? t.TreeReloadAll : ui.Memo("reload-all-tip", t, static s => s.TreeReloadAllTip(Shortcuts.ReloadAll));
                if (DrawTool(ui, inMenu, "reload-all", icons.AssembliesReloadAll, label, any, Shortcuts.ReloadAll))
                    {
                        Commands.Start(ui.State, nameof(AssemblyReloading.RefreshAssembliesAsync), Trees.ListExpandedPaths(tree), static (a, expanded, ct) => a.AssemblyReloading.RefreshAssembliesAsync(a, expanded, ct));
                    }

                    return;
            }
            case 7:
                if (DrawTool(ui, inMenu, "close-assembly", icons.AssembliesCloseAssembly, t.TreeCloseAssemblies, any))
                {
                    AssemblyClosing.OpenCloser(ui.State.Closer, assemblies, tree.View.Selected?.Assembly);
                }

                return;
            case 8:
                if (DrawTool(ui, inMenu, "close-failed", icons.AssembliesCloseFailed, t.TreeCloseFailed, AssemblyListing.AnyFailedToLoad(assemblies)))
                {
                    Commands.Later(ui.State, static a => CloseFailedAssemblies(a.Assemblies));
                }

                return;
            case 9:
                if (DrawTool(ui, inMenu, "clear-list", icons.AssembliesClearList, t.TreeClearList, any))
                {
                    Commands.Later(ui.State, static a => Commands.StartLoads(a, CloseAllAssemblies(a.ListSession)));
                }

                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(index), index, "The assemblies panel has no such tool.");
        }
    }

    /// <summary>An icon button with <paramref name="label"/> as its tooltip, or a menu item showing the
    /// icon, the label and <paramref name="shortcut"/>; true on the frame it is picked.</summary>
    private static bool DrawTool(Ui ui, bool inMenu, string id, IconGlyph glyph, string label, bool enabled, Shortcut? shortcut = null)
    {
        if (inMenu)
        {
            return ui.MenuItem(label, shortcut, enabled, id: id, icon: new UiIcon(glyph));
        }

        return ui.IconButton(id, glyph, label, enabled);
    }

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

    /// <summary>The node the active tab shows, or null when it shows none.</summary>
    private static BrowseNode? FindOpenedNode(Navigation navigation)
    {
        var tab = navigation.ActiveTab;
        if (tab is null)
        {
            return null;
        }

        return tab.Node;
    }

    /// <summary>Expands the tree down to <paramref name="node"/>, when its assembly is listed, selects it,
    /// scrolls to it and focuses the tree.</summary>
    private static void SelectOpenedNode(Ui<UiState> ui, AssemblyList assemblies, BrowseNode node)
    {
        if (!AssemblyListing.IsListed(assemblies, node.Root.Assembly))
        {
            return;
        }

        Trees.RevealNode(ui.State.Tree, node);
        ui.Focus(ui.GetId(TreeId), takesText: false);
    }

    /// <summary>The root selected in the tree, of an assembly, a package or a file that failed to
    /// load, or null when none is or another node is.</summary>
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

    /// <summary>Whether <paramref name="node"/> is the root of an item of the list; an assembly read out
    /// of a package is not one.</summary>
    private static bool IsListedRoot(BrowseNode node)
    {
        return node.Parent is null;
    }

    private static void EmptyHint(Ui<UiState> ui)
    {
        var t = ui.State.Strings;
        var theme = ui.Theme;
        var text = ui.Memo("empty", t, static s => s.TreeEmpty(Shortcuts.Open));
        ui.Text(text, new Box { Width = Sizing.Grow(), Padding = new Spacing(theme.PanelPadding, theme.Gap) }, color: theme.TextMuted, wrap: true);
    }

    private static void AnalyzeOnShortcut(Ui<UiState> ui)
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

        var picked = ui.State.Tree.View.Selected;
        if (picked is null)
        {
            return;
        }

        Commands.Later(ui.State, (screen: ui.State, picked), static (a, s) => AnalyzerPanelView.Analyze(s.screen, a, s.picked));
    }

    private static void CloseOnDelete(Ui<UiState> ui)
    {
        var treeId = ui.GetId(TreeId);
        if (!ui.HasFocus(treeId))
        {
            return;
        }

        var picked = FindPickedAssembly(ui.State.Tree);
        if (picked is null)
        {
            return;
        }

        if (!ui.IsPressed(Shortcuts.Delete))
        {
            return;
        }

        Commands.Later(ui.State, picked.Assembly, static (a, assembly) => AssemblyListing.CloseAssembly(a.Assemblies, assembly));
    }

    private static void Respond(Ui<UiState> ui, Navigation navigation, DecompilerSetup setup,
        TreeViewResult<BrowseNode> result)
    {
        var node = result.Node;
        if (node is null)
        {
            return;
        }

        if (IsPicked(result))
        {
            Preview(ui.State, navigation, node, setup);
        }

        if (result.Activated)
        {
            Open(ui.State, navigation, node, setup);
        }

        if (result.ContextMenuRequested)
        {
            OpenContextMenu(ui, node, result.ContextMenuAt);
        }
    }

    /// <summary>Whether the user picked the node, rather than pressed it for its menu.</summary>
    private static bool IsPicked(TreeViewResult<BrowseNode> result)
    {
        if (!result.SelectionChanged)
        {
            return false;
        }

        return !result.ContextMenuRequested;
    }

    /// <summary>Shows <paramref name="node"/>, just picked, in the preview tab while tabs preview, and asks for the
    /// load it started in the tab to be brought in.</summary>
    private static void Preview(UiState screen, Navigation navigation, BrowseNode node, DecompilerSetup setup)
    {
        if (!navigation.PreviewTabs)
        {
            return;
        }

        Commands.DecompileLater(screen, navigation.PreviewNode(node, setup));
    }

    /// <summary>Asks to go where <paramref name="node"/> points when it stands for a reference, else opens it in a
    /// tab kept open.</summary>
    private static void Open(UiState screen, Navigation navigation, BrowseNode node, DecompilerSetup setup)
    {
        var activation = NodeDefinitions.GetActivation(node.Subject);
        if (activation is Activation.Navigate jump)
        {
            Commands.FollowLater(screen, jump.Target, navigation.BeginResolving(inNewTab: false));
            return;
        }
        Commands.DecompileLater(screen, navigation.OpenNode(node, setup));
    }

    private static void OpenContextMenu(Ui<UiState> ui, BrowseNode node, PointF at)
    {
        ui.State.MenuTarget = node;
        ui.OpenContextMenu(MenuId, at);
    }

    private static void ContextMenu(Ui<UiState> ui, Navigation navigation, AssemblyList assemblies, DecompilerSetup setup)
    {
        using var menu = ui.ContextMenu(MenuId);
        if (!menu.IsOpen)
        {
            return;
        }

        var node = ui.State.MenuTarget;
        if (node is null)
        {
            return;
        }

        NodeMenu(ui, navigation, assemblies, setup, node);
    }

    private static void NodeMenu(Ui<UiState> ui, Navigation navigation, AssemblyList assemblies, DecompilerSetup setup,
        BrowseNode node)
    {
        var t = ui.State.Strings;
        var jump = NodeDefinitions.GetActivation(node.Subject) as Activation.Navigate;
        var openKey = jump != null ? null : Shortcuts.Enter;
        if (ui.MenuItem(t.TreeOpen, openKey, id: "open"))
        {
            Commands.DecompileLater(ui.State, navigation.OpenNode(node, setup));
        }

        if (ui.MenuItem(t.TreeOpenInNewTab, id: "open-in-new-tab"))
        {
            Commands.DecompileLater(ui.State, navigation.ShowNode(node, setup, inNewTab: true));
        }

        if (jump != null && ui.MenuItem(t.TreeGoToTarget, Shortcuts.Enter, id: "go-to-target"))
        {
            Commands.FollowLater(ui.State, jump.Target, navigation.BeginResolving(inNewTab: false));
        }

        if (ui.MenuItem(t.TreeCopyFullName, id: "copy-full-name"))
        {
            ui.ClipboardText = BrowseNodes.GetFullName(node);
        }

        DocsItem(ui, node);
        var analysis = Analyzers.Of(node);
        if (analysis is not null && ui.MenuItem(t.TreeAnalyze, Shortcuts.Analyze, id: "analyze"))
        {
            Commands.Later(ui.State, (screen: ui.State, node), static (a, s) => AnalyzerPanelView.Analyze(s.screen, a, s.node));
        }

        var definition = NodeDefinitions.ReferenceOf(node.Subject);
        if (definition != null && ui.MenuItem(t.TreeShowInMetadata, id: "show-in-metadata"))
        {
            Commands.ShowInMetadataLater(ui.State, definition, navigation.BeginResolving(inNewTab: false));
        }

        if (node.Subject is NodeSubject.TypeDefinition type)
        {
            DiagramItems(ui, type.Definition.Reference);
        }

        if (node.HasResource && ui.MenuItem(t.ResourceExport, id: "export-resource", icon: new UiIcon(ui.State.Icons.ResourceExport)))
        {
            ResourceImageView.PickExport(ui, node);
        }

        ExtractItems(ui, node);
        if (OffersSaveCode(node) && ui.MenuItem(t.FileSaveCode, Shortcuts.SaveCode, id: "save-code", icon: new UiIcon(ui.State.Icons.FileSaveCode)))
        {
            SaveDialogs.PickCodeFile(ui, setup.Language, node);
        }

        if (node.Subject is NodeSubject.Module module)
        {
            AssemblyItems(ui, assemblies, module.Value);
        }

        if (node.Subject is NodeSubject.Module && ui.MenuItem(t.TreeScopeSearchToAssembly, id: "scope-search"))
        {
            ScopeSearch(ui.State, node);
        }

        if (node.Subject is NodeSubject.Namespace && ui.MenuItem(t.TreeScopeSearchToNamespace, id: "scope-search"))
        {
            ScopeSearch(ui.State, node);
        }

        if (IsListedRoot(node) && ui.MenuItem(t.TreeOpenContainingFolder, id: "open-containing-folder", icon: new UiIcon(ui.State.Icons.AssembliesOpenContainingFolder)))
        {
            ui.RevealFile(node.Assembly.FilePath.Text);
        }

        if (IsListedRoot(node))
        {
            ui.MenuSeparator();
            if (ui.MenuItem(t.TreeCloseAssembly, id: "close-assembly"))
            {
                Commands.Later(ui.State, node.Assembly, static (a, assembly) => AssemblyListing.CloseAssembly(a.Assemblies, assembly));
            }
        }
    }

    /// <summary>The items that show the type <paramref name="type"/> points at on the diagram last worked in,
    /// or on a new one.</summary>
    private static void DiagramItems(Ui<UiState> ui, CodeReference.Entity type)
    {
        var t = ui.State.Strings;
        if (ui.MenuItem(t.TreeShowInDiagram, id: "show-in-diagram"))
        {
            DiagramView.ShowInDiagramLater(ui.State, type, t.DiagramTitle(type.Name));
        }

        if (ui.MenuItem(t.TreeShowInNewDiagram, id: "show-in-new-diagram"))
        {
            DiagramView.ShowInNewDiagramLater(ui.State, type, t.DiagramTitle(type.Name));
        }
    }

    /// <summary>The item that opens the .NET API reference page of a namespace, or of a public type or
    /// member, when it has one.</summary>
    private static void DocsItem(Ui<UiState> ui, BrowseNode node)
    {
        var t = ui.State.Strings;
        var page = MicrosoftDocs.FindPage(node);
        if (page is null)
        {
            return;
        }

        if (ui.MenuItem(t.TreeShowInApiReference, id: "show-in-api-reference", icon: new UiIcon(ui.State.Icons.LinkApiReference)))
        {
            ui.OpenUrl(page);
        }
    }

    /// <summary>The items that write files out of a package: the file of an assembly read out of one,
    /// or every file of a package or a folder of one.</summary>
    private static void ExtractItems(Ui<UiState> ui, BrowseNode node)
    {
        var t = ui.State.Strings;
        var file = PackedAssemblyOf(node);
        if (file != null && ui.MenuItem(t.TreeExtractFile, id: "extract-file", icon: new UiIcon(ui.State.Icons.AssembliesExtract)))
        {
            SaveDialogs.PickPackageFile(ui, file);
        }

        var folder = node.FolderInPackage;
        if (folder != null && ui.MenuItem(t.TreeExtractAll, id: "extract-all", icon: new UiIcon(ui.State.Icons.AssembliesExtract)))
        {
            SaveDialogs.PickExtractFolder(ui, node.Text, folder);
        }
    }

    /// <summary>Where the assembly is read from, when <paramref name="node"/> is the root of one read
    /// out of a package; else null.</summary>
    private static PackagePlace? PackedAssemblyOf(BrowseNode node)
    {
        if (node.Subject is not NodeSubject.Module module)
        {
            return null;
        }

        return module.Value.Place;
    }

    /// <summary>Whether the menu offers to save the node's code: it shows code, and is no resource,
    /// whose own Export item saves its bytes.</summary>
    private static bool OffersSaveCode(BrowseNode node)
    {
        if (node.HasResource)
        {
            return false;
        }

        return NodeContentLoader.DependsOnLanguage(node);
    }

    /// <summary>What only an assembly's node offers: exporting it, its PDBs, a diagram of it, comparing it
    /// with another, the framework it resolves against, and opening what it references.</summary>
    private static void AssemblyItems(Ui<UiState> ui, AssemblyList assemblies, LoadedModule assembly)
    {
        var t = ui.State.Strings;
        if (ui.MenuItem(t.FileExport, id: "export", icon: new UiIcon(ui.State.Icons.FileExportProject)))
        {
            Exports.OpenExportSetup(ui.State.ExportSetup, assemblies.Assemblies, assembly);
        }

        PdbItems(ui, assembly);
        if (ui.MenuItem(t.TreeCreateDiagram, id: "create-diagram"))
        {
            SaveDialogs.PickDiagramFolder(ui, assembly);
        }

        if (ui.MenuItem(t.TreeCompareWith, id: "compare-with"))
        {
            ui.State.ComparisonPicker.Open(assembly);
        }

        TargetFrameworkMenu(ui, assembly);
        if (ui.MenuItem(t.TreeLoadDependencies, id: "load-dependencies"))
        {
            Commands.Start(ui.State, nameof(AssemblyOpening.OpenDependenciesAsync), assembly, static (a, m, ct) => a.AssemblyOpening.OpenDependenciesAsync(a.Opens, m, ct));
        }
    }

    /// <summary>Generating a portable PDB for the assembly, and picking the PDB it reads.</summary>
    private static void PdbItems(Ui<UiState> ui, LoadedModule assembly)
    {
        var t = ui.State.Strings;
        if (ui.MenuItem(t.TreeGeneratePdb, enabled: LoadedAssemblies.CanGeneratePdb(assembly), id: "generate-pdb", icon: new UiIcon(ui.State.Icons.FileGeneratePdb)))
        {
            SaveDialogs.PickPdbFile(ui, assembly);
        }

        if (assembly.CanPickPdb && ui.MenuItem(t.TreeSelectPdb, id: "select-pdb"))
        {
            PickPdbToRead(ui, assembly);
        }
    }

    /// <summary>The submenu of the framework the assembly's references are resolved against: the one it
    /// was built for, or one of the common ones, the one in use picked.</summary>
    private static void TargetFrameworkMenu(Ui<UiState> ui, LoadedModule assembly)
    {
        var t = ui.State.Strings;
        using var menu = ui.SubMenu(t.TreeTargetFramework, id: "target-framework");
        if (!menu.IsOpen)
        {
            return;
        }

        var detectedLabel = DetectedFrameworkLabel(t, assembly);
        if (ui.MenuRadioItem(detectedLabel, !assembly.IsTargetFrameworkPicked, id: "detected"))
        {
            Commands.Start(ui.State, nameof(AssemblyReloading.SetTargetFrameworkAsync), assembly, static (a, m, ct) => a.AssemblyReloading.SetTargetFrameworkAsync(a, m, null, ct));
        }

        ui.MenuSeparator();
        var frameworks = FrameworkIds.Common;
        for (var i = 0; i < frameworks.Count; i++)
        {
            var framework = frameworks[i];
            if (ui.MenuRadioItem(framework.Moniker, IsPicked(assembly, framework), id: framework.Text))
            {
                Commands.Start(ui.State, nameof(AssemblyReloading.SetTargetFrameworkAsync), (assembly, framework), static (a, s, ct) => a.AssemblyReloading.SetTargetFrameworkAsync(a, s.assembly, s.framework, ct));
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

    /// <summary>Asks for the PDB <paramref name="assembly"/> reads, starting in its folder, then has it read that one.</summary>
    private static void PickPdbToRead(Ui<UiState> ui, LoadedModule assembly)
    {
        var t = ui.State.Strings;
        var screen = ui.State;
        FileFilter[] filters = [new FileFilter(t.FileFilterPdb, ["*.pdb"]), new FileFilter(t.FileFilterAll, ["*.*"])];
        var title = t.TreeSelectPdbTitle(assembly.Name);
        ui.PickFile(title, FindFolderOf(assembly), filters, path => Commands.Start(screen, nameof(AssemblyReloading.UsePdbAsync), (assembly, path), static (a, s, ct) => a.AssemblyReloading.UsePdbAsync(a, s.assembly, s.path, ct)));
    }

    /// <summary>The folder the assembly's file is in; null for one read out of a package, whose path runs through it.</summary>
    private static string? FindFolderOf(LoadedModule assembly)
    {
        if (assembly.Place is not null)
        {
            return null;
        }

        return Path.GetDirectoryName(assembly.FilePath.Text);
    }
}
