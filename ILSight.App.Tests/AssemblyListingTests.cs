using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ZGF.KeyboardModule;
using ILSight.CommandArea;
using ILSight.AssemblyListArea;
using static ILSight.AssemblyListArea.AssemblyLists;
using static ILSight.Docking;
using static ILSight.SettingArea.Settings;
using static ILSight.App.Tests.BuiltInGlyphs;
using static ILSight.App.Tests.TestPaths;

namespace ILSight.App.Tests;

public sealed class AssemblyListingTests
{
    private const string Other = "Other";

    [Fact]
    public void The_open_assemblies_are_saved_as_the_open_lists()
    {
        using var temp = new TempDir();
        var d = AppStateDriver.Opened(temp.Settings());
        d.AppState.SettingsSaving.Save(d.AppState);

        var loaded = temp.Settings();

        Assert.Equal(SavedAssemblyLists.DefaultName, loaded.ActiveAssemblyList);
        Assert.Equal([AppStateDriver.FixturePath], Assert.Single(loaded.AssemblyLists).Assemblies.Select(static p => p.Text));
    }

    [Fact]
    public void The_list_restored_at_startup_is_saved_whole_while_its_assemblies_are_still_opening()
    {
        using var temp = new TempDir();
        var other = typeof(AppState).Assembly.Location;
        var settings = temp.Settings();
        settings.AssemblyLists = [new AssemblyListRecord(SavedAssemblyLists.DefaultName, [PathOf(AppStateDriver.FixturePath), PathOf(other)])];
        var d = new AppStateDriver(settings);

        d.Start(AppStateDriver.OpenAtStartupAsync);
        d.AppState.SettingsSaving.Save(d.AppState);

        Assert.Equal([AppStateDriver.FixturePath, other], Assert.Single(temp.Settings().AssemblyLists).Assemblies.Select(static p => p.Text));
        d.Settle();
        Assert.Equal([AppStateDriver.FixturePath, other], d.AppState.Assemblies.ListFilePaths().Select(static p => p.Text));
    }

    [Fact]
    public void A_file_of_the_restored_list_that_is_gone_stays_listed_and_saved_as_failed()
    {
        using var temp = new TempDir();
        var gone = temp.PathOf("gone.dll");
        var settings = temp.Settings();
        settings.AssemblyLists = [new AssemblyListRecord(SavedAssemblyLists.DefaultName, [PathOf(gone), PathOf(AppStateDriver.FixturePath)])];
        var d = new AppStateDriver(settings);

        d.Start(AppStateDriver.OpenAtStartupAsync);
        d.Settle();
        d.AppState.SettingsSaving.Save(d.AppState);

        var roots = d.Ui.Tree.Roots;
        Assert.Equal([NodeIcon.FailedAssembly, NodeIcon.Assembly], roots.Select(r => r.Icon));
        Assert.Empty(d.AppState.Errors.Entries);
        Assert.Equal([gone, AppStateDriver.FixturePath], Assert.Single(temp.Settings().AssemblyLists).Assemblies.Select(static p => p.Text));
    }

    [Fact]
    public void The_frame_a_list_is_picked_in_from_the_toolbar_says_the_app_is_reloading_and_empties_the_tree()
    {
        var app = AppDriver.Opened();
        app.AppState.Lists.Add(Other);
        var fixtures = app.Fixture("Greeter").Root.Text;
        app.Run(2);

        app.Frames.ClickOn(Selector.WithText(SavedAssemblyLists.DefaultName));
        app.Frames.ClickOn(Selector.OfRole(SemanticRole.MenuItem, Other));

        Assert.True(app.Frames.Drew(app.Ui.Strings.StatusReloading));
        Assert.False(app.Frames.Drew(fixtures));
        app.Settle(2);
        Assert.Equal(Other, app.AppState.Lists.Active);
        Assert.Empty(app.AppState.Assemblies.Listed);
    }

