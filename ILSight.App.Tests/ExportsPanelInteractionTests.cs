using ZGF.Desktop;
using ZGF.Gui.Immediate;
using ILSight.Core;
using ZGF.KeyboardModule;
using ILSight.ExportArea;
using ILSight.DocumentViewArea;

namespace ILSight.App.Tests;

public sealed class ExportsPanelInteractionTests : IDisposable
{
    private const float TallWindow = 1400f;

    private const float RowReach = 100f;

    private readonly TempDir _temp = new();

    public ExportsPanelInteractionTests()
    {
        Directory.CreateDirectory(_temp.Path);
    }

    public void Dispose()
    {
        _temp.Dispose();
    }

    private async Task<AppDriver> TwoFinishedAsync(float width = 1000f)
    {
        var app = AppDriver.Opened(width: width, height: TallWindow);
        var greeterNode = await app.FixtureAsync("Greeter");
        app.Driver.InTurn(() => ExportCommands.SaveCode(app.AppState, greeterNode, Path.Combine(Folder("First"), "Greeter.cs")));
        var farewellNode = await app.FixtureAsync("Farewell");
        app.Driver.InTurn(() => ExportCommands.SaveCode(app.AppState, farewellNode, Path.Combine(Folder("Second"), "Farewell.cs")));
        app.Settle(2);
        return app;
    }

