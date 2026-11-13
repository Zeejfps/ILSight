using ZGF.Desktop;
using ZGF.Gui.Immediate;
using ILSight.Core;
using ZGF.KeyboardModule;
using ILSight.ExportArea;

namespace ILSight.App.Tests;

public sealed class ExportTests : IDisposable
{
    private static readonly string SampleAppPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "SampleApp", "SampleApp.dll");

    private readonly TempDir _temp = new();

    public ExportTests()
    {
        Directory.CreateDirectory(_temp.Path);
    }

    public void Dispose()
    {
        _temp.Dispose();
    }

    // For what works through a whole assembly, which would take long on the fixtures, the whole test assembly.
    private static AppStateDriver OpenedSample()
    {
        var d = new AppStateDriver();
        d.Start((a, ct) => a.AssemblyOpening.OpenAssembliesAsync(a, [AssemblyPath.Of(SampleAppPath)], ct));
        d.RunUntil(() => d.AppState.Assemblies.IsListed(TestPaths.PathOf(SampleAppPath)));
        d.Settle();
        return d;
    }

    private static LoadedModule SampleOf(AppStateDriver d)
    {
        return Assert.Single(d.AppState.Assemblies.Assemblies);
    }

    [Fact]
    public async Task Saving_code_writes_the_node_in_the_current_language()
    {
        var d = AppStateDriver.Opened();
        d.AppState.Decompiler.UseLanguage(DecompilationLanguage.IL);
        var path = _temp.PathOf("Greeter.il");

        var saved = await d.FixtureAsync("Greeter");

        d.InTurn(() => ExportCommands.SaveCode(d.AppState, saved, path));
        d.Settle();

        Assert.Contains(".class public auto ansi beforefieldinit ILSight.App.Tests.Fixtures.Greeter", await File.ReadAllTextAsync(path));
        Assert.Empty(d.AppState.Errors.Entries);
    }

    [Fact]
    public async Task Saving_an_assembly_writes_one_code_file_even_under_a_project_name()
    {
        var d = OpenedSample();
        var path = _temp.PathOf("SampleApp.csproj");

        var saved = await SampleOf(d).ReadRootAsync();

        d.InTurn(() => ExportCommands.SaveCode(d.AppState, saved, path));
        d.Settle();

        var code = await File.ReadAllTextAsync(path);
        Assert.Contains("public class Counter", code);
        Assert.False(code.TrimStart().StartsWith("<Project", StringComparison.Ordinal));
        Assert.Equal([path], Directory.EnumerateFiles(_temp.Path));
    }

    [Fact]
    public async Task Saving_a_resource_writes_its_bytes_as_they_are()
    {
        var d = AppStateDriver.Opened();
        var node = await d.NodeAsync("Resources", "data.json");
        var path = _temp.PathOf("data.json");

        Assert.True(Exports.CanSaveCode(node));
        d.InTurn(() => ExportCommands.SaveCode(d.AppState, node, path));
        d.Settle();

        Assert.Equal(BrowseNodes.ReadResource(node), await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task Saving_a_resource_names_its_bytes_for_the_file_picked()
    {
        var d = AppStateDriver.Opened();
        var node = await d.NodeAsync("Resources", "data.json");
        var path = _temp.PathOf("data.json");
        var setup = new DecompilerSetup { Language = DecompilationLanguage.CSharp, Options = DecompilerOptions.Default };

        var save = Exports.ToCodeSave(node, setup, path);

        var bytes = Assert.IsType<CodeSave.ResourceBytes>(save);
        Assert.Same(node, bytes.Node);
        Assert.Equal(path, bytes.File);
    }

    [Fact]
    public async Task Saving_a_node_that_is_no_resource_names_its_code_in_the_setup_language_as_one_file()
    {
        var d = AppStateDriver.Opened();
        var node = await d.FixtureAsync("Greeter");
        var path = _temp.PathOf("Greeter.il");
        var setup = new DecompilerSetup { Language = DecompilationLanguage.IL, Options = DecompilerOptions.Default };

        var save = Exports.ToCodeSave(node, setup, path);

        var work = Assert.IsType<CodeSave.Code>(save).Work;
        Assert.Same(node, work.Node);
        Assert.Equal(DecompilationLanguage.IL, work.Language);
        Assert.Same(setup.Options, work.Options);
        Assert.Equal(path, work.File);
    }

    [Fact]
    public void Pdbs_are_generated_for_every_open_assembly_into_the_folder()
    {
        var d = OpenedSample();
        var assembly = SampleOf(d);

        ExportCommands.GeneratePdbs(d.AppState, _temp.Path);
        d.Settle();

        Assert.True(File.Exists(_temp.PathOf(assembly.PdbFileName)));
        Assert.Empty(d.AppState.Errors.Entries);
    }

    [Fact]
    public void The_tree_menu_creates_a_diagram_in_a_folder_named_after_the_assembly_and_opens_it()
    {
        var app = new AppDriver();
        app.Start((a, ct) => a.AssemblyOpening.OpenAssembliesAsync(a, [AssemblyPath.Of(SampleAppPath)], ct));
        app.Frames.WaitUntil(() => app.AppState.Assemblies.IsListed(TestPaths.PathOf(SampleAppPath)), AppStateDriver.Patience);
        app.Settle(2);
        app.Frames.FilePicker.Answer = _temp.Path;

        app.PickFromTreeMenu(SampleOf(app.Driver).Name, "Create diagram…");
        app.Frames.WaitUntil(() => app.Frames.OpenedFiles.Count == 1, AppStateDriver.Patience);

        var page = Path.Combine(_temp.Path, "SampleApp diagram", "index.html");
        var report = Assert.Single(app.AppState.FinishedExports);
        Assert.Equal(["Choose the folder for the diagram"], app.Frames.FilePicker.Titles);
        Assert.Equal(ExportKind.Diagram, report.Job.Kind);
        Assert.Equal([page], Assert.IsType<ExportEnd.Finished>(report.End).Written);
        Assert.Equal([page], app.Frames.OpenedFiles);
        Assert.Contains("class Counter", File.ReadAllText(page));
        Assert.Empty(app.AppState.Errors.Entries);
    }

    [Fact]
    public async Task Ctrl_S_saves_the_active_tab_where_the_user_picks()
    {
        var app = AppDriver.Opened();
        app.Show(await app.FixtureAsync("Greeter"), frames: 2);
        var path = _temp.PathOf("Greeter.cs");
        app.Frames.FilePicker.Answer = path;

        app.Frames.Key(Shortcuts.SaveCode).Frame(2);
        app.Settle();

        Assert.Equal(["Save code"], app.Frames.FilePicker.Titles);
        Assert.Contains("public class Greeter", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task Ctrl_S_on_baml_offers_to_save_it_as_xaml()
    {
        var app = AppDriver.Opened();
        var sample = Path.Combine(AppContext.BaseDirectory, "Fixtures", "SampleApp", "SampleApp.dll");
        app.Start((a, ct) => a.AssemblyOpening.OpenAssembliesAsync(a, [AssemblyPath.Of(sample)], ct));
        app.Driver.RunUntil(() => app.AppState.Assemblies.Assemblies.Any(a => a.FilePath.Text == sample));
        var assembly = app.AppState.Assemblies.Assemblies.Single(a => a.FilePath.Text == sample);
        var baml = await assembly.ReadRootAsync().GetChildAsync("Resources", "SampleApp.g.resources", "mainwindow.baml");
        app.Show(baml, frames: 2);
        var path = _temp.PathOf("mainwindow.xaml");
        app.Frames.FilePicker.Answer = path;

        app.Frames.Key(Shortcuts.SaveCode).Frame(2);
        app.Settle();

        var (fileName, filters) = app.Frames.FilePicker.LastSave;
        Assert.Equal("mainwindow.xaml", fileName);
        Assert.NotNull(filters);
        Assert.Equal(["*.xaml"], filters[0].Patterns);
        Assert.Contains("x:Class=\"SampleApp.MainWindow\"", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public void The_file_menu_shows_an_icon_beside_each_item()
    {
        var app = AppDriver.Opened();
        app.Run(2);

        app.Frames.ClickOn(Selector.WithText("File")).Frame(2);

        IconGlyph[] icons = [BuiltInGlyphs.Icons.FileOpen, BuiltInGlyphs.Icons.FileOpenFromFramework, BuiltInGlyphs.Icons.FileOpenFromProcess, BuiltInGlyphs.Icons.FileSaveCode, BuiltInGlyphs.Icons.FileExportProject,
            BuiltInGlyphs.Icons.FileGeneratePdb];
        Assert.All(icons, icon => Assert.True(app.Frames.Drew(BuiltInGlyphs.Text(icon))));
    }

    [Fact]
    public async Task An_export_asked_for_while_another_runs_waits_its_turn()
    {
        var d = new AppStateDriver();
        var queue = d.AppState.Export;
        var first = d.InTurn(() => queue.Enter(TestJobs.Named("First"))).Turn;
        var second = d.InTurn(() => queue.Enter(TestJobs.Named("Second"))).Turn;

        Assert.True(first.IsCompleted);
        Assert.False(second.IsCompleted);
        Assert.Equal(1, queue.WaitingCount);

        var started = Assert.IsType<ExportTurn.Started>(await first);
        d.InTurn(started.EndTurn);

        Assert.True(second.IsCompleted);
        Assert.Equal(0, queue.WaitingCount);
        Assert.Equal("Second", queue.Running?.Progress.Name);
    }

    [Fact]
    public async Task Only_the_running_export_can_end_its_turn()
    {
        var d = new AppStateDriver();
        var queue = d.AppState.Export;
        var first = Assert.IsType<ExportTurn.Started>(await d.InTurn(() => queue.Enter(TestJobs.Named("First"))).Turn);
        d.InTurn(first.EndTurn);
        var second = Assert.IsType<ExportTurn.Started>(await d.InTurn(() => queue.Enter(TestJobs.Named("Second"))).Turn);

        d.InTurn(first.EndTurn);

        Assert.True(queue.IsRunning);
        Assert.False(second.Token.IsCancellationRequested);
        Assert.Equal("Second", queue.Running?.Progress.Name);
    }

    [Fact]
    public async Task A_waiting_export_taken_out_of_the_queue_is_withdrawn_without_starting()
    {
        var d = new AppStateDriver();
        var queue = d.AppState.Export;
        var first = Assert.IsType<ExportTurn.Started>(await d.InTurn(() => queue.Enter(TestJobs.Named("First"))).Turn);
        var second = d.InTurn(() => queue.Enter(TestJobs.Named("Second"))).Turn;

        queue.Withdraw(Assert.Single(queue.Waiting).Run);

        Assert.IsType<ExportTurn.Withdrawn>(await second);
        Assert.Equal(0, queue.WaitingCount);
        Assert.Equal(first.Run, queue.Running?.Export.Run);
    }

    [Fact]
    public async Task CancelRunning_with_an_export_waiting_cancels_only_the_running_one()
    {
        var d = new AppStateDriver();
        var queue = d.AppState.Export;
        var first = Assert.IsType<ExportTurn.Started>(await d.InTurn(() => queue.Enter(TestJobs.Named("First"))).Turn);
        var second = d.InTurn(() => queue.Enter(TestJobs.Named("Second"))).Turn;

        queue.CancelRunning();

        Assert.True(first.Token.IsCancellationRequested);
        Assert.Equal(true, queue.Running?.IsCancelling);
        d.InTurn(first.EndTurn);
        var next = Assert.IsType<ExportTurn.Started>(await second);
        Assert.False(next.Token.IsCancellationRequested);
        Assert.Equal(false, queue.Running?.IsCancelling);
    }

    [Fact]
    public async Task Progress_that_arrives_after_its_turn_ended_is_dropped()
    {
        var d = new AppStateDriver();
        var queue = d.AppState.Export;
        var first = Assert.IsType<ExportTurn.Started>(await d.InTurn(() => queue.Enter(TestJobs.Named("First"))).Turn);
        first.Progress.Report(new ExportProgress { Name = "First", Part = 1, Parts = 1, Fraction = 0.5f });
        d.InTurn(first.EndTurn);
        Assert.True(d.InTurn(() => queue.Enter(TestJobs.Named("Second"))).Turn.IsCompletedSuccessfully);

        d.Pump();

        Assert.Equal(new ExportProgress
        {
            Name = "Second",
            Part = 1,
            Parts = 1,
            Fraction = null,
        }, queue.Running?.Progress);
    }

    [Fact]
    public async Task Progress_of_the_running_export_arrives_on_the_ui_thread()
    {
        var d = new AppStateDriver();
        var queue = d.AppState.Export;
        var start = Assert.IsType<ExportTurn.Started>(await d.InTurn(() => queue.Enter(TestJobs.Named("Fixtures"))).Turn);

        start.Progress.Report(new ExportProgress { Name = "Fixtures", Part = 1, Parts = 1, Fraction = 0.75f });
        var beforePump = queue.Running;
        Assert.NotNull(beforePump);
        Assert.Null(beforePump.Progress.Fraction);
        d.Pump();

        Assert.Equal(0.75f, queue.Running?.Progress.Fraction);
    }

    [Fact]
    public async Task Progress_that_arrives_out_of_order_never_steps_the_bar_back()
    {
        var d = new AppStateDriver();
        var queue = d.AppState.Export;
        var start = Assert.IsType<ExportTurn.Started>(await d.InTurn(() => queue.Enter(TestJobs.Named("Fixtures"))).Turn);

        start.Progress.Report(new ExportProgress { Name = "Fixtures", Part = 1, Parts = 1, Fraction = 0.6f });
        start.Progress.Report(new ExportProgress { Name = "Fixtures", Part = 1, Parts = 1, Fraction = 0.4f });
        var beforePump = queue.Running;
        Assert.NotNull(beforePump);
        Assert.Null(beforePump.Progress.Fraction);
        d.Pump();

        Assert.Equal(0.6f, queue.Running?.Progress.Fraction);
    }

    [Fact]
    public async Task Exports_asked_for_together_all_run_one_after_another()
    {
        var d = AppStateDriver.Opened();
        var greeter = _temp.PathOf("Greeter.cs");
        var farewell = _temp.PathOf("Farewell.cs");

        var greeterNode = await d.FixtureAsync("Greeter");
        var farewellNode = await d.FixtureAsync("Farewell");
        d.InTurn(() => ExportCommands.SaveCode(d.AppState, greeterNode, greeter));
        d.InTurn(() => ExportCommands.SaveCode(d.AppState, farewellNode, farewell));
        Assert.Equal(1, d.AppState.Export.WaitingCount);
        d.Settle();

        Assert.True(File.Exists(greeter));
        Assert.True(File.Exists(farewell));
        Assert.Null(d.AppState.Export.Running);
        Assert.Equal(0, d.AppState.Export.WaitingCount);
    }

    [Fact]
    public void A_cancelled_export_ends_without_an_error()
    {
        var d = AppStateDriver.Opened();
        d.OpenExportSetup(null);
        d.Ui.ExportSetup.Folder = _temp.PathOf("Project");

        d.StartExport();
        d.AppState.Export.CancelRunning();
        d.Settle();

        Assert.Null(d.AppState.Export.Running);
        Assert.Empty(d.AppState.Errors.Entries);
    }

    [Fact]
    public async Task The_status_bar_shows_the_export_and_how_many_wait_and_cancels_it()
    {
        var app = AppDriver.Opened();
        var start = Assert.IsType<ExportTurn.Started>(await app.Driver.InTurn(() => app.AppState.Export.Enter(TestJobs.Named("Fixtures"))).Turn);
        Assert.False(app.Driver.InTurn(() => app.AppState.Export.Enter(TestJobs.Named("Later"))).Turn.IsCompleted);
        start.Progress.Report(new ExportProgress { Name = "Fixtures", Part = 2, Parts = 3, Fraction = 0.5f });
        app.Run(2);

        Assert.True(app.Frames.Drew("Exporting 2/3 · Fixtures"));
        Assert.True(app.Frames.Drew("1 more queued"));
        app.Frames.ClickOn(Selector.AtPath("cancel-export")).Frame();

        Assert.True(start.Token.IsCancellationRequested);
    }
    [Fact]
    public async Task A_finished_export_reports_what_it_wrote()
    {
        var d = AppStateDriver.Opened();
        var path = _temp.PathOf("Greeter.cs");

        var saved = await d.FixtureAsync("Greeter");

        d.InTurn(() => ExportCommands.SaveCode(d.AppState, saved, path));
        d.Settle();

        var report = Assert.Single(d.AppState.FinishedExports);
        var finished = Assert.IsType<ExportEnd.Finished>(report.End);
        Assert.Equal("Greeter", report.Name);
        Assert.Equal(_temp.Path, report.Folder);
        Assert.Equal([path], finished.Written);
        Assert.Empty(finished.Problems);
    }

    [Fact]
    public void A_cancelled_export_reports_that_it_was_cancelled()
    {
        var d = AppStateDriver.Opened();
        d.OpenExportSetup(null);
        d.Ui.ExportSetup.Folder = _temp.PathOf("Project");

        d.StartExport();
        d.AppState.Export.CancelRunning();
        d.Settle();

        var report = Assert.Single(d.AppState.FinishedExports);
        Assert.IsType<ExportEnd.Cancelled>(report.End);
    }

    [Fact]
    public async Task A_failed_export_reports_why_instead_of_logging_an_error()
    {
        var d = AppStateDriver.Opened();
        var path = _temp.PathOf(Path.Combine("missing", "Greeter.cs"));

        var saved = await d.FixtureAsync("Greeter");

        d.InTurn(() => ExportCommands.SaveCode(d.AppState, saved, path));
        d.Settle();

        var report = Assert.Single(d.AppState.FinishedExports);
        var failed = Assert.IsType<ExportEnd.Failed>(report.End);
        Assert.Equal("Cannot save the code of Greeter", ExportReportText.Headline(Strings.En, report.Job.Kind, failed));
        Assert.Contains("DirectoryNotFoundException", ExportReportText.DetailsOf(failed));
        Assert.Empty(d.AppState.Errors.Entries);
    }

    [Fact]
    public void The_file_menu_offers_the_export_dialog_and_no_solution_of_everything()
    {
        var app = AppDriver.Opened();
        app.Run(2);

        app.Frames.ClickOn(Selector.WithText("File")).Frame(2);

        Assert.True(app.Frames.Drew("Export…"));
        Assert.False(app.Frames.Drew("Export all as solution…"));
    }
}
