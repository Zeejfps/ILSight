using System.Threading.Channels;
using ZGF.Gui.Immediate;
using ILSight.Core;
using ILSight.CommandArea;
using ILSight.AppSettingArea;
using ILSight.AssemblyListArea;
using ILSight.ILSightAppArea;
using ILSight.SettingArea;
using static ILSight.ILSightAppArea.RunArgumentsActions;
using static ILSight.SettingArea.Settings;
using static ILSight.App.Tests.TestPaths;

namespace ILSight.App.Tests;

public sealed class RunArgumentsTests
{
    private const string Other = "Other";
    private static readonly string OtherAssembly = typeof(AppState).Assembly.Location;
    private static readonly string CoreAssembly = typeof(AssemblyPath).Assembly.Location;

    [Fact]
    public void Options_are_read_apart_from_the_files_to_open()
    {
        var arguments = CommandLines.Parse(["--language=il", "--list= Work ", "--clear-list", "a.dll"]).Arguments;

        Assert.Equal(DecompilationLanguage.IL, arguments.Language);
        Assert.Equal("Work", arguments.AssemblyList);
        Assert.True(arguments.ClearList);
        Assert.Equal([Path.GetFullPath("a.dll")], arguments.Files.Paths.Select(static p => p.Text));
    }

    [Fact]
    public void What_to_navigate_to_and_a_new_window_are_read_from_their_options()
    {
        var commandLine = CommandLines.Parse(["--navigateto= T:Some.Type ", "--newinstance"]);

        Assert.Equal("T:Some.Type", commandLine.Arguments.Files.NavigateTo);
        Assert.True(commandLine.Launch.NewInstance);
        Assert.Empty(commandLine.Arguments.Files.Paths);
    }

    [Fact]
    public void A_command_line_is_handed_over_with_its_files_made_absolute_and_reads_back_the_same()
    {
        var arguments = CommandLines.Parse(["--language=IL", "--list=Work", "--clear-list", "--navigateto=T:Some.Type", "--gl", "--newinstance", "a.dll"]).Arguments;

        var handed = CommandLines.Parse(CommandLines.ListArguments(arguments).ToArray()).Arguments;

        Assert.Equal(DecompilationLanguage.IL, handed.Language);
        Assert.Equal("Work", handed.AssemblyList);
        Assert.True(handed.ClearList);
        Assert.Equal("T:Some.Type", handed.Files.NavigateTo);
        Assert.Empty(handed.Ignored);
        Assert.Equal([Path.GetFullPath("a.dll")], handed.Files.Paths.Select(static p => p.Text));
    }

    [Fact]
    public void A_command_line_naming_a_member_opens_its_files_and_shows_the_member()
    {
        var d = new AppStateDriver();

        var files = CommandLines.Parse(["--navigateto=M:ILSight.App.Tests.Fixtures.Greeter.Greet(System.Int32)", AppStateDriver.FixturePath]).Arguments.Files;
        d.Start((a, ct) => OpenRequestedFilesAsync(a, files, ct));
        d.RunUntil(() => d.AppState.Navigation.ActiveTab?.Node is not null);
        d.Settle();

        Assert.Equal("Greet(int) : string", d.AppState.Navigation.ActiveTab?.Node?.Text);
        Assert.Empty(d.AppState.Errors.Entries);
    }

    [Fact]
    public void A_command_line_naming_what_its_files_lack_reports_it()
    {
        var d = new AppStateDriver();

        var files = CommandLines.Parse(["--navigateto=T:Nowhere.Missing", AppStateDriver.FixturePath]).Arguments.Files;
        d.Start((a, ct) => OpenRequestedFilesAsync(a, files, ct));
        d.RunUntil(() => d.AppState.Errors.Entries.Count > 0);

        Assert.Contains("T:Nowhere.Missing", Assert.Single(d.AppState.Errors.Entries).Words().Title);
    }

    [Fact]
    public void A_language_the_app_does_not_have_and_blank_values_are_ignored_as_given()
    {
        var commandLine = CommandLines.Parse(["--language=VB", "--list=  ", "--navigateto=", "--settings= "]);

        Assert.Null(commandLine.Arguments.Language);
        Assert.Null(commandLine.Arguments.AssemblyList);
        Assert.Null(commandLine.Arguments.Files.NavigateTo);
        Assert.Equal(AppSettings.DefaultPath, commandLine.Launch.SettingsPath);
        Assert.Equal(["--language=VB", "--list=  ", "--navigateto=", "--settings= "], commandLine.Arguments.Ignored);
    }

