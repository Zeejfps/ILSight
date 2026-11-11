using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using ZGF.Desktop;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.Localization;
using static ILSight.Settings;
using static ILSight.ScreenLayout;
using static ILSight.App.Tests.TestPaths;

namespace ILSight.App.Tests;

public sealed class BrowseTreeTests
{
    [Fact]
    public void A_node_whose_children_are_not_built_shows_a_loading_row_until_they_are()
    {
        var d = new AppStateDriver();
        var tree = new BrowseTree { Work = new BackgroundWork(), Session = TestSession.Shared, ReportError = static _ => { } };
        var root = new AssemblyList().Open(AppStateDriver.FixturePath).Root;

        var first = tree.GetChildren(root);

        Assert.Empty(first.Nodes);
        var loading = Assert.IsType<TreeRow>(first.PlaceholderRow);
        Assert.Equal("Loading…", loading.Label);
        Assert.True(loading.Spinning);
        d.RunUntil(() => tree.GetChildren(root).PlaceholderRow is null);
        Assert.Contains(tree.GetChildren(root).Nodes, c => c.Text == AppStateDriver.FixturesNamespace);
    }

    [Fact]
    public void Children_that_fail_to_build_show_a_row_saying_why()
    {
        using var temp = new TempDir();
        var d = new AppStateDriver();
        var tree = new BrowseTree { Work = new BackgroundWork(), Session = TestSession.Shared, ReportError = static _ => { } };
        var root = new AssemblyList().Open(BrokenAssemblies.WriteUnreadableAvaloniaContainer(temp)).Root;
        var container = root.Child("Resources", BrokenAssemblies.AvaloniaContainer);

        tree.GetChildren(container);
        d.RunUntil(() => container.ChildrenState is ChildrenState.Failed);

        var children = tree.GetChildren(container);
        var failed = Assert.IsType<TreeRow>(children.PlaceholderRow);
        Assert.Empty(children.Nodes);
        Assert.StartsWith("Error: ", failed.Label);
        Assert.Contains("unknown version 9", failed.Label);
        Assert.False(failed.Spinning);
        Assert.True(tree.HasChildren(container));
    }

    [Fact]
    public void An_assembly_spins_while_it_is_reopened()
    {
        var tree = new BrowseTree { Work = new BackgroundWork(), Session = TestSession.Shared, ReportError = static _ => { } };
        var assembly = new AssemblyList().Open(AppStateDriver.FixturePath);
        var reopens = new Reopens();
        Trees.ShowReopening(tree, reopens.Files);
        var idle = tree.Row(assembly.Root).Spinning;

        Reloads.StartReopen(reopens, assembly.FilePath);

        Assert.False(idle);
        Assert.True(tree.Row(assembly.Root).Spinning);
        Assert.False(tree.Row(assembly.Root.Child(AppStateDriver.FixturesNamespace)).Spinning);
    }

    [Fact]
    public void Revealing_a_hidden_non_public_node_selects_its_nearest_shown_ancestor()
    {
        var d = new AppStateDriver();
        var tree = new BrowseTree { Work = new BackgroundWork(), Session = TestSession.Shared, ReportError = static _ => { } };
        var root = new AssemblyList().Open(AppStateDriver.FixturePath).Root;
        var ns = root.Child(AppStateDriver.FixturesNamespace);
        var secret = ns.Child("Hidden", "Secret() : int");
        Trees.ShowApi(tree, ApiVisibility.PublicOnly);

        Trees.RevealNode(tree, secret);
        Assert.Same(ns, tree.View.Selected);

        Trees.ShowApi(tree, ApiVisibility.AllButCompilerGenerated);
        Trees.RevealNode(tree, secret);
        Assert.Same(secret, tree.View.Selected);
    }

    [Fact]
    public void Compiler_generated_members_show_only_when_everything_is_shown()
    {
        var tree = new BrowseTree { Work = new BackgroundWork(), Session = TestSession.Shared, ReportError = static _ => { } };
        var root = new AssemblyList().Open(AppStateDriver.FixturePath).Root;
        var counter = root.Child(AppStateDriver.FixturesNamespace, "Counter");
        var backingField = counter.Child("<Count>k__BackingField : int");

        Assert.True(backingField.IsCompilerGenerated);
        Assert.DoesNotContain(backingField, tree.GetChildren(counter).Nodes);

        Trees.ShowApi(tree, ApiVisibility.All);

        Assert.Contains(backingField, tree.GetChildren(counter).Nodes);
    }

    [Fact]
    public void The_public_children_cache_does_not_keep_closed_assemblies_alive()
    {
        var tree = new BrowseTree { Work = new BackgroundWork(), Session = TestSession.Shared, ReportError = static _ => { } };
        Trees.ShowApi(tree, ApiVisibility.PublicOnly);
        var assembly = FilterAndClose(tree);

        for (var i = 0; i < 100 && assembly.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Thread.Sleep(20);
        }

        Assert.False(assembly.IsAlive);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference FilterAndClose(BrowseTree tree)
    {
        var list = new AssemblyList();
        var assembly = list.Open(AppStateDriver.FixturePath);
        var root = assembly.Root;
        _ = root.Children;
        Assert.NotEmpty(tree.GetChildren(root).Nodes);
        AssemblyListing.CloseAssembly(list, assembly);
        return new WeakReference(assembly);
    }
}

public sealed class AppSettingsTests : IDisposable
{
    private static readonly string A = Rooted("a.dll");
    private static readonly string B = Rooted("b.dll");
    private static readonly string AInJson = JsonEncodedText.Encode(A).Value;
    private static readonly string BInJson = JsonEncodedText.Encode(B).Value;

    private readonly TempDir _temp = new();

    private string SettingsFile => _temp.PathOf("settings.json");

    public void Dispose()
    {
        _temp.Dispose();
    }

    private string Written(string json)
    {
        return _temp.Write("settings.json", json);
    }

