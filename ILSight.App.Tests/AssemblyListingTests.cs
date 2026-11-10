using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.Localization;
using ZGF.KeyboardModule;
using static ILSight.Assemblies;
using static ILSight.AssemblyLists;
using static ILSight.Docking;
using static ILSight.Settings;
using static ILSight.App.Tests.BuiltInGlyphs;
using static ILSight.App.Tests.TestPaths;

namespace ILSight.App.Tests;

public sealed class SavedAssemblyListsTests
{
    [Fact]
    public void A_new_list_is_numbered_when_its_name_is_taken()
    {
        var lists = AssemblyLists.CreateLists();

        var first = AssemblyLists.AddList(lists, "New list");
        var second = AssemblyLists.AddList(lists, "new list");

        Assert.Equal("New list", first);
        Assert.Equal("new list 2", second);
        Assert.Equal([SavedAssemblyLists.DefaultName, "New list", "new list 2"], lists.Names);
    }

    [Fact]
    public void A_list_is_never_made_without_a_name()
    {
        var lists = AssemblyLists.CreateLists();

        Assert.Throws<ArgumentException>(() => AssemblyLists.AddList(lists, " "));
        Assert.Throws<ArgumentException>(() => AssemblyLists.DuplicateList(lists, SavedAssemblyLists.DefaultName, ""));
        Assert.Equal([SavedAssemblyLists.DefaultName], lists.Names);
    }

    [Fact]
    public void A_duplicate_holds_the_same_assemblies_under_a_name_of_its_own()
    {
        var lists = AssemblyLists.CreateLists();
        AssemblyLists.RememberActive(lists, [FakePath("a.dll"), FakePath("b.dll")]);

        var copy = AssemblyLists.DuplicateList(lists, SavedAssemblyLists.DefaultName, "Default copy");
        AssemblyLists.RememberActive(lists, [FakePath("c.dll")]);

        Assert.Equal("Default copy", copy);
        Assert.Equal([FakePath("a.dll"), FakePath("b.dll")], AssemblyLists.GetPaths(lists, copy));
    }

    [Fact]
    public void A_list_made_from_assemblies_holds_them_under_a_name_of_its_own()
    {
        var lists = AssemblyLists.CreateLists();
        AssemblyLists.AddList(lists, "Framework");

        var name = AssemblyLists.AddList(lists, "Framework", [FakePath("a.dll"), FakePath("b.dll")]);

        Assert.Equal("Framework 2", name);
        Assert.Equal([FakePath("a.dll"), FakePath("b.dll")], AssemblyLists.GetPaths(lists, name));
        Assert.Equal(SavedAssemblyLists.DefaultName, lists.Active);
    }

    [Fact]
    public void A_list_is_not_renamed_to_nothing_or_to_another_lists_name()
    {
        var lists = AssemblyLists.CreateLists();
        AssemblyLists.AddList(lists, "Work");

        Assert.False(AssemblyLists.RenameList(lists, "Work", "  "));
        Assert.False(AssemblyLists.RenameList(lists, "Work", "default"));
        Assert.True(AssemblyLists.RenameList(lists, "Work", "WORK"));
        Assert.True(AssemblyLists.RenameList(lists, SavedAssemblyLists.DefaultName, " Play "));
        Assert.Equal(["Play", "WORK"], lists.Names);
        Assert.Equal("Play", lists.Active);
    }

    [Fact]
    public void Deleting_the_open_list_opens_the_one_that_takes_its_place_and_the_last_leaves_an_empty_default()
    {
        var lists = AssemblyLists.CreateLists();
        AssemblyLists.AddList(lists, "Work");
        AssemblyLists.AddList(lists, "Play");
        AssemblyLists.ActivateList(lists, "Play");

        AssemblyLists.DeleteList(lists, "Play");
        Assert.Equal("Work", lists.Active);

        AssemblyLists.DeleteList(lists, "Work");
        AssemblyLists.RememberActive(lists, [FakePath("a.dll")]);
        AssemblyLists.DeleteList(lists, SavedAssemblyLists.DefaultName);

        Assert.Equal([SavedAssemblyLists.DefaultName], lists.Names);
        Assert.Empty(lists.ActivePaths);
    }