    [Fact]
    public void Switching_lists_keeps_the_analyses_of_the_last_run_still_being_found_with_their_list()
    {
        using var temp = new TempDir();
        var first = AppStateDriver.Opened(temp.Settings());
        first.Analyze(first.Fixture("Greeter"));
        first.AppState.SettingsSaving.Save(first.AppState);
        var d = new AppStateDriver(temp.Settings());
        d.AppState.Lists.Add(Other);

        d.SwitchList(Other);
        d.Settle();
        d.AppState.SettingsSaving.Save(d.AppState);

        var lists = temp.Settings().AssemblyLists;
        Assert.Empty(d.AppState.Analyzer.Roots);
        var firstAnalyzed = lists[0].Analyzed;
        Assert.NotNull(firstAnalyzed);
        Assert.Single(firstAnalyzed);
        var secondAnalyzed = lists[1].Analyzed;
        Assert.NotNull(secondAnalyzed);
        Assert.Empty(secondAnalyzed);
    }

    [Fact]
    public void Switching_back_to_a_list_analyzes_again_what_was_analyzed_in_it()
    {
        var d = AppStateDriver.Opened();
        d.Analyze(d.Fixture("Greeter"));
        d.AppState.Lists.Add(Other);

        d.SwitchList(Other);
        d.Settle();
        var inOther = d.AppState.Analyzer.Roots.Count;
        d.SwitchList(SavedAssemblyLists.DefaultName);
        d.Settle();

        Assert.Equal(0, inOther);
        Assert.Contains("Greeter", Assert.Single(d.AppState.Analyzer.Found()).Symbol.Label);
    }

    [Fact]
    public void A_duplicated_list_has_the_analyses_of_the_one_it_copies()
    {
        var d = AppStateDriver.Opened();
        d.Analyze(d.Fixture("Greeter"));
        var copy = DuplicateAssemblyList(d.AppState, SavedAssemblyLists.DefaultName, "Copy");

        d.SwitchList(copy);
        d.Settle();

        Assert.NotEqual(SavedAssemblyLists.DefaultName, copy);
        Assert.Equal(copy, d.AppState.Lists.Active);
        Assert.Contains("Greeter", Assert.Single(d.AppState.Analyzer.Found()).Symbol.Label);
    }

    [Fact]
    public void Switching_lists_closes_the_assemblies_documents_and_history_of_the_one_before_and_switching_back_reopens_them()
    {
        var d = AppStateDriver.Opened();
        d.Show(d.Fixture("Greeter"));
        d.Show(d.Fixture("Farewell"), inNewTab: true);
        d.AppState.Lists.Add(Other);

        d.SwitchList(Other);
        d.Settle();

        Assert.Empty(d.AppState.Assemblies.Assemblies);
        Assert.Null(Assert.Single(d.AppState.Navigation.OpenTabs).Location);
        Assert.Empty(d.AppState.Navigation.HistoryEntries);

        d.SwitchList(SavedAssemblyLists.DefaultName);
        d.Settle();

        Assert.Equal([AppStateDriver.FixturePath], d.AppState.Assemblies.ListFilePaths().Select(static p => p.Text));
    }

    [Fact]
    public void Switching_lists_with_every_document_closed_opens_an_empty_one()
    {
        var d = AppStateDriver.Opened();
        d.AppState.Lists.Add(Other);
        foreach (var tab in d.AppState.Navigation.OpenTabs.ToList())
        {
            d.AppState.Navigation.CloseTab(tab);
        }

        d.SwitchList(Other);
        d.Settle();

        var empty = Assert.Single(d.AppState.Navigation.OpenTabs);
        Assert.Same(empty, d.AppState.Navigation.ActiveTab);
        Assert.True(d.Ui.Layout.Contains(DocumentPanels.GetPanel(d.Ui.Documents, empty)));
        Assert.Null(empty.Location);
    }

    [Fact]
    public void A_new_list_opens_empty_and_what_is_opened_in_it_stays_out_of_the_list_before()
    {
        var d = AppStateDriver.Opened();
        var other = typeof(AppState).Assembly.Location;

        CreateAssemblyList(d.AppState, "New list");
        d.SwitchList("New list");
        d.Settle();
        d.Start((a, ct) => a.AssemblyOpening.OpenAssembliesAsync(a, [AssemblyPath.Of(other)], ct));
        d.RunUntil(() => d.AppState.Assemblies.ListFilePaths().Contains(PathOf(other)));
        d.SwitchList(SavedAssemblyLists.DefaultName);
        d.Settle();

        Assert.Equal([AppStateDriver.FixturePath], d.AppState.Assemblies.ListFilePaths().Select(static p => p.Text));
        Assert.Equal([other], d.AppState.Lists.GetPaths("New list").Select(static p => p.Text));
    }