    [Fact]
    public void Saved_settings_load_back_and_leave_no_temporary_file()
    {
        var settings = SettingsJson.LoadSettings(FullFilePath.Of(SettingsFile)).Settings;
        settings.AssemblyLists = [new AssemblyListRecord("Work", [FakePath("a.dll"), FakePath("b.dll")]), new AssemblyListRecord("Play", [FakePath("c.dll")])];
        settings.ActiveAssemblyList = "Play";
        settings.Language = DecompilationLanguage.IL;
        settings.Decompiler = DecompilerToggles.SetToggle(new DecompilerOptions { LanguageVersion = CSharpVersion.CSharp7_3 }, DecompilerToggles.FindToggle("AlwaysUseBraces")!, false);
        settings.FoldedOptionGroups = ["Other"];

        Assert.IsType<SettingsWriteResult.Written>(SettingsJson.SaveSettings(settings));

        var (loaded, problem) = SettingsJson.LoadSettings(FullFilePath.Of(SettingsFile));
        Assert.IsType<SettingsProblem.None>(problem);
        Assert.Equal(["Work", "Play"], loaded.AssemblyLists.Select(static l => l.Name));
        Assert.Equal([FakePath("a.dll"), FakePath("b.dll")], loaded.AssemblyLists[0].Assemblies);
        Assert.Equal("Play", loaded.ActiveAssemblyList);
        Assert.Equal(DecompilationLanguage.IL, loaded.Language);
        Assert.Equal(settings.Decompiler, loaded.Decompiler);
        Assert.Equal(["Other"], loaded.FoldedOptionGroups);
        Assert.Equal([SettingsFile], Directory.GetFiles(_temp.Path));
    }

    [Theory]
    [InlineData("""{ "Decompiler": { "LanguageVersion": "CSharp7" } }""", CSharpVersion.CSharp7)]
    [InlineData("""{ "Decompiler": { "LanguageVersion": "CSharp14", "Toggles": null } }""", CSharpVersion.CSharp14)]
    [InlineData("""{ "Decompiler": { } }""", CSharpVersion.CSharp14)]
    public void Decompiler_options_from_older_settings_files_load_with_defaults_for_what_they_lack(string json, CSharpVersion version)
    {
        var (loaded, problem) = SettingsJson.LoadSettings(FullFilePath.Of(Written(json)));

        Assert.IsType<SettingsProblem.None>(problem);
        Assert.Equal(DecompilerOptions.Default with { LanguageVersion = version }, loaded.Decompiler);
        Assert.False(loaded.Decompiler.HasChanges);
    }

    [Fact]
    public void A_decompiler_toggle_the_decompiler_lacks_is_reported_and_left_out()
    {
        var (loaded, problem) = SettingsJson.LoadSettings(FullFilePath.Of(Written("""{ "Decompiler": { "Toggles": { "NoSuchToggle": true, "AlwaysUseBraces": false } } }""")));

        Assert.Equal(["Decompiler"], Assert.IsType<SettingsProblem.ValuesLeftOut>(problem).Fields);
        Assert.Equal(DecompilerToggles.SetToggle(DecompilerOptions.Default, DecompilerToggles.FindToggle("AlwaysUseBraces")!, false), loaded.Decompiler);
    }

    [Fact]
    public void A_decompiler_toggle_saved_at_its_default_loads_as_not_set()
    {
        var braces = DecompilerToggles.FindToggle("AlwaysUseBraces")!;
        var (loaded, problem) = SettingsJson.LoadSettings(FullFilePath.Of(Written($$"""{ "Decompiler": { "Toggles": { "AlwaysUseBraces": {{(braces.DefaultValue ? "true" : "false")}} } } }""")));

        Assert.IsType<SettingsProblem.None>(problem);
        Assert.False(DecompilerToggles.IsChanged(loaded.Decompiler, braces));
        Assert.Equal(DecompilerOptions.Default, loaded.Decompiler);
    }

    [Fact]
    public void Window_bounds_round_trip_and_open_the_window_where_it_was_left()
    {
        var settings = SettingsJson.LoadSettings(FullFilePath.Of(SettingsFile)).Settings;
        settings.Window = new WindowBounds(-1200, 80, 1600, 1000, Maximized: true);
        Assert.IsType<SettingsWriteResult.Written>(SettingsJson.SaveSettings(settings));

        var loaded = SettingsJson.LoadSettings(FullFilePath.Of(SettingsFile)).Settings;
        var config = loaded.Window!.Value.ApplyTo(new StartupConfig { WindowWidth = 1400, WindowHeight = 900, WindowTitle = "ILSight" });

        Assert.Equal(new WindowBounds(-1200, 80, 1600, 1000, true), loaded.Window);
        Assert.Equal((-1200, 80, 1600, 1000, true), (config.WindowX, config.WindowY, config.WindowWidth, config.WindowHeight, config.StartMaximized));
        Assert.Null(SettingsJson.LoadSettings(FullFilePath.Of(_temp.PathOf("missing.json"))).Settings.Window);
    }

    [Theory]
    [InlineData(10, 20, AppSettings.MinWindowWidth, AppSettings.MinWindowHeight)]
    [InlineData(100_000, 90_000, AppSettings.MaxWindowSize, AppSettings.MaxWindowSize)]
    [InlineData(1200, 800, 1200, 800)]
    public void Saved_window_sizes_load_within_reason(int width, int height, int expectedWidth, int expectedHeight)
    {
        var loaded = SettingsJson.LoadSettings(FullFilePath.Of(Written($$"""{ "Window": { "X": 5, "Y": 6, "Width": {{width}}, "Height": {{height}}, "Maximized": false } }"""))).Settings;

        Assert.Equal(new WindowBounds(5, 6, expectedWidth, expectedHeight, false), loaded.Window);
    }