    [Fact]
    public void Loading_drops_nameless_and_repeated_lists_and_opens_the_named_one_or_else_the_first()
    {
        var lists = AssemblyLists.CreateLists();
        List<AssemblyListRecord> records =
        [
            new(" ", [FakePath("x.dll")]),
            new("Work", [FakePath("a.dll")]),
            new("work", [FakePath("b.dll")]),
            new("Play", [FakePath("c.dll")]),
        ];

        AssemblyLists.LoadLists(lists, records, "play");
        Assert.Equal(["Work", "Play"], lists.Names);
        Assert.Equal("Play", lists.Active);

        AssemblyLists.LoadLists(lists, records, "Gone");
        Assert.Equal("Work", lists.Active);
        Assert.Equal([FakePath("a.dll")], lists.ActivePaths);
    }
}

public sealed class AssemblyListingTests
{
    private const string Other = "Other";

    [Fact]
    public void The_open_assemblies_are_saved_as_the_open_lists()
    {
        using var temp = new TempDir();
        var d = AppStateDriver.Opened(temp.Settings());
        SaveSettings(d.AppState);

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

        RestoreActiveAssemblyList(d.AppState);
        SaveSettings(d.AppState);

        Assert.Equal([AppStateDriver.FixturePath, other], Assert.Single(temp.Settings().AssemblyLists).Assemblies.Select(static p => p.Text));
        d.Settle();
        Assert.Equal([AppStateDriver.FixturePath, other], AssemblyListing.ListFilePaths(d.AppState.Assemblies).Select(static p => p.Text));
    }

    [Fact]
    public void A_file_of_the_restored_list_that_is_gone_stays_listed_and_saved_as_failed()
    {
        using var temp = new TempDir();
        var gone = temp.PathOf("gone.dll");
        var settings = temp.Settings();
        settings.AssemblyLists = [new AssemblyListRecord(SavedAssemblyLists.DefaultName, [PathOf(gone), PathOf(AppStateDriver.FixturePath)])];
        var d = new AppStateDriver(settings);

        RestoreActiveAssemblyList(d.AppState);
        d.Settle();
        SaveSettings(d.AppState);

        var roots = d.Ui.Tree.Roots;
        Assert.Equal([NodeIcon.FailedAssembly, NodeIcon.Assembly], roots.Select(r => r.Icon));
        Assert.Empty(d.AppState.Errors.Entries);
        Assert.Equal([gone, AppStateDriver.FixturePath], Assert.Single(temp.Settings().AssemblyLists).Assemblies.Select(static p => p.Text));
    }

    [Fact]
    public void Switching_lists_keeps_the_analyses_of_the_last_run_still_being_found_with_their_list()
    {
        using var temp = new TempDir();
        var first = AppStateDriver.Opened(temp.Settings());
        first.Analyze(first.Fixture("Greeter"));
        SaveSettings(first.AppState);
        var d = new AppStateDriver(temp.Settings());
        AssemblyLists.AddList(d.AppState.Lists, Other);

        SwitchAssemblyList(d.AppState, Other);
        d.Settle();
        SaveSettings(d.AppState);

        var lists = temp.Settings().AssemblyLists;
        Assert.Empty(d.AppState.Analyzer.Roots);
        Assert.Single(lists[0].Analyzed!);
        Assert.Empty(lists[1].Analyzed!);
    }

    [Fact]
    public void Switching_back_to_a_list_analyzes_again_what_was_analyzed_in_it()
    {
        var d = AppStateDriver.Opened();
        d.Analyze(d.Fixture("Greeter"));
        AssemblyLists.AddList(d.AppState.Lists, Other);

        SwitchAssemblyList(d.AppState, Other);
        d.Settle();
        var inOther = d.AppState.Analyzer.Roots.Count;
        SwitchAssemblyList(d.AppState, SavedAssemblyLists.DefaultName);
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

        SwitchAssemblyList(d.AppState, copy);
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
        AssemblyLists.AddList(d.AppState.Lists, Other);

        SwitchAssemblyList(d.AppState, Other);
        d.Settle();

        Assert.Empty(d.AppState.Assemblies.Assemblies);
        Assert.Null(Assert.Single(d.AppState.Tabs.Open).Location);
        Assert.Empty(d.AppState.History.Entries);

        SwitchAssemblyList(d.AppState, SavedAssemblyLists.DefaultName);
        d.Settle();

        Assert.Equal([AppStateDriver.FixturePath], AssemblyListing.ListFilePaths(d.AppState.Assemblies).Select(static p => p.Text));
    }

