using ZGF.Desktop;
using ZGF.Gui.Immediate;
using ILSight.Core;
using ZGF.KeyboardModule;
using ILSight.Exporting;
using static ILSight.Preferences;
using static ILSight.ScreenLayout;
using static ILSight.App.Tests.BuiltInGlyphs;

namespace ILSight.App.Tests;

public sealed class ExportDialogTests : IDisposable
{
    private static readonly string SecondPath = typeof(KeyboardKey).Assembly.Location;

    private readonly TempDir _temp = new();

    public ExportDialogTests()
    {
        Directory.CreateDirectory(_temp.Path);
    }

    public void Dispose()
    {
        _temp.Dispose();
    }

    private static AppStateDriver OpenedTwo()
    {
        var d = AppStateDriver.Opened();
        d.Start((a, ct) => a.AssemblyOpening.OpenAssemblyAsync(a, SecondPath, ct));
        d.RunUntil(() => d.AppState.Assemblies.Assemblies.Count == 2);
        d.Settle();
        return d;
    }

    private static LoadedModule Fixtures(AppStateDriver d)
    {
        return Assert.IsType<LoadedModule>(d.Node().Assembly);
    }

    [Fact]
    public void The_dialog_opens_with_the_picked_assembly_ticked()
    {
        var d = OpenedTwo();

        d.OpenExportSetup(Fixtures(d));

        var setup = d.Ui.ExportSetup;
        Assert.True(setup.IsOpen);
        Assert.Equal([true, false], setup.Choices.Select(c => c.Included));
        Assert.False(setup.IsSolution);
    }

    [Fact]
    public void The_only_open_assembly_is_ticked_without_being_picked()
    {
        var d = AppStateDriver.Opened();

        d.OpenExportSetup(null);

        Assert.True(Assert.Single(d.Ui.ExportSetup.Choices).Included);
    }

    [Fact]
    public void Ticking_a_second_assembly_makes_the_export_a_solution_named_after_the_folder()
    {
        var d = OpenedTwo();
        d.OpenExportSetup(Fixtures(d));
        var setup = d.Ui.ExportSetup;
        setup.Folder = _temp.PathOf("Out");

        setup.Choices[1].Included = true;

        Assert.True(setup.IsSolution);
        Assert.Equal("Out.sln", setup.SolutionFileName);
        setup.SolutionName = "Mine";
        Assert.Equal("Mine.sln", setup.SolutionFileName);
    }

    [Fact]
    public void Two_projects_with_one_name_stop_the_export_until_one_is_renamed()
    {
        var d = OpenedTwo();
        d.OpenExportSetup(null);
        var setup = d.Ui.ExportSetup;
        setup.Folder = _temp.Path;
        Exports.IncludeAll(setup, true);

        setup.Choices[1].Name = setup.Choices[0].Name.ToUpperInvariant();

        Assert.NotNull(setup.Clash);
        Assert.False(Exports.CanExport(setup, ExportDialogView.ReadFiles(setup)));
        setup.Choices[1].Name = "Other";
        Assert.Null(setup.Clash);
        Assert.True(Exports.CanExport(setup, ExportDialogView.ReadFiles(setup)));
    }

    [Fact]
    public void The_export_needs_a_full_folder_path_and_a_ticked_assembly()
    {
        var d = AppStateDriver.Opened();
        d.OpenExportSetup(null);
        var setup = d.Ui.ExportSetup;

        setup.Folder = "relative";
        Assert.False(Exports.CanExport(setup, ExportDialogView.ReadFiles(setup)));
        setup.Folder = _temp.Path;
        Assert.True(Exports.CanExport(setup, ExportDialogView.ReadFiles(setup)));
        Exports.IncludeAll(setup, false);
        Assert.False(Exports.CanExport(setup, ExportDialogView.ReadFiles(setup)));
    }