    [Fact]
    public void The_export_window_reopens_where_it_was_left_in_the_last_run()
    {
        var d = new AppStateDriver(SettingsJson.LoadSettings(FullFilePath.Of(SettingsFile)).Settings);
        d.Ui.ExportSetup.Window.Bounds = new WindowBounds(-900, 200, 700, 800, false);
        DialogWindows.Settle(d.Ui.ExportSetup.Window, held: false);
        d.NextFrame();
        SaveSettings(d.AppState);

        var next = new AppStateDriver(SettingsJson.LoadSettings(FullFilePath.Of(SettingsFile)).Settings);

        Assert.Equal(new WindowBounds(-900, 200, 700, 800, false), next.Ui.ExportSetup.Window.Bounds);
        Assert.Null(SettingsJson.LoadSettings(FullFilePath.Of(Written("""{ "ExportWindow": { "X": 5, "Y": 6, "Width": 0, "Height": 0, "Maximized": false } }"""))).Settings.ExportWindow);
    }

    [Fact]
    public void Window_bounds_without_a_size_load_as_none()
    {
        Assert.Null(SettingsJson.LoadSettings(FullFilePath.Of(Written("""{ "Window": { "X": 5, "Y": 6, "Width": 0, "Height": 0, "Maximized": false } }"""))).Settings.Window);
        Assert.Null(SettingsJson.SanitizeWindow(default(WindowBounds)));
    }

    [Fact]
    public void Null_assembly_lists_load_as_none()
    {
        var (loaded, problem) = SettingsJson.LoadSettings(FullFilePath.Of(Written("""{ "AssemblyLists": null, "Theme": "Monokai" }""")));

        Assert.IsType<SettingsProblem.None>(problem);
        Assert.Empty(loaded.AssemblyLists);
        Assert.Equal("monokai", loaded.Theme.Text);
    }

    [Fact]
    public void Lists_missing_their_name_or_assemblies_load_without_them()
    {
        var (loaded, problem) = SettingsJson.LoadSettings(FullFilePath.Of(Written($$"""{ "AssemblyLists": [ null, { "Name": null }, { "Name": "Work", "Assemblies": null }, { "Name": "Play", "Assemblies": [ " ", "a.dll", "{{AInJson}}" ] } ] }""")));

        Assert.IsType<SettingsProblem.None>(problem);
        Assert.Equal(["Work", "Play"], loaded.AssemblyLists.Select(static l => l.Name));
        Assert.Empty(loaded.AssemblyLists[0].Assemblies);
        Assert.Equal([A], loaded.AssemblyLists[1].Assemblies.Select(static p => p.Text));
    }

    [Fact]
    public void The_one_list_of_an_older_settings_file_becomes_the_default_list_and_is_saved_only_there()
    {
        var loaded = SettingsJson.LoadSettings(FullFilePath.Of(Written($$"""{ "Assemblies": [ "{{AInJson}}", "", "{{BInJson}}" ] }"""))).Settings;

        var list = Assert.Single(loaded.AssemblyLists);
        Assert.Equal(SavedAssemblyLists.DefaultName, list.Name);
        Assert.Equal([A, B], list.Assemblies.Select(static p => p.Text));
        Assert.IsType<SettingsWriteResult.Written>(SettingsJson.SaveSettings(loaded));
        Assert.False(JsonDocument.Parse(File.ReadAllText(SettingsFile)).RootElement.TryGetProperty("Assemblies", out _));
    }

    [Fact]
    public void The_analyses_of_an_older_settings_file_go_to_the_open_list_and_are_saved_only_there()
    {
        var loaded = SettingsJson.LoadSettings(FullFilePath.Of(Written($$"""
            { "AssemblyLists": [ { "Name": "Work", "Assemblies": [] }, { "Name": "Play", "Assemblies": [] } ],
              "ActiveAssemblyList": "Play",
              "Analyzed": [ { "Kind": "Definition", "FilePath": "{{AInJson}}", "Token": 33554434 } ] }
            """))).Settings;

        Assert.Empty(loaded.AssemblyLists[0].Analyzed!);
        var analyzed = Assert.Single(loaded.AssemblyLists[1].Analyzed!);
        Assert.Equal(new CodeAnchor(CodeAnchorKind.Definition, PathOf(A), 33554434), analyzed);
        Assert.IsType<SettingsWriteResult.Written>(SettingsJson.SaveSettings(loaded));
        Assert.False(JsonDocument.Parse(File.ReadAllText(SettingsFile)).RootElement.TryGetProperty("Analyzed", out _));
    }

    [Theory]
    [InlineData(false, ApiVisibility.PublicOnly)]
    [InlineData(true, ApiVisibility.AllButCompilerGenerated)]
    public void The_non_public_switch_of_an_older_settings_file_becomes_its_visibility_and_is_saved_only_as_that(bool showNonPublic, ApiVisibility expected)
    {
        var loaded = SettingsJson.LoadSettings(FullFilePath.Of(Written($$"""{ "ShowNonPublic": {{(showNonPublic ? "true" : "false")}} }"""))).Settings;

        Assert.Equal(expected, loaded.ApiVisibility);
        Assert.IsType<SettingsWriteResult.Written>(SettingsJson.SaveSettings(loaded));
        Assert.False(JsonDocument.Parse(File.ReadAllText(SettingsFile)).RootElement.TryGetProperty("ShowNonPublic", out _));
    }

    [Fact]
    public void An_unreadable_file_falls_back_to_defaults_and_is_reported()
    {
        var loadResult = SettingsJson.LoadSettings(FullFilePath.Of(Written("""{ "Assemblies": [ "a.dll", """)));
        var (loaded, problem) = loadResult;
        var appState = ILSightAppLauncher.LoadAppState(loadResult, CommandLines.Parse([]).Arguments, TestSession.Shared);

        Assert.Empty(loaded.AssemblyLists);
        Assert.IsType<SettingsProblem.NotJson>(problem);
        var error = Assert.Single(appState.Errors.Entries);
        Assert.Contains(SettingsFile, error.Words().Details);
    }