    [Fact]
    public void An_option_the_app_does_not_have_and_a_file_that_is_no_path_are_ignored_as_given()
    {
        var commandLine = CommandLines.Parse(["--opengl", "--GL", "a\0.dll", "a.dll"]);

        Assert.Equal(RenderBackendKind.Auto, commandLine.Launch.RenderBackend);
        Assert.Equal([Path.GetFullPath("a.dll")], commandLine.Arguments.Files.Paths.Select(static p => p.Text));
        Assert.Equal(["--opengl", "--GL", "a\0.dll"], commandLine.Arguments.Ignored);
    }

    [Fact]
    public void How_a_run_starts_is_read_apart_from_its_arguments()
    {
        var commandLine = CommandLines.Parse(["--settings= other.json ", "--gl", "--newinstance", "--language=IL"]);

        Assert.Equal(new LaunchConfig
        {
            SettingsPath = FullFilePath.Of(Path.GetFullPath("other.json")),
            RenderBackend = RenderBackendKind.OpenGl,
            NewInstance = true,
        }, commandLine.Launch);
        Assert.Equal(DecompilationLanguage.IL, commandLine.Arguments.Language);
        Assert.Empty(commandLine.Arguments.Ignored);
    }

    [Fact]
    public void A_settings_file_is_made_absolute_from_this_process_s_folder_so_it_has_one()
    {
        var bare = CommandLines.Parse(["--settings=settings.json"]).Launch.SettingsPath;
        var nested = CommandLines.Parse(["--settings=" + Path.Combine("sub", "..", "other.json")]).Launch.SettingsPath;

        Assert.Equal(Path.GetFullPath("settings.json"), bare.Text);
        Assert.Equal(Environment.CurrentDirectory, bare.Folder);
        Assert.Equal(Path.GetFullPath("other.json"), nested.Text);
    }

    [Fact]
    public void A_settings_path_that_names_no_file_is_ignored_as_given()
    {
        var folder = "--settings=sub" + Path.DirectorySeparatorChar;
        var invalid = "--settings=a\0.json";

        var commandLine = CommandLines.Parse([folder, invalid]);

        Assert.Equal(AppSettings.DefaultPath, commandLine.Launch.SettingsPath);
        Assert.Equal([folder, invalid], commandLine.Arguments.Ignored);
    }

    [Fact]
    public void The_arguments_ignored_are_handed_over_for_the_open_window_to_tell_of()
    {
        var arguments = CommandLines.Parse(["--language=VB", "--opengl", "a.dll"]).Arguments;

        var handed = CommandLines.Parse(CommandLines.ListArguments(arguments).ToArray()).Arguments;

        Assert.Equal(["--language=VB", "--opengl"], handed.Ignored);
        Assert.Equal([Path.GetFullPath("a.dll")], handed.Files.Paths.Select(static p => p.Text));
    }

    [Fact]
    public void Starting_with_arguments_the_app_ignores_tells_of_them()
    {
        var d = Start(new AppSettings(), "--language=il-csharp", "--opengl", "--language=IL");

        var error = Assert.Single(d.AppState.Errors.Entries).Words();
        Assert.Contains("2", error.Title);
        Assert.Equal($"--language=il-csharp{Environment.NewLine}--opengl", error.Details);
        Assert.Equal(DecompilationLanguage.IL, d.AppState.Decompiler.Setup.Language);
    }

    [Fact]
    public void Starting_with_arguments_the_app_understands_tells_of_nothing()
    {
        var d = Start(new AppSettings(), "--language=IL", "--gl", "--newinstance");

        Assert.Empty(d.AppState.Errors.Entries);
    }

    [Fact]
    public void The_language_named_is_the_one_decompiled_to()
    {
        var csharp = Start(new AppSettings { Language = DecompilationLanguage.IL }, "--language=C#");
        var il = Start(new AppSettings { Language = DecompilationLanguage.CSharp }, "--language=IL");

        Assert.Equal(DecompilationLanguage.CSharp, csharp.AppState.Decompiler.Setup.Language);
        Assert.Equal(DecompilationLanguage.IL, il.AppState.Decompiler.Setup.Language);
    }

