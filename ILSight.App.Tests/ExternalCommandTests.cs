using ZGF.Gui.Immediate;
using ILSight.Core;
using static ILSight.ExternalCommands;
using static ILSight.Settings;
using static ILSight.App.Tests.TestPaths;

namespace ILSight.App.Tests;

public sealed class ExternalCommandTests
{
    private const string Other = "Other";
    private static readonly string OtherAssembly = typeof(AppState).Assembly.Location;

    [Fact]
    public void Options_are_read_apart_from_the_files_to_open()
    {
        var command = ExternalCommand.Parse(["--language=il", "--list= Work ", "--clear-list", "a.dll"]);

        Assert.Equal(DecompilationLanguage.IL, command.Language);
        Assert.Equal("Work", command.AssemblyList);
        Assert.True(command.ClearList);
        Assert.Equal([Path.GetFullPath("a.dll")], command.AssemblyPaths.Select(static p => p.Text));
    }

    [Fact]
    public void What_to_navigate_to_and_a_new_window_are_read_from_their_options()
    {
        var command = ExternalCommand.Parse(["--navigateto= T:Some.Type ", "--newinstance"]);

        Assert.Equal("T:Some.Type", command.NavigateTo);
        Assert.True(command.NewInstance);
        Assert.Empty(command.AssemblyPaths);
    }

    [Fact]
    public void A_command_line_is_handed_over_with_its_files_made_absolute_and_reads_back_the_same()
    {
        var command = ExternalCommand.Parse(["--language=IL", "--list=Work", "--clear-list", "--navigateto=T:Some.Type", "--gl", "--newinstance", "a.dll"]);

        var handed = ExternalCommand.Parse(command.ListHandedArguments().ToArray());

        Assert.Equal(DecompilationLanguage.IL, handed.Language);
        Assert.Equal("Work", handed.AssemblyList);
        Assert.True(handed.ClearList);
        Assert.Equal("T:Some.Type", handed.NavigateTo);
        Assert.False(handed.NewInstance);
        Assert.Equal([Path.GetFullPath("a.dll")], handed.AssemblyPaths.Select(static p => p.Text));
    }

    [Fact]
    public void A_command_line_naming_a_member_opens_its_files_and_shows_the_member()
    {
        var d = new AppStateDriver();

        OpenCommandFiles(d.AppState, ExternalCommand.Parse(["--navigateto=M:ILSight.App.Tests.Fixtures.Greeter.Greet(System.Int32)", AppStateDriver.FixturePath]).Files);
        d.RunUntil(() => d.AppState.Tabs.Active?.Node != null);
        d.Settle();

        Assert.Equal("Greet(int) : string", d.AppState.Tabs.Active!.Node!.Text);
        Assert.Empty(d.AppState.Errors.Entries);
    }

    [Fact]
    public void A_command_line_naming_what_its_files_lack_reports_it()
    {
        var d = new AppStateDriver();

        OpenCommandFiles(d.AppState, ExternalCommand.Parse(["--navigateto=T:Nowhere.Missing", AppStateDriver.FixturePath]).Files);
        d.RunUntil(() => d.AppState.Errors.Entries.Count > 0);

        Assert.Contains("T:Nowhere.Missing", Assert.Single(d.AppState.Errors.Entries).Words().Title);
    }

    [Fact]
    public void A_language_the_app_does_not_have_and_blank_values_are_ignored_as_given()
    {
        var command = ExternalCommand.Parse(["--language=VB", "--list=  ", "--navigateto=", "--settings= "]);

        Assert.Null(command.Language);
        Assert.Null(command.AssemblyList);
        Assert.Null(command.NavigateTo);
        Assert.Equal(AppSettings.DefaultPath, command.SettingsPath);
        Assert.Equal(["--language=VB", "--list=  ", "--navigateto=", "--settings= "], command.Ignored);
    }

    [Fact]
    public void An_option_the_app_does_not_have_and_a_file_that_is_no_path_are_ignored_as_given()
    {
        var command = ExternalCommand.Parse(["--opengl", "--GL", "a\0.dll", "a.dll"]);

        Assert.Equal(RenderBackendKind.Auto, command.RenderBackend);
        Assert.Equal([Path.GetFullPath("a.dll")], command.AssemblyPaths.Select(static p => p.Text));
        Assert.Equal(["--opengl", "--GL", "a\0.dll"], command.Ignored);
    }

    [Fact]
    public void The_arguments_ignored_are_handed_over_for_the_open_window_to_tell_of()
    {
        var command = ExternalCommand.Parse(["--language=VB", "--opengl", "a.dll"]);

        var handed = ExternalCommand.Parse(command.ListHandedArguments().ToArray());

        Assert.Equal(["--language=VB", "--opengl"], handed.Ignored);
        Assert.Equal([Path.GetFullPath("a.dll")], handed.AssemblyPaths.Select(static p => p.Text));
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
        Assert.Empty(d.AppState.Assemblies.FilePaths);
        Assert.Equal([AppStateDriver.FixturePath], d.AppState.Lists.PathsOf(SavedAssemblyLists.DefaultName).Select(static p => p.Text));
    }

    [Fact]
    public void A_list_named_in_any_case_opens_with_its_assemblies_and_the_files_named()
    {
        using var temp = new TempDir();
        var settings = SettingsWith(temp,
            new AssemblyListRecord(SavedAssemblyLists.DefaultName, []),
            new AssemblyListRecord(Other, [PathOf(AppStateDriver.FixturePath)]));

        var d = Start(settings, "--list=other", OtherAssembly);
        SaveSettings(d.AppState);

        AssemblyPath[] opened = [PathOf(AppStateDriver.FixturePath), PathOf(OtherAssembly)];
        Assert.Equal(Other, d.AppState.Lists.Active);
        Assert.Equal(opened.OrderBy(static p => p.Text), d.AppState.Assemblies.FilePaths.OrderBy(static p => p.Text));
        Assert.Equal(Other, temp.Settings().ActiveAssemblyList);
    }

    [Fact]
    public void Clearing_the_list_opens_only_the_files_named_and_saves_the_list_with_them()
    {
        using var temp = new TempDir();
        var d = Start(SettingsWith(temp, new AssemblyListRecord(SavedAssemblyLists.DefaultName, [PathOf(AppStateDriver.FixturePath)])), "--clear-list", OtherAssembly);
        SaveSettings(d.AppState);

        Assert.Equal([OtherAssembly], d.AppState.Assemblies.FilePaths.Select(static p => p.Text));
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
        var command = ExternalCommand.Parse(args);
        var d = AppStateDriver.Started(new SettingsLoadResult(settings, new SettingsProblem.None()), command);
        ILSightWindow.OpenStartupAssemblies(d.AppState, command.Files);
        d.RunUntil(() => AreListed(d.AppState, command.AssemblyPaths));
        d.Settle();
        return d;
    }

    private static bool AreListed(AppState appState, List<AssemblyPath> paths) => paths.All(appState.Assemblies.IsListed);
}