    [Fact]
    public void An_unreadable_file_is_copied_beside_it_before_the_settings_are_saved_over_it()
    {
        const string broken = """{ "Assemblies": [ "a.dll", """;
        var loadResult = SettingsJson.LoadSettings(FullFilePath.Of(Written(broken)));
        var (loaded, problem) = loadResult;
        var appState = ILSightAppLauncher.LoadAppState(loadResult, CommandLines.Parse([]).Arguments, TestSession.Shared);

        Assert.IsType<SettingsWriteResult.Written>(SettingsJson.SaveSettings(loaded));

        var copy = _temp.PathOf("settings.unreadable.json");
        Assert.Equal(new SettingsKept.CopiedTo(copy), Assert.IsType<SettingsProblem.NotJson>(problem).Kept);
        Assert.Equal(broken, File.ReadAllText(copy));
        Assert.Contains(copy, Assert.Single(appState.Errors.Entries).Words().Details);
        Assert.Equal(JsonValueKind.Object, JsonDocument.Parse(File.ReadAllText(SettingsFile)).RootElement.ValueKind);
    }

    [Fact]
    public void A_file_that_cannot_be_read_or_copied_is_not_saved_over()
    {
        const string held = """{ "Theme": "light" }""";
        var path = Written(held);
        SettingsLoadResult loadResult;
        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            loadResult = SettingsJson.LoadSettings(FullFilePath.Of(path));
        }

        Assert.IsType<SettingsWriteResult.Written>(SettingsJson.SaveSettings(loadResult.Settings));

        Assert.IsType<SettingsKept.NotSaved>(Assert.IsType<SettingsProblem.NotRead>(loadResult.Problem).Kept);
        Assert.Equal(held, File.ReadAllText(path));
    }

    [Fact]
    public void A_number_where_the_settings_name_a_language_is_refused()
    {
        var (loaded, problem) = SettingsJson.LoadSettings(FullFilePath.Of(Written("""{ "Locale": 42 }""")));

        Assert.Equal(["Locale"], Assert.IsType<SettingsProblem.ValuesLeftOut>(problem).Fields);
        Assert.Null(loaded.Locale);
    }

    [Fact]
    public void A_value_the_file_holds_wrong_leaves_out_that_value_alone_and_the_file_is_kept()
    {
        var json = $$"""
            { "Theme": "light", "Language": "Klingon", "Window": "wide",
              "AssemblyLists": [ { "Name": "Work", "Assemblies": [ "{{AInJson}}" ] } ] }
            """;
        var loadResult = SettingsJson.LoadSettings(FullFilePath.Of(Written(json)));
        var (loaded, problem) = loadResult;
        var appState = ILSightAppLauncher.LoadAppState(loadResult, CommandLines.Parse([]).Arguments, TestSession.Shared);

        var leftOut = Assert.IsType<SettingsProblem.ValuesLeftOut>(problem);
        Assert.Equal(["Language", "Window"], leftOut.Fields);
        Assert.Equal("light", loaded.Theme.Text);
        Assert.Equal(DecompilationLanguage.CSharp, loaded.Language);
        Assert.Null(loaded.Window);
        Assert.Equal([A], Assert.Single(loaded.AssemblyLists).Assemblies.Select(static p => p.Text));
        var copy = _temp.PathOf("settings.unreadable.json");
        Assert.Equal(new SettingsKept.CopiedTo(copy), leftOut.Kept);
        Assert.Equal(json, File.ReadAllText(copy));
        var details = Assert.Single(appState.Errors.Entries).Words().Details;
        Assert.Contains("Language, Window", details);
        Assert.Contains(copy, details);
    }

    [Fact]
    public void A_list_or_tab_the_file_holds_wrong_leaves_out_that_one_alone()
    {
        var (loaded, problem) = SettingsJson.LoadSettings(FullFilePath.Of(Written($$"""
            { "AssemblyLists": [
                { "Name": "Broken", "Assemblies": 5 },
                { "Name": "Work", "Assemblies": [ "{{AInJson}}" ], "Documents": { "Tabs": [
                    { "kind": "nothing", "Panel": "document-1" },
                    { "kind": "comparison", "Panel": "document-2", "LeftPath": "{{AInJson}}", "RightPath": "{{BInJson}}" } ] } } ] }
            """)));

        Assert.Equal(["AssemblyLists"], Assert.IsType<SettingsProblem.ValuesLeftOut>(problem).Fields);
        var list = Assert.Single(loaded.AssemblyLists);
        Assert.Equal("Work", list.Name);
        Assert.Equal("document-2", Assert.Single(list.Documents!.Tabs).Panel);
    }

    [Fact]
    public void Display_options_round_trip_and_a_font_missing_its_file_is_left_out_alone()
    {
        var settings = SettingsJson.LoadSettings(FullFilePath.Of(SettingsFile)).Settings;
        settings.Display = new DisplayOptions(CodeFontSize.Clamp(14f), false, true, false, true, false, 1.25f, new CodeFont("Mono", "/mono.ttf", 1),
            TokenFormat.Hex, true, true, true, false);
        Assert.IsType<SettingsWriteResult.Written>(SettingsJson.SaveSettings(settings));

        Assert.Equal(settings.Display, SettingsJson.LoadSettings(FullFilePath.Of(SettingsFile)).Settings.Display);
        var (loaded, problem) = SettingsJson.LoadSettings(FullFilePath.Of(Written("""{ "Display": { "ShowLineNumbers": false, "Font": { "Family": "Mono", "Path": null } } }""")));
        Assert.IsType<SettingsProblem.None>(problem);
        Assert.Equal(DisplayOptions.Default with { ShowLineNumbers = false }, loaded.Display);
    }