    [Fact]
    public void A_list_named_that_there_is_none_of_is_added_and_opened_empty()
    {
        using var temp = new TempDir();
        var d = Start(SettingsWith(temp, new AssemblyListRecord
        {
            Name = SavedAssemblyLists.DefaultName,
            Assemblies = [PathOf(AppStateDriver.FixturePath)],
        }), $"--list={Other}");

        Assert.Equal(Other, d.AppState.Lists.Active);
        Assert.Empty(d.AppState.Assemblies.ListFilePaths());
        Assert.Equal([AppStateDriver.FixturePath], d.AppState.Lists.GetPaths(SavedAssemblyLists.DefaultName).Select(static p => p.Text));
    }

    [Fact]
    public void A_list_named_in_any_case_opens_with_its_assemblies_and_the_files_named()
    {
        using var temp = new TempDir();
        var settings = SettingsWith(temp,
            new AssemblyListRecord { Name = SavedAssemblyLists.DefaultName, Assemblies = [] },
            new AssemblyListRecord { Name = Other, Assemblies = [PathOf(AppStateDriver.FixturePath)] });

        var d = Start(settings, "--list=other", OtherAssembly);
        Assert.IsType<SettingsWriteResult.Written>(d.AppState.SettingsSaving.Save(d.AppState));

        AssemblyPath[] opened = [PathOf(AppStateDriver.FixturePath), PathOf(OtherAssembly)];
        Assert.Equal(Other, d.AppState.Lists.Active);
        Assert.Equal(opened.OrderBy(static p => p.Text), d.AppState.Assemblies.ListFilePaths().OrderBy(static p => p.Text));
        Assert.Equal(Other, temp.Settings().ActiveAssemblyList);
    }

    [Fact]
    public void Clearing_the_list_opens_only_the_files_named_and_saves_the_list_with_them()
    {
        using var temp = new TempDir();
        var d = Start(SettingsWith(temp, new AssemblyListRecord
        {
            Name = SavedAssemblyLists.DefaultName,
            Assemblies = [PathOf(AppStateDriver.FixturePath)],
        }), "--clear-list", OtherAssembly);
        Assert.IsType<SettingsWriteResult.Written>(d.AppState.SettingsSaving.Save(d.AppState));

        Assert.Equal([OtherAssembly], d.AppState.Assemblies.ListFilePaths().Select(static p => p.Text));
        Assert.Equal([OtherAssembly], Assert.Single(temp.Settings().AssemblyLists).Assemblies.Select(static p => p.Text));
    }

    [Fact]
    public void A_list_switched_to_and_cleared_at_startup_reads_none_of_the_saved_list_s_files_and_ends_as_handed_over()
    {
        using var startedTemp = new TempDir();
        using var openTemp = new TempDir();
        var arguments = CommandLines.Parse([$"--list={Other}", "--clear-list", CoreAssembly]).Arguments;
        var listing = new ListingRecord();

        var started = new AppDriver(settings: DefaultAndOther(startedTemp), queuedArguments: arguments, reopening: listing.Over);
        Assert.DoesNotContain(PathOf(AppStateDriver.FixturePath), started.AppState.Assemblies.Arriving);
        Assert.False(started.AppState.IsReloading);
        started.Frames.WaitUntil(() => AreListed(started.AppState, arguments.Files.Paths), Patience);
        started.Settle();

        var inbox = Channel.CreateUnbounded<RunArguments>();
        var handed = new AppDriver(settings: DefaultAndOther(openTemp), inbox: inbox);
        handed.Frames.WaitUntil(() => handed.AppState.Assemblies.IsListed(PathOf(AppStateDriver.FixturePath)), Patience);
        handed.Settle();
        Assert.True(inbox.Writer.TryWrite(arguments));
        handed.Frames.WaitUntil(() => AreListed(handed.AppState, arguments.Files.Paths), Patience);
        handed.Settle();

        Assert.DoesNotContain(PathOf(AppStateDriver.FixturePath), listing.Asked);
        Assert.Equal(Other, started.AppState.Lists.Active);
        Assert.Equal([CoreAssembly], started.AppState.Assemblies.ListFilePaths().Select(static p => p.Text));
        Assert.Empty(started.AppState.Errors.Entries);
        Assert.Equal(Describe(handed), Describe(started));
        Assert.Equal(DescribeSaved(handed, openTemp), DescribeSaved(started, startedTemp));
        Assert.Equal([AppStateDriver.FixturePath], startedTemp.Settings().AssemblyLists.Single(static l => l.Name == SavedAssemblyLists.DefaultName)
            .Assemblies.Select(static p => p.Text));
    }