    [Fact]
    public void What_stops_the_export_is_named_one_thing_at_a_time()
    {
        var d = AppStateDriver.Opened();
        d.OpenExportSetup(null);
        var setup = d.Ui.ExportSetup;
        var choice = Assert.Single(setup.Choices);

        Exports.IncludeAll(setup, false);
        Assert.IsType<ExportBlocker.NoAssembly>(Exports.FindBlocker(setup, NothingAmiss));
        Exports.IncludeAll(setup, true);
        Assert.IsType<ExportBlocker.NoFolder>(Exports.FindBlocker(setup, NothingAmiss));
        setup.Folder = FullPath;
        choice.Name = " ";
        Assert.IsType<ExportBlocker.NamelessProject>(Exports.FindBlocker(setup, NothingAmiss));
        choice.Name = "Project";
        Assert.Null(Exports.FindBlocker(setup, NothingAmiss));
    }

    /// <summary>A folder path no test writes to: the decision never looks at the disk.</summary>
    private static readonly string FullPath = Path.Combine(Path.GetTempPath(), "ILSight-never-written");

    private static readonly ExportFileFacts NothingAmiss = new() { FolderHoldsFiles = false, IsKeyFileMissing = false };

    private static ExportSetup ReadyToExport(AppStateDriver d)
    {
        d.OpenExportSetup(null);
        var setup = d.Ui.ExportSetup;
        setup.Folder = FullPath;
        return setup;
    }

    [Fact]
    public void FindBlocker_KeyFileMissing_StopsTheExportWithoutTheDisk()
    {
        var setup = ReadyToExport(AppStateDriver.Opened());
        setup.StrongNameKeyFile = Path.Combine(FullPath, "signing.snk");

        var blocker = Exports.FindBlocker(setup, NothingAmiss with { IsKeyFileMissing = true });

        Assert.IsType<ExportBlocker.KeyFileMissing>(blocker);
        Assert.Null(Exports.FindBlocker(setup, NothingAmiss));
    }

    [Fact]
    public void FindBlocker_FolderHoldsFiles_DoesNotStopTheExport()
    {
        var setup = ReadyToExport(AppStateDriver.Opened());

        var canExport = Exports.CanExport(setup, NothingAmiss with { FolderHoldsFiles = true });

        Assert.True(canExport);
    }

    [Fact]
    public void FindBlocker_ClashAndMissingKeyFile_NamesTheClashFirst()
    {
        var d = OpenedTwo();
        var setup = ReadyToExport(d);
        Exports.IncludeAll(setup, true);
        setup.Choices[1].Name = setup.Choices[0].Name;

        var blocker = Exports.FindBlocker(setup, NothingAmiss with { IsKeyFileMissing = true });

        Assert.Equal(setup.Choices[0].Name, Assert.IsType<ExportBlocker.Clash>(blocker).Name);
    }

    [Fact]
    public void A_folder_that_holds_files_is_pointed_out()
    {
        var d = AppStateDriver.Opened();
        d.OpenExportSetup(null);
        var setup = d.Ui.ExportSetup;
        setup.Folder = _temp.Path;
        Assert.False(ExportDialogView.ReadFiles(setup).FolderHoldsFiles);

        setup.Folder = _temp.PathOf("Full");
        _temp.Write(Path.Combine("Full", "notes.txt"), "keep");

        Assert.True(ExportDialogView.ReadFiles(setup).FolderHoldsFiles);
    }

    [Fact]
    public void One_ticked_assembly_exports_as_a_project_straight_into_the_folder()
    {
        var d = AppStateDriver.Opened();
        d.OpenExportSetup(null);
        var setup = d.Ui.ExportSetup;
        setup.Folder = _temp.Path;
        setup.Choices[0].Name = "Renamed";

        ExportDialogView.Start(d.Ui.ExportSetup, d.AppState);
        d.Settle();

        Assert.False(setup.IsOpen);
        Assert.True(File.Exists(_temp.PathOf("Renamed.csproj")));
    }