    [Theory]
    [InlineData("""{ "Display": { "FontSize": 900, "UiScale": 1.3 } }""", CodeFontSize.Max, 1.25f)]
    [InlineData("""{ "Display": { "FontSize": 1, "UiScale": 0.01 } }""", CodeFontSize.Min, 0.75f)]
    public void A_saved_font_size_out_of_range_and_a_ui_scale_not_offered_load_within_what_is(string json, float fontSize, float uiScale)
    {
        var (loaded, problem) = SettingsJson.LoadSettings(FullFilePath.Of(Written(json)));

        Assert.IsType<SettingsProblem.None>(problem);
        Assert.Equal(fontSize, loaded.Display!.FontSize?.Points);
        Assert.Equal(uiScale, loaded.Display.UiScale);
    }

    [Fact]
    public void A_list_whose_documents_layout_is_missing_a_part_keeps_its_tabs_without_it_and_is_reported()
    {
        var (loaded, problem) = SettingsJson.LoadSettings(FullFilePath.Of(Written($$"""
            { "AssemblyLists": [ { "Name": "Work", "Assemblies": [], "Documents": {
                "Tabs": [ { "kind": "comparison", "Panel": "document-1", "LeftPath": "{{AInJson}}", "RightPath": "{{BInJson}}" } ],
                "Layout": { "Root": { "kind": "group", "Panels": [ null ] } } } } ] }
            """)));

        Assert.Equal(["AssemblyLists"], Assert.IsType<SettingsProblem.ValuesLeftOut>(problem).Fields);
        var documents = Assert.Single(loaded.AssemblyLists).Documents!;
        Assert.Single(documents.Tabs);
        Assert.Null(documents.Layout);
    }

    [Fact]
    public void Filters_and_bookmark_views_load_without_the_parts_they_hold_as_null()
    {
        var (loaded, problem) = SettingsJson.LoadSettings(FullFilePath.Of(Written($$"""
            { "FlagsFilters": { "TypeDef/Attributes": { "Groups": { "Visibility": null, "Layout": [ 8 ] }, "Mode": "Any" }, "Gone": null },
              "Bookmarks": [ { "Name": "B", "FileName": "{{AInJson}}",
                "ViewState": { "ExpandedFoldings": [ null, { "Start": 1, "End": 2 } ], "SelectedTreeNodePath": [ "{{AInJson}}", null ] } } ] }
            """)));

        Assert.IsType<SettingsProblem.None>(problem);
        var filter = Assert.Single(loaded.FlagsFilters!).Value;
        Assert.Equal(["Layout"], filter.Groups!.Keys);
        var view = Assert.Single(loaded.Bookmarks).ViewState!;
        Assert.Equal([new BookmarkFoldingRange(1, 2)], view.ExpandedFoldings!);
        Assert.Null(view.SelectedTreeNodePath);
    }

    [Fact]
    public void Saved_values_missing_what_they_name_load_without_them()
    {
        var (loaded, problem) = SettingsJson.LoadSettings(FullFilePath.Of(Written($$"""
            { "AssemblyLists": [ { "Name": "Work", "Assemblies": [ null, "{{AInJson}}" ],
                "Analyzed": [ { "Kind": "Definition", "FilePath": null, "Token": 1 } ],
                "Documents": { "Tabs": [
                    { "kind": "node", "Panel": "document-1", "FilePath": null, "Path": { "Version": 1, "Steps": [] } },
                    { "kind": "node", "Panel": "document-2", "FilePath": "{{AInJson}}", "Path": null },
                    { "kind": "diagram", "Panel": "document-3", "Types": [ { "AssemblyPath": "{{AInJson}}", "FullName": null } ] } ] } } ],
              "Bookmarks": [ { "Name": null, "FileName": "{{AInJson}}" }, { "Name": "Gone", "FileName": null } ],
              "NuGet": { "FeedUrl": null },
              "FoldedOptionGroups": [ null, "Other" ] }
            """)));

        Assert.IsType<SettingsProblem.None>(problem);
        var list = Assert.Single(loaded.AssemblyLists);
        Assert.Equal([A], list.Assemblies.Select(static p => p.Text));
        Assert.Empty(list.Analyzed!);
        var diagram = Assert.IsType<DiagramDocumentRecord>(Assert.Single(list.Documents!.Tabs));
        Assert.Empty(diagram.Types);
        var bookmark = Assert.Single(loaded.Bookmarks);
        Assert.Equal(("", "a.dll", ""), (bookmark.Name, bookmark.ModuleName, bookmark.MemberName));
        Assert.Null(loaded.NuGet);
        Assert.Equal(["Other"], loaded.FoldedOptionGroups);
    }

    [Fact]
    public void Every_enum_the_settings_hold_is_read_by_name_alone()
    {
        var enums = typeof(AppSettingsJson).GetProperties()
            .Select(static p => p.PropertyType)
            .Where(static t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(JsonTypeInfo<>))
            .Select(static t => t.GetGenericArguments()[0])
            .Where(static t => t.IsEnum)
            .ToList();

        Assert.NotEmpty(enums);
        Assert.All(enums, type => Assert.Throws<JsonException>(() => JsonSerializer.Deserialize("1", type, AppSettingsJson.Default)));
    }

    [Fact]
    public void A_file_holding_null_falls_back_to_defaults_and_is_reported()
    {
        var (_, problem) = SettingsJson.LoadSettings(FullFilePath.Of(Written("null")));

        Assert.IsType<SettingsProblem.Empty>(problem);
    }

    [Theory]
    [InlineData("""{ "Layout": { "Root": null } }""")]
    [InlineData("""{ "Layout": { "Root": { "kind": "group", "Panels": null } } }""")]
    [InlineData("""{ "Layout": { "Root": { "kind": "group", "Panels": [ null ] } } }""")]
    [InlineData("""{ "Layout": { "Root": { "kind": "split", "First": null } } }""")]
    [InlineData("""{ "Layout": { "Windows": null } }""")]
    [InlineData("""{ "Layout": { "Windows": [ null ] } }""")]
    [InlineData("""{ "Layout": { "Windows": [ { "Root": null } ] } }""")]
    public void A_layout_missing_parts_is_left_out_for_the_panels_to_start_where_they_do(string json)
    {
        var loadResult = SettingsJson.LoadSettings(FullFilePath.Of(Written($$"""{ "Theme": "light", {{json[1..]}}""")));
        var (loaded, problem) = loadResult;
        var d = AppStateDriver.Started(loadResult, CommandLines.Parse([]).Arguments);

        Assert.IsType<SettingsProblem.LayoutPartsMissing>(problem);
        Assert.Equal("light", loaded.Theme.Text);
        var fresh = new AppStateDriver(new AppSettings());
        Assert.Equal(DockLayout.PanelsIn(fresh.Ui.Layout.Root), DockLayout.PanelsIn(d.Ui.Layout.Root));
        Assert.Empty(d.Ui.Layout.Windows);
        Assert.Contains(d.AppState.Errors.Entries, e => e.Words().Details.Contains(SettingsFile));
    }