    [Fact]
    public void Switching_lists_with_every_document_closed_opens_an_empty_one()
    {
        var d = AppStateDriver.Opened();
        AssemblyLists.AddList(d.AppState.Lists, Other);
        foreach (var tab in d.AppState.Tabs.Open.ToList())
            CloseTab(d.AppState, tab);

        SwitchAssemblyList(d.AppState, Other);
        d.Settle();

        var empty = Assert.Single(d.AppState.Tabs.Open);
        Assert.Same(empty, d.AppState.Tabs.Active);
        Assert.True(d.Ui.Layout.Contains(DocumentPanels.GetPanel(d.Ui.Documents, empty)));
        Assert.Null(empty.Location);
    }

    [Fact]
    public void A_new_list_opens_empty_and_what_is_opened_in_it_stays_out_of_the_list_before()
    {
        var d = AppStateDriver.Opened();
        var other = typeof(AppState).Assembly.Location;

        CreateAssemblyList(d.AppState, "New list");
        SwitchAssemblyList(d.AppState, "New list");
        d.Settle();
        OpenAssembly(d.AppState, other);
        d.RunUntil(() => AssemblyListing.ListFilePaths(d.AppState.Assemblies).Contains(PathOf(other)));
        SwitchAssemblyList(d.AppState, SavedAssemblyLists.DefaultName);
        d.Settle();

        Assert.Equal([AppStateDriver.FixturePath], AssemblyListing.ListFilePaths(d.AppState.Assemblies).Select(static p => p.Text));
        Assert.Equal([other], AssemblyLists.GetPaths(d.AppState.Lists, "New list").Select(static p => p.Text));
    }

    [Fact]
    public void An_assembly_still_opening_when_the_list_is_switched_stays_out_of_the_next_list()
    {
        var d = AppStateDriver.Opened();
        AssemblyLists.AddList(d.AppState.Lists, Other);

        OpenAssembly(d.AppState, typeof(AppState).Assembly.Location);
        SwitchAssemblyList(d.AppState, Other);
        d.Settle();
        d.Pump();

        Assert.Empty(d.AppState.Assemblies.Assemblies);
        Assert.Empty(d.AppState.Errors.Entries);
    }

    [Fact]
    public void Switching_lists_stops_the_assemblies_still_opening_from_landing_in_the_next_list()
    {
        var d = AppStateDriver.Opened();
        AssemblyLists.AddList(d.AppState.Lists, Other);
        var opening = d.AppState.Opens.Token;

        SwitchAssemblyList(d.AppState, Other);

        Assert.True(opening.IsCancellationRequested);
        Assert.False(d.AppState.Opens.Token.IsCancellationRequested);
    }

    [Fact]
    public void Deleting_the_open_list_opens_the_assemblies_of_the_one_that_takes_its_place()
    {
        var d = AppStateDriver.Opened();
        CreateAssemblyList(d.AppState, "New list");
        d.Settle();

        DeleteAssemblyList(d.AppState, "New list");
        d.Settle();

        Assert.Equal(SavedAssemblyLists.DefaultName, d.AppState.Lists.Active);
        Assert.Equal([AppStateDriver.FixturePath], AssemblyListing.ListFilePaths(d.AppState.Assemblies).Select(static p => p.Text));
    }

    [Fact]
    public void Reloading_reopens_the_whole_list_even_while_it_is_still_being_opened()
    {
        var d = AppStateDriver.Opened();
        AssemblyLists.AddList(d.AppState.Lists, Other);
        SwitchAssemblyList(d.AppState, Other);
        d.Settle();

        SwitchAssemblyList(d.AppState, SavedAssemblyLists.DefaultName);
        RefreshAssemblies(d.AppState, Trees.ListExpandedPaths(d.Ui.Tree));
        d.Settle();

        Assert.Equal([AppStateDriver.FixturePath], AssemblyListing.ListFilePaths(d.AppState.Assemblies).Select(static p => p.Text));
    }

    [Fact]
    public void Expanding_the_tree_expands_the_assemblies_and_their_namespaces()
    {
        var d = AppStateDriver.Opened();
        var tree = d.Ui.Tree;
        var root = tree.Roots[0];
        Trees.CollapseTree(tree);

        Trees.ExpandNamespaces(tree);
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
        Assert.Same(greeter, app.AppState.Tabs.Active!.Node);
    }

    [Fact]
    public void Opening_a_node_in_a_new_tab_from_its_menu_leaves_the_shown_document_alone()
    {
        var app = AppDriver.Opened();
        var greeter = app.Fixture("Greeter");
        var farewell = app.Fixture("Farewell");
        app.Show(greeter, 2);
        var first = app.AppState.Tabs.Active!;

        app.Frames.ClickOn(Selector.OfRole(SemanticRole.TreeItem, "Farewell"), PointerButton.Right).Frame();
        var shownAfterRightClick = first.Node;
        app.Frames.ClickOn(Selector.OfRole(SemanticRole.MenuItem, "Open in new tab")).Frame(2);
        app.Settle(2);

        Assert.Same(greeter, shownAfterRightClick);
        Assert.Same(greeter, first.Node);
        Assert.Same(farewell, app.AppState.Tabs.Active!.Node);
    }