    private string Folder(string name)
    {
        var folder = _temp.PathOf(name);
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static ExportRun RunOf(AppDriver app, string name)
    {
        return app.AppState.FinishedExports.Single(r => r.Name == name).Run;
    }

    [Fact]
    public async Task Clicking_a_row_selects_it_and_the_arrow_keys_walk_the_list()
    {
        var app = await TwoFinishedAsync();

        app.Frames.Wait(1).Click(app.TextRect("Farewell")).Frame();
        Assert.Equal(RunOf(app, "Farewell"), app.Ui.SelectedExport);

        app.Frames.Key(KeyboardKey.DownArrow).Frame();
        Assert.Equal(RunOf(app, "Greeter"), app.Ui.SelectedExport);
        app.Frames.Key(KeyboardKey.UpArrow).Frame();
        Assert.Equal(RunOf(app, "Farewell"), app.Ui.SelectedExport);
    }

    [Fact]
    public async Task Enter_and_a_double_click_open_the_report_of_a_finished_export()
    {
        var app = await TwoFinishedAsync();
        app.Frames.Wait(1).Click(app.TextRect("Greeter")).Frame();

        app.Frames.Key(KeyboardKey.Enter).Frame(2);
        var active = app.AppState.Navigation.ActiveTab;
        Assert.NotNull(active);
        Assert.Equal("Export report", DocumentsView.Tab(app.Ui, app.AppState.Navigation, active).Title);
        var tabs = app.AppState.Navigation.OpenTabs.Count();

        app.Frames.Wait(1).DoubleClick(app.TextRect("Farewell")).Frame(2);
        Assert.Equal(tabs + 1, app.AppState.Navigation.OpenTabs.Count());
    }

    [Fact]
    public async Task Delete_takes_the_selected_export_off_the_history()
    {
        var app = await TwoFinishedAsync();
        app.Frames.Wait(1).Click(app.TextRect("Greeter")).Frame();

        app.Frames.Key(KeyboardKey.Delete).Frame();

        Assert.Equal("Farewell", Assert.Single(app.AppState.FinishedExports).Name);
    }

    [Fact]
    public async Task Ctrl_C_copies_the_folder_of_the_selected_export()
    {
        var app = await TwoFinishedAsync();
        app.Frames.Wait(1).Click(app.TextRect("Greeter")).Frame();

        app.Frames.Key(Shortcuts.Copy).Frame();

        Assert.Equal(_temp.PathOf("First"), app.Frames.Clipboard.Text);
    }

    [Fact]
    public async Task The_context_menu_offers_what_suits_a_finished_export()
    {
        var app = await TwoFinishedAsync();

        app.Frames.Wait(1).Click(app.TextRect("Greeter"), button: PointerButton.Right).Frame(2);
        Assert.True(app.Frames.Drew("Copy folder path"));
        app.Frames.ClickOn(Selector.OfRole(SemanticRole.MenuItem, "Remove from history")).Frame();

        Assert.Equal("Farewell", Assert.Single(app.AppState.FinishedExports).Name);
    }

    [Fact]
    public async Task Shift_F10_opens_the_menu_of_the_selected_export()
    {
        var app = await TwoFinishedAsync();
        app.Frames.Wait(1).Click(app.TextRect("Greeter")).Frame();

        app.Frames.Key(KeyboardKey.F10, KeyModifiers.Shift).Frame(2);

        Assert.True(app.Frames.Drew("Copy folder path"));
        Assert.Equal(RunOf(app, "Greeter"), app.Ui.ExportMenuTarget);
    }

    [Fact]
    public async Task A_waiting_export_is_withdrawn_from_the_keyboard_and_the_running_one_stays()
    {
        var app = AppDriver.Opened(height: TallWindow);
        ScreenLayout.OpenPanel(app.Ui, DockView.Exports);
        var running = Assert.IsType<ExportTurn.Started>(await app.Driver.InTurn(() => app.AppState.Export.Enter(TestJobs.Named("Running"))).Turn);
        Assert.False(app.Driver.InTurn(() => app.AppState.Export.Enter(TestJobs.Named("Later"))).Turn.IsCompleted);
        app.Run(2);

        app.Frames.Wait(1).Click(app.TextRect("Running")).Frame();
        app.Frames.Key(KeyboardKey.Delete).Frame();
        Assert.True(app.AppState.Export.IsRunning);
        Assert.Equal(1, app.AppState.Export.WaitingCount);

        app.Frames.Key(KeyboardKey.DownArrow).Frame();
        app.Frames.Key(KeyboardKey.Delete).Frame();
        Assert.Equal(0, app.AppState.Export.WaitingCount);
        app.Driver.InTurn(running.EndTurn);
    }

    [Fact]
    public async Task The_remove_from_queue_button_takes_the_export_out_mid_frame_without_breaking_the_list()
    {
        var app = AppDriver.Opened(height: TallWindow);
        ScreenLayout.OpenPanel(app.Ui, DockView.Exports);
        var running = Assert.IsType<ExportTurn.Started>(await app.Driver.InTurn(() => app.AppState.Export.Enter(TestJobs.Named("Running"))).Turn);
        Assert.False(app.Driver.InTurn(() => app.AppState.Export.Enter(TestJobs.Named("Second"))).Turn.IsCompleted);
        Assert.False(app.Driver.InTurn(() => app.AppState.Export.Enter(TestJobs.Named("Third"))).Turn.IsCompleted);
        app.Run(2);

        app.Frames.Wait(1).Click(app.TextRect("Remove from queue")).Frame(2);

        Assert.Equal(["Third"], app.AppState.Export.Waiting.Select(q => q.Job.Name));
        app.Driver.InTurn(running.EndTurn);
    }

    [Fact]
    public async Task A_narrow_panel_shows_its_actions_as_icons()
    {
        var app = await TwoFinishedAsync(width: 560f);
        app.Run(2);

        Assert.False(app.Frames.Drew("Open folder"));
        Assert.True(app.Frames.Drew(BuiltInGlyphs.Text(BuiltInGlyphs.Icons.ExportOpenFolder)));
    }

    [Fact]
    public async Task A_row_lines_its_icon_name_and_actions_up_and_runs_the_bar_from_the_name_to_the_actions()
    {
        var app = AppDriver.Opened(height: TallWindow);
        var theme = app.Theme;
        ScreenLayout.OpenPanel(app.Ui, DockView.Exports);
        var running = Assert.IsType<ExportTurn.Started>(await app.Driver.InTurn(() => app.AppState.Export.Enter(TestJobs.Named("Running"))).Turn);
        running.Progress.Report(new ExportProgress { Name = "Fixtures", Part = 1, Parts = 1, Fraction = 0.5f });
        app.Run(2);

        var name = app.TextRect("Running");
        var cancel = app.TextRect("Cancel");
        var percent = app.TextRect("50%");
        var track = app.Frames.Canvas.Rects
            .Select(r => (r.Inputs.Style.BackgroundColor, Rect: app.Frames.ToUi(r.Inputs.Position)))
            .Where(r => r.BackgroundColor == theme.Control && Math.Abs(r.Rect.Height - theme.ProgressHeight) < 0.5f)
            .First(r => r.Rect.Y > name.Y && r.Rect.Y < name.Y + RowReach)
            .Rect;
        var summary = app.TextRect("1 running");

        Assert.Equal(name.Y + name.Height / 2, cancel.Y + cancel.Height / 2, 1f);
        Assert.Equal(name.Y + name.Height / 2, percent.Y + percent.Height / 2, 1f);
        Assert.Equal(name.X, track.X, 1f);
        Assert.True(track.Right >= cancel.Right);
        Assert.Equal(name.X - theme.IconSize - theme.Gap, summary.X, 1f);
        app.Driver.InTurn(running.EndTurn);
    }

    [Fact]
    public async Task The_running_export_keeps_the_frames_coming_so_its_spinner_turns()
    {
        var app = AppDriver.Opened();
        ScreenLayout.OpenPanel(app.Ui, DockView.Exports);
        var running = Assert.IsType<ExportTurn.Started>(await app.Driver.InTurn(() => app.AppState.Export.Enter(TestJobs.Named("Running"))).Turn);
        app.Run(2);

        Assert.False(app.Frames.Drew(BuiltInGlyphs.Text(BuiltInGlyphs.Icons.StatusLoading)));
        Assert.True(app.Frames.WakeAt - app.Frames.RealTime <= 1.0 / 30.0 + 1e-6);
        app.Driver.InTurn(running.EndTurn);
    }
}