    [Fact]
    public void An_assembly_still_opening_when_the_list_is_switched_stays_out_of_the_next_list()
    {
        var d = AppStateDriver.Opened();
        d.AppState.Lists.Add(Other);

        d.Start((a, ct) => a.AssemblyOpening.OpenAssembliesAsync(a, [AssemblyPath.Of(typeof(AppState).Assembly.Location)], ct));
        d.SwitchList(Other);
        d.Settle();
        d.Pump();

        Assert.Empty(d.AppState.Assemblies.Assemblies);
        Assert.Empty(d.AppState.Errors.Entries);
    }

    [Fact]
    public void Switching_lists_stops_the_assemblies_still_opening_from_landing_in_the_next_list()
    {
        var d = AppStateDriver.Opened();
        d.AppState.Lists.Add(Other);
        var opening = d.AppState.Opens.Token;

        d.SwitchList(Other);

        Assert.True(opening.IsCancellationRequested);
        Assert.False(d.AppState.Opens.Token.IsCancellationRequested);
    }

    [Fact]
    public void Deleting_the_open_list_opens_the_assemblies_of_the_one_that_takes_its_place()
    {
        var d = AppStateDriver.Opened();
        CreateAssemblyList(d.AppState, "New list");
        d.Settle();

        d.DeleteList("New list");
        d.Settle();

        Assert.Equal(SavedAssemblyLists.DefaultName, d.AppState.Lists.Active);
        Assert.Equal([AppStateDriver.FixturePath], d.AppState.Assemblies.ListFilePaths().Select(static p => p.Text));
    }

    [Fact]
    public void Reloading_reopens_the_whole_list_even_while_it_is_still_being_opened()
    {
        var d = AppStateDriver.Opened();
        d.AppState.Lists.Add(Other);
        d.SwitchList(Other);
        d.Settle();

        d.SwitchList(SavedAssemblyLists.DefaultName);
        d.Start((a, ct) => a.AssemblyReloading.RefreshAssembliesAsync(a.Lists, a.ListSession, a.ViewCommands, a.Decompiler, Trees.ListExpandedPaths(d.Ui.Tree), ct));
        d.Settle();

        Assert.Equal([AppStateDriver.FixturePath], d.AppState.Assemblies.ListFilePaths().Select(static p => p.Text));
    }

    [Fact]
    public void Expanding_the_tree_expands_the_assemblies_and_their_namespaces()
    {
        var d = AppStateDriver.Opened();
        var tree = d.Ui.Tree;
        var root = tree.Roots[0];
        Trees.CollapseTree(tree);

        Commands.StartExpandingNamespaces(d.AppState, tree, Trees.ExpandNamespaces(tree));
        var ns = d.Node(AppStateDriver.FixturesNamespace);
        d.RunUntil(() => tree.View.IsExpanded(ns));

        Assert.True(tree.View.IsExpanded(root));
        Assert.False(tree.View.IsExpanded(d.Fixture("Greeter")));
    }

    [Fact]
    public void Collapsing_the_tree_above_the_shown_node_keeps_the_tab_on_it()
    {
        var app = AppDriver.Opened();
        var greeter = app.Fixture("Greeter");
        app.Show(greeter, 2);

        app.Frames.ClickOn(Selector.AtPath("dock/assemblies/collapse")).Frame(2);

        Assert.Same(greeter, app.Ui.Tree.View.Selected);
        Assert.Same(greeter, app.AppState.Navigation.ActiveTab?.Node);
    }