    [Fact]
    public void Several_ticked_assemblies_export_as_a_solution_with_a_folder_per_project()
    {
        var d = OpenedTwo();
        d.OpenExportSetup(null);
        var setup = d.Ui.ExportSetup;
        setup.Folder = _temp.Path;
        Exports.IncludeAll(setup, true);
        setup.SolutionName = "Both";

        ExportDialogView.Start(d.Ui.ExportSetup, d.AppState);
        d.Settle();

        Assert.True(File.Exists(_temp.PathOf("Both.sln")));
        foreach (var choice in setup.Choices)
        {
            Assert.True(File.Exists(_temp.PathOf(Path.Combine(choice.Name, choice.Name + ".csproj"))));
        }
    }

    [Fact]
    public void With_nothing_ticked_the_dialog_opens_on_the_Choose_page()
    {
        var d = OpenedTwo();

        d.OpenExportSetup(null);
        Assert.Equal(ExportPage.Choose, d.Ui.ExportSetup.Page);

        d.OpenExportSetup(Fixtures(d));
        Assert.Equal(ExportPage.Main, d.Ui.ExportSetup.Page);
    }

    [Fact]
    public void The_Choose_page_lists_ticked_assemblies_first_and_keeps_its_order_while_ticking()
    {
        var d = OpenedTwo();
        var second = d.AppState.Assemblies.Assemblies[1];
        d.OpenExportSetup(second);
        var setup = d.Ui.ExportSetup;

        Exports.ChooseAssemblies(setup);
        var first = ExportDialogView.ListShown(setup.Picker);
        first[1].Included = true;

        Assert.Equal(second, first[0].Assembly);
        Assert.Equal(first, ExportDialogView.ListShown(setup.Picker));
    }

    [Fact]
    public void The_filter_narrows_the_list_and_Select_all_shown_ticks_only_what_it_shows()
    {
        var d = OpenedTwo();
        d.OpenExportSetup(null);
        var picker = d.Ui.ExportSetup.Picker;
        var second = d.AppState.Assemblies.Assemblies[1];

        picker.Filter = second.Name.ToUpperInvariant();
        var shown = Assert.Single(ExportDialogView.ListShown(picker));
        ExportDialogView.IncludeShown(picker, true);

        Assert.Equal(second, shown.Assembly);
        Assert.Equal([false, true], d.Ui.ExportSetup.Choices.Select(c => c.Included));
        Assert.Equal(true, ExportDialogView.CalculateAllShownIncluded(picker));
    }

    private static AppStateDriver OpenedThree()
    {
        var d = OpenedTwo();
        d.Start((a, ct) => a.AssemblyOpening.OpenAssemblyAsync(a, typeof(FactAttribute).Assembly.Location, ct));
        d.RunUntil(() => d.AppState.Assemblies.Assemblies.Count == 3);
        d.Settle();
        return d;
    }

    /// <summary>Flips <paramref name="choice"/>'s tick as its checkbox does, then follows the click.</summary>
    private static void Click(AssemblyPicker picker, List<ExportChoice> shown, ExportChoice choice, bool shift)
    {
        choice.Included = !choice.Included;
        ExportDialogView.TickClicked(picker, shown, choice, shift);
    }

    [Fact]
    public void Shift_click_ticks_every_row_from_the_last_one_clicked()
    {
        var d = OpenedThree();
        d.OpenExportSetup(null);
        var picker = d.Ui.ExportSetup.Picker;
        var shown = ExportDialogView.ListShown(picker);

        Click(picker, shown, shown[0], shift: false);
        Click(picker, shown, shown[2], shift: true);

        Assert.Equal([true, true, true], shown.Select(c => c.Included));
    }

    [Fact]
    public void Shift_click_upwards_unticks_the_range_when_the_clicked_row_is_unticked()
    {
        var d = OpenedThree();
        d.OpenExportSetup(null);
        var picker = d.Ui.ExportSetup.Picker;
        var shown = ExportDialogView.ListShown(picker);
        ExportDialogView.IncludeShown(picker, true);

        Click(picker, shown, shown[2], shift: false);
        Click(picker, shown, shown[1], shift: true);

        Assert.Equal([true, false, false], shown.Select(c => c.Included));
    }