    [Fact]
    public void A_layout_with_two_parts_under_one_id_is_left_out()
    {
        var (loaded, problem) = SettingsJson.LoadSettings(FullFilePath.Of(Written("""
            { "Layout": { "Root": { "kind": "split", "Id": 1,
                "First": { "kind": "group", "Id": 2, "Panels": [ "assemblies" ] },
                "Second": { "kind": "group", "Id": 2, "Panels": [ "search" ] } } } }
            """)));

        Assert.IsType<SettingsProblem.LayoutPartsRepeated>(problem);
        Assert.Null(loaded.Layout);
    }

    [Fact]
    public void A_layout_leaving_out_its_root_has_an_empty_group_there()
    {
        var (loaded, problem) = SettingsJson.LoadSettings(FullFilePath.Of(Written("""{ "Layout": { "NextId": 7 } }""")));

        Assert.IsType<SettingsProblem.None>(problem);
        AssertEmptyGroup(loaded.Layout!.Root);
        Assert.Empty(loaded.Layout.Windows);
        Assert.Equal(7, loaded.Layout.NextId);
    }

    [Fact]
    public void A_layout_leaving_out_everything_is_an_empty_group_numbering_from_one()
    {
        var (loaded, problem) = SettingsJson.LoadSettings(FullFilePath.Of(Written("""{ "Layout": { } }""")));

        Assert.IsType<SettingsProblem.None>(problem);
        AssertEmptyGroup(loaded.Layout!.Root);
        Assert.Empty(loaded.Layout.Windows);
        Assert.Equal(1, loaded.Layout.NextId);
    }

    [Fact]
    public void A_floating_window_leaving_out_its_root_has_an_empty_group_there()
    {
        var (loaded, problem) = SettingsJson.LoadSettings(FullFilePath.Of(Written("""{ "Layout": { "Windows": [ { "Id": 4, "X": 10, "Width": 300 } ] } }""")));

        Assert.IsType<SettingsProblem.None>(problem);
        var window = Assert.Single(loaded.Layout!.Windows);
        Assert.Equal((4, 10f, 300f), (window.Id, window.X, window.Width));
        AssertEmptyGroup(window.Root);
    }

    [Fact]
    public void A_split_leaving_out_its_first_part_has_an_empty_group_there()
    {
        var (loaded, problem) = SettingsJson.LoadSettings(FullFilePath.Of(Written("""
            { "Layout": { "Root": { "kind": "split", "Id": 1, "Ratio": 0.25,
                "Second": { "kind": "group", "Id": 2, "Panels": [ "search" ] } } } }
            """)));

        Assert.IsType<SettingsProblem.None>(problem);
        var split = Assert.IsType<DockSplit>(loaded.Layout!.Root);
        AssertEmptyGroup(split.First);
        Assert.Equal(["search"], Assert.IsType<DockGroup>(split.Second).Panels);
        Assert.Equal(0.25f, split.Ratio);
    }

    [Fact]
    public void A_split_leaving_out_its_second_part_has_an_empty_group_there()
    {
        var (loaded, problem) = SettingsJson.LoadSettings(FullFilePath.Of(Written("""
            { "Layout": { "Root": { "kind": "split", "Id": 1, "Ratio": 0.25,
                "First": { "kind": "group", "Id": 2, "Panels": [ "search" ] } } } }
            """)));

        Assert.IsType<SettingsProblem.None>(problem);
        var split = Assert.IsType<DockSplit>(loaded.Layout!.Root);
        Assert.Equal(["search"], Assert.IsType<DockGroup>(split.First).Panels);
        AssertEmptyGroup(split.Second);
    }

    [Fact]
    public void A_split_leaving_out_both_parts_has_two_empty_groups_which_do_not_count_as_one_id_twice()
    {
        var (loaded, problem) = SettingsJson.LoadSettings(FullFilePath.Of(Written("""{ "Layout": { "Root": { "kind": "split", "Id": 1 } } }""")));

        Assert.IsType<SettingsProblem.None>(problem);
        var split = Assert.IsType<DockSplit>(loaded.Layout!.Root);
        AssertEmptyGroup(split.First);
        AssertEmptyGroup(split.Second);
    }

    [Fact]
    public void A_split_leaving_out_its_ratio_divides_the_space_evenly()
    {
        var (loaded, problem) = SettingsJson.LoadSettings(FullFilePath.Of(Written("""
            { "Layout": { "Root": { "kind": "split", "Id": 1, "Direction": "Vertical",
                "First": { "kind": "group", "Id": 2, "Panels": [ "tree" ] },
                "Second": { "kind": "group", "Id": 3, "Panels": [ "search" ] } } } }
            """)));

        Assert.IsType<SettingsProblem.None>(problem);
        var split = Assert.IsType<DockSplit>(loaded.Layout!.Root);
        Assert.Equal(0.5f, split.Ratio);
        Assert.Equal(Direction.Vertical, split.Direction);
    }