    [Fact]
    public void Opening_a_node_in_a_new_tab_from_its_menu_leaves_the_shown_document_alone()
    {
        var app = AppDriver.Opened();
        var greeter = app.Fixture("Greeter");
        var farewell = app.Fixture("Farewell");
        app.Show(greeter, 2);
        var first = app.AppState.Navigation.ActiveTab;
        Assert.NotNull(first);

        app.Frames.ClickOn(Selector.OfRole(SemanticRole.TreeItem, "Farewell"), PointerButton.Right).Frame();
        var shownAfterRightClick = first.Node;
        app.Frames.ClickOn(Selector.OfRole(SemanticRole.MenuItem, "Open in new tab")).Frame(2);
        app.Settle(2);

        Assert.Same(greeter, shownAfterRightClick);
        Assert.Same(greeter, first.Node);
        Assert.Same(farewell, app.AppState.Navigation.ActiveTab?.Node);
    }

    [Fact]
    public void Without_preview_tabs_clicking_a_node_selects_it_and_double_clicking_opens_it_in_the_active_tab()
    {
        var app = AppDriver.Opened();
        app.Driver.TurnOffPreviewTabs();
        var greeter = app.Fixture("Greeter");
        var farewell = app.Fixture("Farewell");
        app.Show(greeter, 2);
        var tab = app.AppState.Navigation.ActiveTab;
        Assert.NotNull(tab);

        app.Frames.ClickOn(Selector.WithText("Farewell")).Frame(2);
        var shownAfterClick = tab.Node;
        var selectedAfterClick = app.Ui.Tree.View.Selected;
        app.DoubleClickOn(Selector.WithText("Farewell")).Frame(2);
        app.Settle(2);

        Assert.Same(greeter, shownAfterClick);
        Assert.Same(farewell, selectedAfterClick);
        Assert.Same(tab, app.AppState.Navigation.ActiveTab);
        Assert.Same(farewell, tab.Node);
    }

    [Fact]
    public void The_tree_menu_opens_a_node_in_the_active_tab()
    {
        var app = AppDriver.Opened();
        var farewell = app.Fixture("Farewell");
        app.Show(app.Fixture("Greeter"), 2);
        var tab = app.AppState.Navigation.ActiveTab;
        Assert.NotNull(tab);

        app.Frames.ClickOn(Selector.OfRole(SemanticRole.TreeItem, "Farewell"), PointerButton.Right).Frame();
        app.Frames.ClickOn(Selector.OfRole(SemanticRole.MenuItem, "Open")).Frame(2);
        app.Settle(2);

        Assert.Same(tab, app.AppState.Navigation.ActiveTab);
        Assert.Same(farewell, tab.Node);
    }

    [Fact]
    public void Switching_tabs_leaves_the_tree_selection_alone()
    {
        var app = AppDriver.Opened();
        var greeter = app.Fixture("Greeter");
        var farewell = app.Fixture("Farewell");
        app.Show(greeter, 2);
        var first = app.AppState.Navigation.ActiveTab;
        Assert.NotNull(first);
        app.Show(farewell, 2, inNewTab: true);

        DocumentPanels.ActivateTab(app.Ui, app.AppState.Navigation, app.AppState.Decompiler.Setup, first);
        app.Run(2);

        Assert.Same(first, app.AppState.Navigation.ActiveTab);
        Assert.Same(farewell, app.Ui.Tree.View.Selected);
    }

    [Fact]
    public void The_select_opened_node_button_reveals_what_the_active_tab_shows()
    {
        var app = AppDriver.Opened();
        var greeter = app.Fixture("Greeter");
        app.Show(greeter, 2);
        Trees.CollapseTree(app.Ui.Tree);
        app.Ui.Tree.View.Select(app.Ui.Tree.Roots[0]);
        app.Run(2);

        app.Frames.ClickOn(Selector.AtPath("dock/assemblies/select-opened")).Frame(2);

        Assert.Same(greeter, app.Ui.Tree.View.Selected);
        Assert.True(app.Ui.Tree.View.IsExpanded(app.Node(AppStateDriver.FixturesNamespace)));
        Assert.Same(greeter, app.AppState.Navigation.ActiveTab?.Node);
    }