    [Fact]
    public void Shift_click_with_no_row_clicked_before_ticks_only_that_row()
    {
        var d = OpenedThree();
        d.OpenExportSetup(null);
        var picker = d.Ui.ExportSetup.Picker;
        var shown = ExportDialogView.ListShown(picker);

        Click(picker, shown, shown[2], shift: true);

        Assert.Equal([false, false, true], shown.Select(c => c.Included));
    }

    [Fact]
    public void Shift_click_counts_a_row_that_Selected_only_hides_once_unticked()
    {
        var d = OpenedThree();
        d.OpenExportSetup(null);
        var picker = d.Ui.ExportSetup.Picker;
        ExportDialogView.IncludeShown(picker, true);
        picker.SelectedOnly = true;
        var shown = ExportDialogView.ListShown(picker);

        Click(picker, shown, shown[0], shift: false);
        Click(picker, shown, shown[1], shift: false);
        Click(picker, shown, shown[2], shift: true);

        Assert.Empty(ExportDialogView.ListShown(picker));
    }

    [Fact]
    public void Selected_only_hides_the_unticked_assemblies()
    {
        var d = OpenedTwo();
        d.OpenExportSetup(Fixtures(d));
        var setup = d.Ui.ExportSetup;
        Exports.ChooseAssemblies(setup);

        setup.Picker.SelectedOnly = true;

        Assert.Equal(Fixtures(d), Assert.Single(ExportDialogView.ListShown(setup.Picker)).Assembly);
    }

    [Fact]
    public void The_main_page_shows_only_the_ticked_assemblies()
    {
        var app = AppDriver.Opened(height: 1400f);
        app.Start((a, ct) => a.AssemblyOpening.OpenAssemblyAsync(a, SecondPath, ct));
        app.Driver.RunUntil(() => app.AppState.Assemblies.Assemblies.Count == 2);
        var second = app.AppState.Assemblies.Assemblies[1];
        app.Run(2);
        var drawnByTheTree = app.Frames.Canvas.Texts.Count(t => t.Inputs.Text == second.Name);

        app.OpenExportSetup(app.AppState.Assemblies.Assemblies[0]);
        app.Run(2);

        Assert.Equal(drawnByTheTree, app.Frames.Canvas.Texts.Count(t => t.Inputs.Text == second.Name));
        Assert.False(app.Frames.Drew("Select all shown"));
    }

    [Fact]
    public void Choose_opens_the_list_and_Done_or_Escape_goes_back_without_closing()
    {
        var app = AppDriver.Opened(height: 1400f);
        app.OpenExportSetup(null);
        app.Run(2);
        var setup = app.Ui.ExportSetup;

        app.Frames.ClickOn(Selector.AtPath("add")).Frame(2);
        Assert.Equal(ExportPage.Choose, setup.Page);
        app.Frames.ClickOn(Selector.WithText("Done")).Frame(2);
        Assert.Equal(ExportPage.Main, setup.Page);

        app.Frames.ClickOn(Selector.AtPath("add")).Frame(2);
        app.Frames.Key(KeyboardKey.Escape).Frame(2);

        Assert.Equal(ExportPage.Main, setup.Page);
        Assert.True(setup.IsOpen);
    }

    [Fact]
    public void A_filter_that_hides_everything_says_so_and_can_be_cleared()
    {
        var app = AppDriver.Opened(height: 1400f);
        app.OpenExportSetup(null);
        var setup = app.Ui.ExportSetup;
        Exports.ChooseAssemblies(setup);
        setup.Picker.Filter = "nothing-is-called-this";
        app.Run(2);

        Assert.True(app.Frames.Drew("No assemblies match the filter."));
        app.Frames.ClickOn(Selector.WithText("Clear filter")).Frame(2);

        Assert.Equal("", setup.Picker.Filter);
        Assert.Single(ExportDialogView.ListShown(setup.Picker));
    }

    [Fact]
    public void With_nothing_chosen_the_main_page_offers_to_choose()
    {
        var app = AppDriver.Opened(height: 1400f);
        app.OpenExportSetup(null);
        Exports.IncludeAll(app.Ui.ExportSetup, false);
        app.Run(2);

        app.Frames.ClickOn(Selector.AtPath("add")).Frame(2);

        Assert.Equal(ExportPage.Choose, app.Ui.ExportSetup.Page);
    }