    [Fact]
    public void A_group_leaving_out_its_panels_is_empty_and_keeps_what_else_it_holds()
    {
        var (loaded, problem) = SettingsJson.LoadSettings(FullFilePath.Of(Written("""{ "Layout": { "Root": { "kind": "group", "Id": 3, "Active": "search" } } }""")));

        Assert.IsType<SettingsProblem.None>(problem);
        var group = Assert.IsType<DockGroup>(loaded.Layout!.Root);
        Assert.Empty(group.Panels);
        Assert.Equal((3, "search"), (group.Id, group.Active));
    }

    [Theory]
    [InlineData("""{ "Layout": { "Root": { "Id": 1 } } }""")]
    [InlineData("""{ "Layout": { "Root": { "kind": "nothing", "Id": 1 } } }""")]
    [InlineData("""{ "Layout": { "Root": { "kind": "split", "First": { "Id": 1 } } } }""")]
    [InlineData("""{ "Layout": { "Windows": [ { "Root": { "kind": "nothing" } } ] } }""")]
    public void A_layout_part_of_no_kind_there_is_leaves_out_the_layout(string json)
    {
        var (loaded, problem) = SettingsJson.LoadSettings(FullFilePath.Of(Written(json)));

        Assert.Equal(["Layout"], Assert.IsType<SettingsProblem.ValuesLeftOut>(problem).Fields);
        Assert.Null(loaded.Layout);
    }

    [Fact]
    public void A_tab_of_no_kind_at_all_leaves_out_that_one_alone()
    {
        var (loaded, problem) = SettingsJson.LoadSettings(FullFilePath.Of(Written($$"""
            { "AssemblyLists": [ { "Name": "Work", "Assemblies": [], "Documents": { "Tabs": [
                { "Panel": "document-1" },
                { "kind": "comparison", "Panel": "document-2", "LeftPath": "{{AInJson}}", "RightPath": "{{BInJson}}" } ] } } ] }
            """)));

        Assert.Equal(["AssemblyLists"], Assert.IsType<SettingsProblem.ValuesLeftOut>(problem).Fields);
        Assert.Equal("document-2", Assert.Single(Assert.Single(loaded.AssemblyLists).Documents!.Tabs).Panel);
    }

    private static void AssertEmptyGroup(DockNode node)
    {
        var group = Assert.IsType<DockGroup>(node);
        Assert.Equal(0, group.Id);
        Assert.Empty(group.Panels);
        Assert.Null(group.Active);
    }

    [Fact]
    public void Saving_replaces_the_whole_file()
    {
        var settings = SettingsJson.LoadSettings(FullFilePath.Of(SettingsFile)).Settings;
        settings.AssemblyLists = Enumerable.Range(0, 200).Select(i => new AssemblyListRecord($"list-{i}", [])).ToList();
        SettingsJson.SaveSettings(settings);
        settings.AssemblyLists = [new AssemblyListRecord("one", [])];

        SettingsJson.SaveSettings(settings);

        var json = JsonDocument.Parse(File.ReadAllText(SettingsFile));
        Assert.Equal(1, json.RootElement.GetProperty("AssemblyLists").GetArrayLength());
    }
}

public sealed class DockPanelsTests
{
    [Fact]
    public void A_rearranged_layout_is_saved_and_restored_with_the_settings()
    {
        using var temp = new TempDir();
        var d = new AppStateDriver(temp.Settings());
        var layout = d.Ui.Layout;
        layout.Float(DockPanels.Assemblies, new UiRect(40f, 50f, 300f, 400f));
        d.NextFrame();
        SaveSettings(d.AppState);

        var next = new AppStateDriver(temp.Settings());
        var restored = next.Ui.Layout;

        Assert.Same(next.AppState.Tabs.Active, DocumentPanels.FindTab(next.Ui.Documents, Assert.Single(Assert.IsType<DockGroup>(restored.Root).Panels)));
        var window = Assert.Single(restored.Windows);
        Assert.Equal(new UiRect(40f, 50f, 300f, 400f), window.Rect);
        Assert.Same(window, restored.WindowOf(restored.GroupOf(DockPanels.Assemblies)!));
    }

    [Fact]
    public void A_saved_layout_keeps_the_views_left_open_and_shows_the_first_tab_in_place_of_documents_not_kept()
    {
        var saved = new DockLayout { Root = DockLayout.Group("document-7", DockPanels.Search, "gone", DockPanels.Bookmarks) };

        var d = new AppStateDriver(new AppSettings { Layout = saved });
        var fresh = DockPanels.Restore(null);

        var panel = DocumentPanels.GetPanel(d.Ui.Documents, Assert.Single(d.AppState.Tabs.Open));
        Assert.Equal([panel, DockPanels.Search, DockPanels.Bookmarks], Assert.IsType<DockGroup>(d.Ui.Layout.Root).Panels);
        Assert.NotEqual("document-7", panel);
        Assert.Equal([DockPanels.Assemblies], Assert.IsType<DockGroup>(fresh.Root).Panels);
    }

    [Fact]
    public void Every_view_is_known_by_its_own_name_and_a_document_by_none()
    {
        var views = Enum.GetValues<DockView>();

        Assert.All(views, v => Assert.Equal(v, DockPanels.ViewOf(DockPanels.NameOf(v))));
        Assert.Equal(views.Length, views.Select(DockPanels.NameOf).Distinct().Count());
        Assert.Null(DockPanels.ViewOf("document-1"));
    }

    [Fact]
    public void Opening_a_search_docks_its_panel_and_closing_the_panel_clears_the_query()
    {
        var d = new AppStateDriver();
        var appState = d.AppState;

        OpenSearch(d.Ui);
        d.Ui.Search.Query = "Greeter";

        var search = d.Ui.Layout.GroupOf(DockPanels.Search);
        Assert.NotNull(search);
        Assert.NotSame(search, d.Ui.Layout.GroupOf(DocumentPanels.GetPanel(d.Ui.Documents, appState.Tabs.Active!)));

        d.Ui.Layout.Close(DockPanels.Search);

        Assert.Equal("", d.Ui.Search.Query);
    }

