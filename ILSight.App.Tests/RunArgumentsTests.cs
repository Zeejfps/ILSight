using ZGF.Gui.Immediate;
using ILSight.Core;
using ILSight.Commanding;
using ILSight.AppSettingsFormat;
using ILSight.AssemblyListKeeping;
using ILSight.AppHosting;
using ILSight.SettingsKeeping;
using static ILSight.AppHosting.RunArgumentsActions;
using static ILSight.SettingsKeeping.Settings;
using static ILSight.App.Tests.TestPaths;

namespace ILSight.App.Tests;

public sealed class RunArgumentsTests
{
    private const string Other = "Other";
    private static readonly string OtherAssembly = typeof(AppState).Assembly.Location;

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

        Assert.Equal(new LaunchConfig(FullFilePath.Of(Path.GetFullPath("other.json")), RenderBackendKind.OpenGl, NewInstance: true), commandLine.Launch);
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
        Assert.Equal(DecompilationLanguage.IL, d.AppState.Language);
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

        Assert.Equal(DecompilationLanguage.CSharp, csharp.AppState.Language);
        Assert.Equal(DecompilationLanguage.IL, il.AppState.Language);
    }

    [Fact]
    public void A_list_named_that_there_is_none_of_is_added_and_opened_empty()
    {
        using var temp = new TempDir();
        var d = Start(SettingsWith(temp, new AssemblyListRecord(SavedAssemblyLists.DefaultName, [PathOf(AppStateDriver.FixturePath)])), $"--list={Other}");

        Assert.Equal(Other, d.AppState.Lists.Active);
        Assert.Empty(AssemblyListing.ListFilePaths(d.AppState.Assemblies));
        Assert.Equal([AppStateDriver.FixturePath], d.AppState.Lists.GetPaths(SavedAssemblyLists.DefaultName).Select(static p => p.Text));
    }

    [Fact]
    public void A_list_named_in_any_case_opens_with_its_assemblies_and_the_files_named()
    {
        using var temp = new TempDir();
        var settings = SettingsWith(temp,
            new AssemblyListRecord(SavedAssemblyLists.DefaultName, []),
            new AssemblyListRecord(Other, [PathOf(AppStateDriver.FixturePath)]));

        var d = Start(settings, "--list=other", OtherAssembly);
        d.AppState.SettingsSaving.Save(d.AppState);

        AssemblyPath[] opened = [PathOf(AppStateDriver.FixturePath), PathOf(OtherAssembly)];
        Assert.Equal(Other, d.AppState.Lists.Active);
        Assert.Equal(opened.OrderBy(static p => p.Text), AssemblyListing.ListFilePaths(d.AppState.Assemblies).OrderBy(static p => p.Text));
        Assert.Equal(Other, temp.Settings().ActiveAssemblyList);
    }

    [Fact]
    public void Clearing_the_list_opens_only_the_files_named_and_saves_the_list_with_them()
    {
        using var temp = new TempDir();
        var d = Start(SettingsWith(temp, new AssemblyListRecord(SavedAssemblyLists.DefaultName, [PathOf(AppStateDriver.FixturePath)])), "--clear-list", OtherAssembly);
        d.AppState.SettingsSaving.Save(d.AppState);

        Assert.Equal([OtherAssembly], AssemblyListing.ListFilePaths(d.AppState.Assemblies).Select(static p => p.Text));
        Assert.Equal([OtherAssembly], Assert.Single(temp.Settings().AssemblyLists).Assemblies.Select(static p => p.Text));
    }

    private static AppSettings SettingsWith(TempDir temp, params AssemblyListRecord[] lists)
    {
        var settings = temp.Settings();
        settings.AssemblyLists = lists.ToList();
        return settings;
    }

    /// <summary>Starts over <paramref name="settings"/> with the command line of <paramref name="args"/>
    /// the way the app does, and opens the list and the files it names.</summary>
    private static AppStateDriver Start(AppSettings settings, params string[] args)
    {
        var arguments = CommandLines.Parse(args).Arguments;
        var d = AppStateDriver.Started(new SettingsLoadResult(settings, new SettingsProblem.None()), arguments);
        d.Start((a, ct) => AppFrames.OpenStartupAssembliesAsync(a, arguments.Files, ct));
        d.RunUntil(() => AreListed(d.AppState, arguments.Files.Paths));
        d.Settle();
        return d;
    }

    private static bool AreListed(AppState appState, List<AssemblyPath> paths)
    {
        return paths.All(path => AssemblyListing.IsListed(appState.Assemblies, path));
    }
}