    private static AppDriver WithTwoIncluded()
    {
        var app = AppDriver.Opened(height: 1400f);
        app.Start((a, ct) => a.AssemblyOpening.OpenAssemblyAsync(a, SecondPath, ct));
        app.Driver.RunUntil(() => app.AppState.Assemblies.Assemblies.Count == 2);
        app.OpenExportSetup(null);
        Exports.IncludeAll(app.Ui.ExportSetup, true);
        Exports.FinishChoosing(app.Ui.ExportSetup);
        app.Run(2);
        return app;
    }

    /// <summary>Clicks the assembly cell of <paramref name="assembly"/>'s row: the first of the texts
    /// on the row of the last one drawn, which is its project name.</summary>
    private static void ClickRowOf(AppDriver app, LoadedAssembly assembly, KeyModifiers modifiers = KeyModifiers.None)
    {
        var projectName = app.LastTextRect(assembly.Name);
        var cell = app.Frames.TextRects()
            .Where(t => t.Text == assembly.Name && t.Rect.Y == projectName.Y)
            .MinBy(t => t.Rect.X)
            .Rect;
        app.Frames.Click(cell.X + 2f, cell.Y + cell.Height / 2, modifiers: modifiers).Frame();
    }

    [Fact]
    public void Minus_removes_the_picked_out_rows_and_picks_out_the_next()
    {
        var app = WithTwoIncluded();
        var setup = app.Ui.ExportSetup;
        var first = app.AppState.Assemblies.Assemblies[0];
        var second = app.AppState.Assemblies.Assemblies[1];
        Assert.False(setup.RowSelection.Count > 0);

        ClickRowOf(app, first);
        app.Frames.ClickOn(Selector.AtPath("remove-selected")).Frame(2);

        Assert.Equal([false, true], setup.Choices.Select(c => c.Included));
        Assert.True(setup.RowSelection.IsOnly(setup.Choices[1]));
        Assert.Equal(second, Exports.ListIncludedChoices(setup)[0].Assembly);
    }

    [Fact]
    public void Delete_removes_every_row_picked_out_with_ctrl()
    {
        var app = WithTwoIncluded();
        var setup = app.Ui.ExportSetup;

        ClickRowOf(app, app.AppState.Assemblies.Assemblies[0]);
        ClickRowOf(app, app.AppState.Assemblies.Assemblies[1], KeyModifiers.Control);
        Assert.Equal(2, setup.RowSelection.Count);
        app.Frames.Key(KeyboardKey.Delete).Frame(2);

        Assert.Equal(0, setup.IncludedCount);
        Assert.True(app.Frames.Drew("No assemblies included yet."));
    }

    [Fact]
    public void Ctrl_Shift_E_opens_the_dialog_and_Export_starts_it()
    {
        var app = AppDriver.Opened();
        app.Run(2);

        app.Frames.Key(Shortcuts.Export).Frame(2);
        Assert.True(app.Ui.ExportSetup.IsOpen);
        app.Frames.FilePicker.Answer = _temp.Path;
        app.Frames.ClickOn(Selector.AtPath("browse")).Frame(2);
        Assert.Equal(_temp.Path, app.Ui.ExportSetup.Folder);

        app.Frames.ClickOn(Selector.AtPath("export-start")).Frame(2);
        app.Settle(1);

        Assert.False(app.Ui.ExportSetup.IsOpen);
        Assert.Single(app.AppState.FinishedExports);
    }

    [Fact]
    public void Export_on_an_assembly_in_the_tree_opens_the_dialog_with_that_assembly_ticked()
    {
        var app = AppDriver.Opened();
        app.Start((a, ct) => a.AssemblyOpening.OpenAssemblyAsync(a, SecondPath, ct));
        app.Driver.RunUntil(() => app.AppState.Assemblies.Assemblies.Count == 2);
        app.Settle(2);
        var second = app.AppState.Assemblies.Assemblies[1];

        app.Frames.ClickOn(Selector.OfRole(SemanticRole.TreeItem, Trees.GetLabel(app.Ui.Tree, second.Root)), PointerButton.Right).Frame();
        app.Frames.ClickOn(Selector.OfRole(SemanticRole.MenuItem, "Export…")).Frame(2);

        var setup = app.Ui.ExportSetup;
        Assert.True(setup.IsOpen);
        Assert.Equal([false, true], setup.Choices.Select(c => c.Included));
    }