    [Fact]
    public void The_assemblies_panels_remove_button_offers_the_assembly_of_the_selected_node_to_close()
    {
        var app = AppDriver.Opened();
        app.Ui.Tree.View.Select(app.Node(AppStateDriver.FixturesNamespace));
        app.Run(2);

        app.Frames.ClickOn(Selector.AtPath("dock/assemblies/close-assembly")).Frame(2);
        Assert.True(app.Ui.Closer.IsOpen);
        Assert.True(Assert.Single(app.Ui.Closer.Order).Included);
        Assert.Single(app.AppState.Assemblies.Assemblies);

        app.Frames.ClickOn(Selector.AtPath("close-ticked")).Frame(2);

        Assert.Empty(app.AppState.Assemblies.Assemblies);
    }

    [Fact]
    public void The_assemblies_panels_clear_button_closes_every_assembly()
    {
        var app = AppDriver.Opened();
        app.Run(2);

        app.Frames.ClickOn(Selector.AtPath("dock/assemblies/clear-list")).Frame(2);

        Assert.Empty(app.AppState.Assemblies.Assemblies);
    }

    [Fact]
    public void A_wide_assemblies_panel_shows_every_tool_and_no_more_button()
    {
        var app = AppDriver.Opened(width: 1600f);
        app.Run(2);

        Assert.IsType<TargetResult.Hittable>(app.Frames.Target(Selector.AtPath("dock/assemblies/clear-list")));
        Assert.IsType<TargetResult.NotFound>(app.Frames.Target(Selector.AtPath("dock/assemblies/more")));
    }

    [Fact]
    public void A_narrow_assemblies_panel_puts_the_tools_that_dont_fit_in_its_more_menu()
    {
        var app = AppDriver.Opened(width: 360f);
        app.Run(2);

        Assert.IsType<TargetResult.Hittable>(app.Frames.Target(Selector.AtPath("dock/assemblies/select-opened")));
        Assert.IsType<TargetResult.NotFound>(app.Frames.Target(Selector.AtPath("dock/assemblies/clear-list")));

        app.Frames.ClickOn(Selector.AtPath("dock/assemblies/more")).Frame(2);
        app.Frames.ClickOn(Selector.OfRole(SemanticRole.MenuItem, "Clear list")).Frame(2);

        Assert.Empty(app.AppState.Assemblies.Assemblies);
    }

    [Fact]
    public void The_assemblies_panels_close_failed_button_is_disabled_while_none_failed()
    {
        var app = AppDriver.Opened();
        app.Run(2);

        app.Frames.ClickOn(Selector.AtPath("dock/assemblies/close-failed")).Frame(2);

        Assert.Single(app.AppState.Assemblies.Assemblies);
    }

    [Fact]
    public void Dropping_files_on_the_window_opens_them_and_leaves_folders_out()
    {
        var app = AppDriver.Opened();
        app.Run(2);
        var other = typeof(AppState).Assembly.Location;
        var folder = Path.GetDirectoryName(other) ?? throw new InvalidOperationException($"The app assembly's path names no folder: '{other}'.");

        app.Frames.DropFiles(folder, other).Frame();
        app.Frames.WaitUntil(() => app.AppState.Assemblies.ListFilePaths().Contains(PathOf(other)), Patience);

        Assert.Equal([AppStateDriver.FixturePath, other], app.AppState.Assemblies.ListFilePaths().Select(static p => p.Text));
    }

    [Fact]
    public void The_frame_a_file_is_picked_to_open_in_drops_what_was_still_being_looked_up()
    {
        var app = AppDriver.Opened();
        var other = typeof(AppState).Assembly.Location;
        var resolving = app.AppState.Navigation.BeginResolving(inNewTab: false);
        app.Run(2);
        var saidResolving = app.Frames.Drew(app.Ui.Strings.StatusResolving);

        app.Frames.FilePicker.Answer = other;
        app.Frames.Key(Shortcuts.Open).Frame();
        app.Frames.Frame();

        Assert.True(saidResolving);
        Assert.False(app.Frames.Drew(app.Ui.Strings.StatusResolving));
        Assert.True(app.AppState.Navigation.IsStale(resolving.Intent));
        app.Frames.WaitUntil(() => app.AppState.Assemblies.ListFilePaths().Contains(PathOf(other)), Patience);
    }