    [Fact]
    public void Without_preview_tabs_clicking_a_node_selects_it_and_double_clicking_opens_it_in_the_active_tab()
    {
        var app = AppDriver.Opened();
        app.Driver.TurnOffPreviewTabs();
        var greeter = app.Fixture("Greeter");
        var farewell = app.Fixture("Farewell");
        app.Show(greeter, 2);
        var tab = app.AppState.Tabs.Active!;

        app.Frames.ClickOn(Selector.WithText("Farewell")).Frame(2);
        var shownAfterClick = tab.Node;
        var selectedAfterClick = app.Ui.Tree.View.Selected;
        app.DoubleClickOn(Selector.WithText("Farewell")).Frame(2);
        app.Settle(2);

        Assert.Same(greeter, shownAfterClick);
        Assert.Same(farewell, selectedAfterClick);
        Assert.Same(tab, app.AppState.Tabs.Active);
        Assert.Same(farewell, tab.Node);
    }

    [Fact]
    public void The_tree_menu_opens_a_node_in_the_active_tab()
    {
        var app = AppDriver.Opened();
        var farewell = app.Fixture("Farewell");
        app.Show(app.Fixture("Greeter"), 2);
        var tab = app.AppState.Tabs.Active!;

        app.Frames.ClickOn(Selector.OfRole(SemanticRole.TreeItem, "Farewell"), PointerButton.Right).Frame();
        app.Frames.ClickOn(Selector.OfRole(SemanticRole.MenuItem, "Open")).Frame(2);
        app.Settle(2);

        Assert.Same(tab, app.AppState.Tabs.Active);
        Assert.Same(farewell, tab.Node);
    }

    [Fact]
    public void Switching_tabs_leaves_the_tree_selection_alone()
    {
        var app = AppDriver.Opened();
        var greeter = app.Fixture("Greeter");
        var farewell = app.Fixture("Farewell");
        app.Show(greeter, 2);
        var first = app.AppState.Tabs.Active!;
        app.Show(farewell, 2, inNewTab: true);

        DocumentPanels.ActivateTab(app.Ui, first);
        app.Run(2);

        Assert.Same(first, app.AppState.Tabs.Active);
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
        Assert.Same(greeter, app.AppState.Tabs.Active!.Node);
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
        var folder = Path.GetDirectoryName(other)!;

        app.Frames.DropFiles(folder, other).Frame();
        app.Frames.WaitUntil(() => AssemblyListing.ListFilePaths(app.AppState.Assemblies).Contains(PathOf(other)), Patience);

        Assert.Equal([AppStateDriver.FixturePath, other], AssemblyListing.ListFilePaths(app.AppState.Assemblies).Select(static p => p.Text));
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

        Assert.Equal([AppStateDriver.FixturePath], AssemblyListing.ListFilePaths(app.AppState.Assemblies).Select(static p => p.Text));
    }

    [Fact]
    public void The_assemblies_panel_names_the_open_list_and_the_dialog_counts_each_lists_assemblies()
    {
        var app = AppDriver.Opened();
        AssemblyLists.AddList(app.AppState.Lists, Other);
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
    public void A_preconfigured_list_holds_an_installed_frameworks_assemblies_and_is_named_in_place_without_switching_to_it()
    {
        var app = OpenedDialog();
        var preset = AssemblyListPresets.ListInstalled(new InstalledRuntime())[0];

        app.Frames.ClickOn(Selector.WithText("Add preconfigured")).Frame(2);
        app.Frames.ClickOn(Selector.OfRole(SemanticRole.MenuItem, preset.Name)).Frame(2);

        Assert.Equal(preset.Name, Assert.IsType<ListsDialogMode.Renaming>(app.Ui.ListsDialog.Mode).List);
        Assert.Equal(preset.Paths, AssemblyLists.GetPaths(app.AppState.Lists, preset.Name));
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
        Assert.Equal(Other, AssemblyLists.FindReplacement(app.AppState.Lists, SavedAssemblyLists.DefaultName));
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
        AssemblyLists.AddList(app.AppState.Lists, Other);
        AssemblyListsDialogView.Open(app.Ui.ListsDialog, app.AppState);
        app.Run(2);
        return app;
    }
}