    [Fact]
    public void The_search_mode_is_saved_and_restored()
    {
        using var temp = new TempDir();
        var d = new AppStateDriver(temp.Settings());

        d.Ui.Search.Kinds = SearchKinds.Literals;
        d.NextFrame();
        SaveSettings(d.AppState);

        Assert.Equal(SearchKinds.Literals, new AppStateDriver(temp.Settings()).Ui.Search.Kinds);
    }

    [Fact]
    public void Saving_leaves_the_settings_the_app_started_with_as_they_were()
    {
        using var temp = new TempDir();
        var settings = temp.Settings();
        var d = new AppStateDriver(settings);

        d.Ui.Search.Kinds = SearchKinds.Literals;
        d.AppState.Language = DecompilationLanguage.IL;
        d.NextFrame();
        SaveSettings(d.AppState);

        Assert.Equal(SearchKinds.TypesAndMembers, settings.SearchKinds);
        Assert.Equal(DecompilationLanguage.CSharp, settings.Language);
        var saved = temp.Settings();
        Assert.Equal(SearchKinds.Literals, saved.SearchKinds);
        Assert.Equal(DecompilationLanguage.IL, saved.Language);
    }

    [Fact]
    public void Scoping_the_search_to_a_namespace_keeps_the_terms_typed()
    {
        var driver = AppStateDriver.Opened();
        driver.Ui.Search.Query = "inassembly:Old Greet";

        ScopeSearch(driver.Ui, driver.Node(AppStateDriver.FixturesNamespace));

        Assert.Equal($"innamespace:{AppStateDriver.FixturesNamespace} inassembly:Old Greet ", driver.Ui.Search.Query);
        Assert.True(driver.Ui.Layout.Contains(DockPanels.Search));
    }
}

public sealed class FrameworkPickerTests
{
    [Fact]
    public void A_failed_scan_ends_loading_with_an_error_and_reports_it()
    {
        var posted = PostedActions.OfThisTest();
        var reported = new List<string>();
        var picker = new FrameworkPicker { Work = new BackgroundWork(), ReportError = problem => reported.Add(ProblemTexts.Describe(Strings.En, problem).Title), ListAssemblies = static () => throw new InvalidDataException("no runtime") };

        FrameworkPickerView.Open(picker);
        Poll.Until(() => picker.Scan is not RuntimeScan.Scanning, posted.Run);

        Assert.Contains("no runtime", Assert.IsType<RuntimeScan.Failed>(picker.Scan).Error);
        Assert.Single(reported);
    }

    [Fact]
    public void Filtering_keeps_only_the_picked_out_assemblies_still_shown()
    {
        var posted = PostedActions.OfThisTest();
        var assemblies = new[]
        {
            new RuntimeAssembly("System.Net.Http", "Microsoft.NETCore.App", FakePath("a.dll")),
            new RuntimeAssembly("System.Net.Sockets", "Microsoft.NETCore.App", FakePath("b.dll")),
            new RuntimeAssembly("System.Text.Json", "Microsoft.NETCore.App", FakePath("c.dll")),
        };
        var picker = new FrameworkPicker { Work = new BackgroundWork(), ReportError = static _ => { }, ListAssemblies = () => assemblies };
        var list = new AssemblyList();
        FrameworkPickerView.Open(picker);
        Poll.Until(() => picker.Scan is not RuntimeScan.Scanning, posted.Run);
        FrameworkPickerView.Update(picker, list);

        picker.Selection.SelectAll(picker.Filtered);
        picker.Filter = "Net";
        FrameworkPickerView.Update(picker, list);

        Assert.Equal([assemblies[0], assemblies[1]], FrameworkPickerView.ListSelected(picker));
    }

    [Fact]
    public void Assemblies_already_open_are_not_offered()
    {
        var posted = PostedActions.OfThisTest();
        var open = new RuntimeAssembly("Fixtures", "Microsoft.NETCore.App", PathOf(AppStateDriver.FixturePath));
        var other = new RuntimeAssembly("System.Text.Json", "Microsoft.NETCore.App", FakePath("c.dll"));
        var picker = new FrameworkPicker { Work = new BackgroundWork(), ReportError = static _ => { }, ListAssemblies = () => [open, other] };
        var list = new AssemblyList();
        FrameworkPickerView.Open(picker);
        Poll.Until(() => picker.Scan is not RuntimeScan.Scanning, posted.Run);
        FrameworkPickerView.Update(picker, list);
        Assert.Equal([open, other], picker.Filtered);

        list.Open(AppStateDriver.FixturePath);
        FrameworkPickerView.Update(picker, list);

        Assert.Equal([other], picker.Filtered);
    }

    [Fact]
    public void Only_an_open_tab_can_be_active_or_the_preview()
    {
        var tabs = new DocumentTabs();
        var closed = new DocumentTab();

        Assert.Throws<InvalidOperationException>(() => Tabs.MakeActive(tabs, closed));
        Assert.Throws<InvalidOperationException>(() => Tabs.MakePreview(tabs, closed));
    }

    [Fact]
    public void Closing_a_tab_stops_it_being_active_or_the_preview_and_counts_on_upward()
    {
        var tabs = new DocumentTabs();
        var tab = Tabs.AddNew(tabs);
        Tabs.MakeActive(tabs, tab);
        Tabs.MakePreview(tabs, tab);
        var before = tabs.Changes;

        Tabs.Remove(tabs, tab);

        Assert.Null(tabs.Active);
        Assert.Null(tabs.Preview);
        Assert.True(tabs.Changes > before);
    }

    [Fact]
    public void Showing_hidden_members_counts_as_a_change_only_when_it_is_one()
    {
        var view = new DiagramViewState();
        var before = view.Changes;

        Diagrams.ShowHiddenMembers(view, false);
        var unchanged = view.Changes;
        Diagrams.ShowHiddenMembers(view, true);
        Diagrams.ShowHiddenMembers(view, true);

        Assert.Equal(before, unchanged);
        Assert.Equal(before + 1, view.Changes);
        Assert.True(view.ShowsHiddenMembers);
    }
}