    [Fact]
    public void The_frame_a_file_is_picked_to_open_in_says_it_is_opening_until_it_is_shown()
    {
        var app = AppDriver.Opened();
        var other = typeof(AppState).Assembly.Location;
        app.Run(2);
        var saidOpeningBefore = app.Frames.Drew(app.Ui.Strings.StatusOpening);

        app.Frames.FilePicker.Answer = other;
        app.Frames.Key(Shortcuts.Open).Frame();
        app.Frames.Frame();
        var saidOpening = app.Frames.Drew(app.Ui.Strings.StatusOpening);
        app.Frames.WaitUntil(() => app.AppState.Assemblies.ListFilePaths().Contains(PathOf(other)), Patience);
        app.Settle(2);

        Assert.False(saidOpeningBefore);
        Assert.True(saidOpening);
        Assert.False(app.AppState.Navigation.IsOpening);
        Assert.False(app.Frames.Drew(app.Ui.Strings.StatusOpening));
    }

    [Fact]
    public void Files_dropped_while_a_dialog_is_open_stay_closed()
    {
        var app = AppDriver.Opened();
        app.Run(2);
        app.OpenExportSetup(null);
        app.Run(2);

        app.Frames.DropFiles(typeof(AppState).Assembly.Location).Frame();
        app.Settle(2);

        Assert.Equal([AppStateDriver.FixturePath], app.AppState.Assemblies.ListFilePaths().Select(static p => p.Text));
    }

    [Fact]
    public void The_assemblies_panel_names_the_open_list_and_the_dialog_counts_each_lists_assemblies()
    {
        var app = AppDriver.Opened();
        app.AppState.Lists.Add(Other);
        app.Run(2);
        Assert.True(app.Frames.Drew(SavedAssemblyLists.DefaultName));

        AssemblyListsDialogView.Open(app.Ui.ListsDialog, app.AppState);
        app.Run(2);

        Assert.True(app.Frames.Drew("Assembly lists"));
        Assert.True(app.Frames.Drew("1 assembly"));
        Assert.True(app.Frames.Drew("0 assemblies"));
        Assert.True(app.Frames.Drew(Text(Icons.ListsActive)));
    }

    [Fact]
    public void The_toolbars_manage_lists_button_opens_the_lists_dialog()
    {
        var app = AppDriver.Opened();
        app.Run(2);

        app.Frames.ClickOn(Selector.AtPath("manage-lists")).Frame(2);

        Assert.True(app.Ui.ListsDialog.IsOpen);
    }

    [Fact]
    public void A_rows_activate_button_switches_to_its_list_and_leaves_the_dialog_open()
    {
        var app = OpenedDialog();

        app.Frames.ClickOn(Selector.AtPath("Other/activate")).Frame(2);
        app.Settle(2);

        Assert.Equal(Other, app.AppState.Lists.Active);
        Assert.True(app.Ui.ListsDialog.IsOpen);
        Assert.Empty(app.AppState.Assemblies.Assemblies);
    }

    [Fact]
    public void A_new_list_is_named_in_place_without_switching_to_it()
    {
        var app = OpenedDialog();

        app.Frames.ClickOn(Selector.WithText("New list")).Frame(2);
        app.Frames.Type("Work").Frame(2);
        app.Frames.Key(KeyboardKey.Enter).Frame(2);

        Assert.Contains("Work", app.AppState.Lists.Names);
        Assert.Equal(SavedAssemblyLists.DefaultName, app.AppState.Lists.Active);
        Assert.IsType<ListsDialogMode.Browsing>(app.Ui.ListsDialog.Mode);
    }

    [Fact]
    public void The_frame_the_preset_menu_opens_in_says_the_frameworks_are_looked_for_until_they_come()
    {
        var app = OpenedDialog();

        app.Frames.ClickOn(Selector.WithText("Add preconfigured"));
        app.Frames.WaitUntil(() => app.Frames.Drew(app.Ui.Strings.ListsLookingForPresets), Patience);
        var lookedForBeforeShown = app.Ui.Presets.Found is null;
        app.Frames.WaitUntil(() => app.Ui.Presets.Found is not null, Patience);
        app.Frames.Frame(2);

        Assert.True(lookedForBeforeShown);
        Assert.False(app.Frames.Drew(app.Ui.Strings.ListsLookingForPresets));
        Assert.True(app.Frames.Drew(AssemblyListPresets.FindInstalled()[0].Name));
    }