    [Fact]
    public async Task A_run_that_sends_a_file_as_soon_as_the_role_is_claimed_has_it_listed_in_the_list_the_first_run_switches_to()
    {
        using var temp = new TempDir();
        var settingsPath = FullFilePath.Of(temp.PathOf("settings.json"));
        var first = CommandLines.Parse([$"--list={Other}"]).Arguments;
        var later = CommandLines.Parse([CoreAssembly]).Arguments;

        // The later run sends as soon as a receiver listens: its hand-over waits for the pipe the claim opens.
        var routing = Task.Run(() => AppRoot.RouteRunAsync(settingsPath, first, newInstance: false, CancellationToken.None));
        var isTaken = await RunPipes.TrySendAsync(RunArgumentsRouting.GetAddress(settingsPath), later);
        using var receiver = await routing;
        Assert.NotNull(receiver);
        var app = new AppDriver(settings: DefaultAndOther(temp), inbox: receiver.Inbox);
        app.Frames.WaitUntil(() => AreListed(app.AppState, later.Files.Paths), Patience);
        app.Settle();

        Assert.True(isTaken);
        Assert.Equal(Other, app.AppState.Lists.Active);
        Assert.Equal([CoreAssembly, OtherAssembly], app.AppState.Assemblies.ListFilePaths().Select(static p => p.Text).Order());
        Assert.Empty(app.AppState.Errors.Entries);
    }

    private static AppSettings SettingsWith(TempDir temp, params AssemblyListRecord[] lists)
    {
        var settings = temp.Settings();
        settings.AssemblyLists = lists.ToList();
        return settings;
    }

    /// <summary>Settings whose open list, Default, has the fixture assembly, beside a list named Other with this app's
    /// own.</summary>
    private static AppSettings DefaultAndOther(TempDir temp)
    {
        return SettingsWith(temp,
            new AssemblyListRecord { Name = SavedAssemblyLists.DefaultName, Assemblies = [PathOf(AppStateDriver.FixturePath)] },
            new AssemblyListRecord { Name = Other, Assemblies = [PathOf(OtherAssembly)] });
    }

    /// <summary>Starts over <paramref name="settings"/> with the command line of <paramref name="args"/> the way the
    /// launcher does: its arguments in the app's inbox before the app takes them, ahead of its first frame; then waits
    /// for the files they name to open and the app to settle.</summary>
    private static AppDriver Start(AppSettings settings, params string[] args)
    {
        var arguments = CommandLines.Parse(args).Arguments;
        var app = new AppDriver(settings: settings, queuedArguments: arguments);
        app.Frames.WaitUntil(() => AreListed(app.AppState, arguments.Files.Paths), Patience);
        app.Settle();
        return app;
    }

    /// <summary>What a user sees of <paramref name="app"/>'s lists: the open one, its assemblies, its analyses, the
    /// errors and the tabs' titles.</summary>
    private static string Describe(AppDriver app)
    {
        var appState = app.AppState;
        var assemblies = string.Join(",", appState.Assemblies.ListFilePaths().Select(static p => p.Text));
        var analyses = string.Join(",", appState.Analyzer.ListSavedAnalyses());
        var errors = string.Join(",", appState.Errors.Entries.Select(static e => e.Words().Title));
        var tabs = string.Join(",", appState.Navigation.OpenTabs.Select(static t => t.Title));
        return $"{appState.Lists.Active}: [{assemblies}] analyses [{analyses}] errors [{errors}] tabs [{tabs}]";
    }

    /// <summary>Every list as <paramref name="app"/> saves it into <paramref name="temp"/>'s settings: its name, files
    /// and analyses.</summary>
    private static List<string> DescribeSaved(AppDriver app, TempDir temp)
    {
        Assert.IsType<SettingsWriteResult.Written>(app.AppState.SettingsSaving.Save(app.AppState));
        return temp.Settings().AssemblyLists
            .Select(static l => $"{l.Name}: [{string.Join(",", l.Assemblies.Select(static p => p.Text))}] analyses [{string.Join(",", l.Analyzed ?? [])}]")
            .ToList();
    }

    private static bool AreListed(AppState appState, List<AssemblyPath> paths)
    {
        return paths.All(path => appState.Assemblies.IsListed(path));
    }
}