    [Fact]
    public void The_export_options_live_in_the_dialog_and_not_in_the_decompiler_options()
    {
        var app = AppDriver.Opened(height: 1400f);
        OpenPanel(app.Ui, DockView.DecompilerOptions);
        app.Run(2);
        Assert.False(app.Frames.Drew("SDK-style project files"));

        app.OpenExportSetup(null);
        app.Run(2);

        Assert.True(app.Frames.Drew("SDK-style project files"));
        Assert.True(app.Frames.Drew("These also change the code you browse."));
    }

    [Fact]
    public void A_project_option_ticked_in_the_dialog_is_kept_for_the_next_export()
    {
        var app = AppDriver.Opened(height: 1400f);
        var nested = DecompilerToggles.FindToggle("UseNestedDirectoriesForNamespaces")!;
        app.OpenExportSetup(null);
        app.Run(2);

        app.Frames.ClickOn(Selector.WithText("A folder per namespace part")).Frame(2);

        Assert.Equal(!nested.DefaultValue, DecompilerToggles.IsOn(app.AppState.DecompilerOptions, nested));
    }

    [Fact]
    public void A_key_file_that_is_not_there_stops_the_export()
    {
        var d = AppStateDriver.Opened();
        d.OpenExportSetup(null);
        var setup = d.Ui.ExportSetup;
        setup.Folder = _temp.Path;

        setup.StrongNameKeyFile = _temp.PathOf("missing.snk");
        Assert.True(ExportDialogView.ReadFiles(setup).IsKeyFileMissing);
        Assert.False(Exports.CanExport(setup, ExportDialogView.ReadFiles(setup)));

        setup.StrongNameKeyFile = _temp.Write("signing.snk", [1, 2, 3]);
        Assert.False(ExportDialogView.ReadFiles(setup).IsKeyFileMissing);
        Assert.True(Exports.CanExport(setup, ExportDialogView.ReadFiles(setup)));
    }

    [Fact]
    public void A_pdb_asked_for_in_the_dialog_is_written_beside_the_project_and_reported()
    {
        var d = AppStateDriver.Opened();
        d.OpenExportSetup(null);
        var setup = d.Ui.ExportSetup;
        setup.Folder = _temp.Path;
        setup.GeneratePdb = true;

        ExportDialogView.Start(d.Ui.ExportSetup, d.AppState);
        d.Settle();

        var pdb = _temp.PathOf(LoadedAssemblies.GetPdbFileName(Fixtures(d)));
        Assert.True(File.Exists(pdb));
        Assert.Contains(pdb, Assert.IsType<ExportEnd.Finished>(Assert.Single(d.AppState.FinishedExports).End).Written);
    }

    [Fact]
    public void Resetting_the_decompiler_options_keeps_how_projects_are_exported()
    {
        var d = AppStateDriver.Opened();
        var sdk = DecompilerToggles.FindToggle("UseSdkStyleProjectFormat")!;
        SetDecompilerToggle(d.AppState, sdk, !sdk.DefaultValue);

        ResetDecompilerToggles(d.AppState);

        Assert.Equal(!sdk.DefaultValue, DecompilerToggles.IsOn(d.AppState.DecompilerOptions, sdk));
    }

    [Fact]
    public void The_dialog_warns_in_place_about_a_folder_that_holds_files()
    {
        var app = AppDriver.Opened();
        _temp.Write("notes.txt", "keep");
        app.OpenExportSetup(null);
        app.Ui.ExportSetup.Folder = _temp.Path;

        app.Run(2);

        Assert.Contains(app.Frames.Canvas.Texts, t => t.Inputs.Text.StartsWith("This folder already holds files.", StringComparison.Ordinal));
    }
}