    [Fact]
    public void A_preconfigured_list_holds_an_installed_frameworks_assemblies_and_is_named_in_place_without_switching_to_it()
    {
        var app = OpenedDialog();
        var preset = AssemblyListPresets.FindInstalled()[0];

        app.Frames.ClickOn(Selector.WithText("Add preconfigured"));
        app.Frames.WaitUntil(() => app.Ui.Presets.Found is not null, Patience);
        app.Frames.Frame(2);
        app.Frames.ClickOn(Selector.OfRole(SemanticRole.MenuItem, preset.Name)).Frame(2);

        Assert.Equal(preset.Name, Assert.IsType<ListsDialogMode.Renaming>(app.Ui.ListsDialog.Mode).List);
        Assert.Equal(preset.Paths, app.AppState.Lists.GetPaths(preset.Name));
        Assert.Equal(SavedAssemblyLists.DefaultName, app.AppState.Lists.Active);
    }

    [Fact]
    public void Double_clicking_a_list_renames_it_and_escape_drops_the_name_but_keeps_the_dialog_open()
    {
        var app = OpenedDialog();

        app.DoubleClickOn(Selector.WithText(Other)).Frame(2);
        Assert.Equal(Other, Assert.IsType<ListsDialogMode.Renaming>(app.Ui.ListsDialog.Mode).List);
        app.Frames.Type("Gone").Frame(2);
        app.Frames.Key(KeyboardKey.Escape).Frame(2);

        Assert.Equal([SavedAssemblyLists.DefaultName, Other], app.AppState.Lists.Names);
        Assert.IsType<ListsDialogMode.Browsing>(app.Ui.ListsDialog.Mode);
        Assert.True(app.Ui.ListsDialog.IsOpen);
        Assert.Equal(SavedAssemblyLists.DefaultName, app.AppState.Lists.Active);
    }

    [Fact]
    public void Deleting_the_active_list_says_which_opens_instead_once_confirmed()
    {
        var app = OpenedDialog();

        app.Frames.ClickOn(Selector.AtPath("delete")).Frame(2);
        Assert.True(app.Frames.Drew("Delete assembly list"));
        Assert.Equal(SavedAssemblyLists.DefaultName, Assert.IsType<ListsDialogMode.ConfirmingDelete>(app.Ui.ListsDialog.Mode).List);
        Assert.Equal(Other, app.AppState.Lists.GetReplacement(SavedAssemblyLists.DefaultName));
        app.Frames.ClickOn(Selector.WithText("Delete list")).Frame(2);
        app.Settle(2);

        Assert.Equal([Other], app.AppState.Lists.Names);
        Assert.Equal(Other, app.AppState.Lists.Active);
    }

    [Fact]
    public void Escape_closes_the_delete_confirmation_and_leaves_the_lists_dialog_open()
    {
        var app = OpenedDialog();

        app.Frames.ClickOn(Selector.AtPath("delete")).Frame(2);
        app.Frames.Key(KeyboardKey.Escape).Frame(2);
        app.Frames.FrameTime = UiWidgets.DialogOutSeconds;
        app.Frames.Frame(2);

        Assert.False(app.Frames.Drew("Delete assembly list"));
        Assert.IsType<ListsDialogMode.Browsing>(app.Ui.ListsDialog.Mode);
        Assert.True(app.Ui.ListsDialog.IsOpen);
        Assert.Equal([SavedAssemblyLists.DefaultName, Other], app.AppState.Lists.Names);
    }

    /// <summary>An app with the fixtures open in the Default list, beside an empty one, and the dialog
    /// open on Default.</summary>
    private static AppDriver OpenedDialog()
    {
        var app = AppDriver.Opened();
        app.AppState.Lists.Add(Other);
        AssemblyListsDialogView.Open(app.Ui.ListsDialog, app.AppState);
        app.Run(2);
        return app;
    }
}
