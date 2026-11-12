using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using ZGF.Desktop;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using static ILSight.SettingsKeeping.Settings;
using static ILSight.App.Tests.TestPaths;
using ILSight.Bookmarking;
using ILSight.Commanding;
using ILSight.AppSettingsFormat;
using ILSight.AssemblyListKeeping;
using ILSight.DisplayChoices;
using ILSight.Diagramming;
using ILSight.SettingsKeeping;

namespace ILSight.App.Tests;

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
        var settings = AppRoot.ReadSettingsFile(FullFilePath.Of(SettingsFile)).Settings;
        settings.AssemblyLists = [new AssemblyListRecord("Work", [FakePath("a.dll"), FakePath("b.dll")]), new AssemblyListRecord("Play", [FakePath("c.dll")])];
        settings.ActiveAssemblyList = "Play";
        settings.Language = DecompilationLanguage.IL;
        var braces = DecompilerToggles.FindToggle("AlwaysUseBraces");
        Assert.NotNull(braces);
        settings.Decompiler = DecompilerToggles.SetToggle(new DecompilerOptions { LanguageVersion = CSharpVersion.CSharp7_3 }, braces, false);
        settings.FoldedOptionGroups = ["Other"];

        Assert.IsType<SettingsWriteResult.Written>(TempSettings.Save(settings));

        var (loaded, problem) = AppRoot.ReadSettingsFile(FullFilePath.Of(SettingsFile));
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
        var (loaded, problem) = AppRoot.ReadSettingsFile(FullFilePath.Of(Written(json)));

        Assert.IsType<SettingsProblem.None>(problem);
        Assert.Equal(DecompilerOptions.Default with { LanguageVersion = version }, loaded.Decompiler);
        Assert.False(loaded.Decompiler.HasChanges);
    }

    [Fact]
    public void A_decompiler_toggle_the_decompiler_lacks_is_reported_and_left_out()
    {
        var (loaded, problem) = AppRoot.ReadSettingsFile(FullFilePath.Of(Written("""{ "Decompiler": { "Toggles": { "NoSuchToggle": true, "AlwaysUseBraces": false } } }""")));

        Assert.Equal(["Decompiler"], Assert.IsType<SettingsProblem.ValuesLeftOut>(problem).Fields);
        var braces = DecompilerToggles.FindToggle("AlwaysUseBraces");
        Assert.NotNull(braces);
        Assert.Equal(DecompilerToggles.SetToggle(DecompilerOptions.Default, braces, false), loaded.Decompiler);
    }

    [Fact]
    public void A_decompiler_toggle_saved_at_its_default_loads_as_not_set()
    {
        var braces = DecompilerToggles.FindToggle("AlwaysUseBraces");
        Assert.NotNull(braces);
        var (loaded, problem) = AppRoot.ReadSettingsFile(FullFilePath.Of(Written($$"""{ "Decompiler": { "Toggles": { "AlwaysUseBraces": {{(braces.DefaultValue ? "true" : "false")}} } } }""")));

        Assert.IsType<SettingsProblem.None>(problem);
        Assert.False(DecompilerToggles.IsChanged(loaded.Decompiler, braces));
        Assert.Equal(DecompilerOptions.Default, loaded.Decompiler);
    }

    [Fact]
    public void Window_bounds_round_trip_and_open_the_window_where_it_was_left()
    {
        var settings = AppRoot.ReadSettingsFile(FullFilePath.Of(SettingsFile)).Settings;
        settings.Window = new WindowBounds(-1200, 80, 1600, 1000, Maximized: true);
        Assert.IsType<SettingsWriteResult.Written>(TempSettings.Save(settings));

        var loaded = AppRoot.ReadSettingsFile(FullFilePath.Of(SettingsFile)).Settings;
        var window = Assert.NotNull(loaded.Window);
        var config = window.ApplyTo(new StartupConfig { WindowWidth = 1400, WindowHeight = 900, WindowTitle = "ILSight" });

        Assert.Equal(new WindowBounds(-1200, 80, 1600, 1000, true), loaded.Window);
        Assert.Equal((-1200, 80, 1600, 1000, true), (config.WindowX, config.WindowY, config.WindowWidth, config.WindowHeight, config.StartMaximized));
        Assert.Null(AppRoot.ReadSettingsFile(FullFilePath.Of(_temp.PathOf("missing.json"))).Settings.Window);
    }

    [Theory]
    [InlineData(10, 20, AppSettings.MinWindowWidth, AppSettings.MinWindowHeight)]
    [InlineData(100_000, 90_000, AppSettings.MaxWindowSize, AppSettings.MaxWindowSize)]
    [InlineData(1200, 800, 1200, 800)]
    public void Saved_window_sizes_load_within_reason(int width, int height, int expectedWidth, int expectedHeight)
    {
        var loaded = AppRoot.ReadSettingsFile(FullFilePath.Of(Written($$"""{ "Window": { "X": 5, "Y": 6, "Width": {{width}}, "Height": {{height}}, "Maximized": false } }"""))).Settings;

        Assert.Equal(new WindowBounds(5, 6, expectedWidth, expectedHeight, false), loaded.Window);
    }

    [Fact]
    public void The_export_window_reopens_where_it_was_left_in_the_last_run()
    {
        var d = new AppStateDriver(AppRoot.ReadSettingsFile(FullFilePath.Of(SettingsFile)).Settings);
        d.Ui.ExportSetup.Window.Bounds = new WindowBounds(-900, 200, 700, 800, false);
        DialogWindows.Settle(d.Ui.ExportSetup.Window, held: false);
        d.NextFrame();
        d.AppState.SettingsSaving.Save(d.AppState);

        var next = new AppStateDriver(AppRoot.ReadSettingsFile(FullFilePath.Of(SettingsFile)).Settings);

        Assert.Equal(new WindowBounds(-900, 200, 700, 800, false), next.Ui.ExportSetup.Window.Bounds);
        Assert.Null(AppRoot.ReadSettingsFile(FullFilePath.Of(Written("""{ "ExportWindow": { "X": 5, "Y": 6, "Width": 0, "Height": 0, "Maximized": false } }"""))).Settings.ExportWindow);
    }

    [Fact]
    public void Window_bounds_without_a_size_load_as_none()
    {
        Assert.Null(AppRoot.ReadSettingsFile(FullFilePath.Of(Written("""{ "Window": { "X": 5, "Y": 6, "Width": 0, "Height": 0, "Maximized": false } }"""))).Settings.Window);
        Assert.Null(SettingsJson.SanitizeWindow(default(WindowBounds)));
    }

    [Fact]
    public void Null_assembly_lists_load_as_none()
    {
        var (loaded, problem) = AppRoot.ReadSettingsFile(FullFilePath.Of(Written("""{ "AssemblyLists": null, "Theme": "Monokai" }""")));

        Assert.IsType<SettingsProblem.None>(problem);
        Assert.Empty(loaded.AssemblyLists);
        Assert.Equal("monokai", loaded.Theme.Text);
    }

    [Fact]
    public void Lists_missing_their_name_or_assemblies_load_without_them()
    {
        var (loaded, problem) = AppRoot.ReadSettingsFile(FullFilePath.Of(Written($$"""{ "AssemblyLists": [ null, { "Name": null }, { "Name": "Work", "Assemblies": null }, { "Name": "Play", "Assemblies": [ " ", "a.dll", "{{AInJson}}" ] } ] }""")));

        Assert.IsType<SettingsProblem.None>(problem);
        Assert.Equal(["Work", "Play"], loaded.AssemblyLists.Select(static l => l.Name));
        Assert.Empty(loaded.AssemblyLists[0].Assemblies);
        Assert.Equal([A], loaded.AssemblyLists[1].Assemblies.Select(static p => p.Text));
    }

    [Fact]
    public void The_one_list_of_an_older_settings_file_becomes_the_default_list_and_is_saved_only_there()
    {
        var loaded = AppRoot.ReadSettingsFile(FullFilePath.Of(Written($$"""{ "Assemblies": [ "{{AInJson}}", "", "{{BInJson}}" ] }"""))).Settings;

        var list = Assert.Single(loaded.AssemblyLists);
        Assert.Equal(SavedAssemblyLists.DefaultName, list.Name);
        Assert.Equal([A, B], list.Assemblies.Select(static p => p.Text));
        Assert.IsType<SettingsWriteResult.Written>(TempSettings.Save(loaded));
        Assert.False(JsonDocument.Parse(File.ReadAllText(SettingsFile)).RootElement.TryGetProperty("Assemblies", out _));
    }

    [Fact]
    public void The_analyses_of_an_older_settings_file_go_to_the_open_list_and_are_saved_only_there()
    {
        var loaded = AppRoot.ReadSettingsFile(FullFilePath.Of(Written($$"""
            { "AssemblyLists": [ { "Name": "Work", "Assemblies": [] }, { "Name": "Play", "Assemblies": [] } ],
              "ActiveAssemblyList": "Play",
              "Analyzed": [ { "Kind": "Definition", "FilePath": "{{AInJson}}", "Token": 33554434 } ] }
            """))).Settings;

        var workAnalyzed = loaded.AssemblyLists[0].Analyzed;
        Assert.NotNull(workAnalyzed);
        Assert.Empty(workAnalyzed);
        var playAnalyzed = loaded.AssemblyLists[1].Analyzed;
        Assert.NotNull(playAnalyzed);
        var analyzed = Assert.Single(playAnalyzed);
        Assert.Equal(new CodeAnchor(CodeAnchorKind.Definition, PathOf(A), 33554434), analyzed);
        Assert.IsType<SettingsWriteResult.Written>(TempSettings.Save(loaded));
        Assert.False(JsonDocument.Parse(File.ReadAllText(SettingsFile)).RootElement.TryGetProperty("Analyzed", out _));
    }

    [Theory]
    [InlineData(false, ApiVisibility.PublicOnly)]
    [InlineData(true, ApiVisibility.AllButCompilerGenerated)]
    public void The_non_public_switch_of_an_older_settings_file_becomes_its_visibility_and_is_saved_only_as_that(bool showNonPublic, ApiVisibility expected)
    {
        var loaded = AppRoot.ReadSettingsFile(FullFilePath.Of(Written($$"""{ "ShowNonPublic": {{(showNonPublic ? "true" : "false")}} }"""))).Settings;

        Assert.Equal(expected, loaded.ApiVisibility);
        Assert.IsType<SettingsWriteResult.Written>(TempSettings.Save(loaded));
        Assert.False(JsonDocument.Parse(File.ReadAllText(SettingsFile)).RootElement.TryGetProperty("ShowNonPublic", out _));
    }

    [Fact]
    public void An_unreadable_file_falls_back_to_defaults_and_is_reported()
    {
        var loadResult = AppRoot.ReadSettingsFile(FullFilePath.Of(Written("""{ "Assemblies": [ "a.dll", """)));
        var (loaded, problem) = loadResult;
        var appState = AppRoot.RestoreAppState(loadResult, CommandLines.Parse([]).Arguments, TestSession.Shared);

        Assert.Empty(loaded.AssemblyLists);
        Assert.IsType<SettingsProblem.NotJson>(problem);
        var error = Assert.Single(appState.Errors.Entries);
        Assert.Contains(SettingsFile, error.Words().Details);
    }

    [Fact]
    public void An_unreadable_file_is_copied_beside_it_before_the_settings_are_saved_over_it()
    {
        const string broken = """{ "Assemblies": [ "a.dll", """;
        var loadResult = AppRoot.ReadSettingsFile(FullFilePath.Of(Written(broken)));
        var (loaded, problem) = loadResult;
        var appState = AppRoot.RestoreAppState(loadResult, CommandLines.Parse([]).Arguments, TestSession.Shared);

        Assert.IsType<SettingsWriteResult.Written>(TempSettings.Save(loaded));

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
            loadResult = AppRoot.ReadSettingsFile(FullFilePath.Of(path));
        }

        Assert.IsType<SettingsWriteResult.Written>(TempSettings.Save(loadResult.Settings));

        Assert.IsType<SettingsKept.NotSaved>(Assert.IsType<SettingsProblem.NotRead>(loadResult.Problem).Kept);
        Assert.Equal(held, File.ReadAllText(path));
    }

    [Fact]
    public void A_file_that_cannot_be_read_or_copied_keeps_the_themes_beside_it_and_is_named_in_the_error()
    {
        const string held = """{ "Theme": "light" }""";
        var path = Written(held);
        _temp.Write(Path.Combine("themes", "mine.json"), """{ "extends": "dark" }""");
        SettingsLoadResult loadResult;
        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            loadResult = AppRoot.ReadSettingsFile(FullFilePath.Of(path));
        }

        var appState = AppRoot.RestoreAppState(loadResult, CommandLines.Parse([]).Arguments, TestSession.Shared);
        appState.SettingsSaving.Save(appState);

        Assert.IsType<SettingsKept.NotSaved>(Assert.IsType<SettingsProblem.NotRead>(loadResult.Problem).Kept);
        Assert.Contains(ThemeId<ThemeFile>.Of("mine"), appState.Themes.Ids);
        Assert.StartsWith(path + Environment.NewLine, Assert.Single(appState.Errors.Entries).Words().Details);
        Assert.Equal(held, File.ReadAllText(path));
    }

    [Fact]
    public void A_number_where_the_settings_name_a_language_is_refused()
    {
        var (loaded, problem) = AppRoot.ReadSettingsFile(FullFilePath.Of(Written("""{ "Locale": 42 }""")));

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
        var loadResult = AppRoot.ReadSettingsFile(FullFilePath.Of(Written(json)));
        var (loaded, problem) = loadResult;
        var appState = AppRoot.RestoreAppState(loadResult, CommandLines.Parse([]).Arguments, TestSession.Shared);

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
        var (loaded, problem) = AppRoot.ReadSettingsFile(FullFilePath.Of(Written($$"""
            { "AssemblyLists": [
                { "Name": "Broken", "Assemblies": 5 },
                { "Name": "Work", "Assemblies": [ "{{AInJson}}" ], "Documents": { "Tabs": [
                    { "kind": "nothing", "Panel": "document-1" },
                    { "kind": "comparison", "Panel": "document-2", "LeftPath": "{{AInJson}}", "RightPath": "{{BInJson}}" } ] } } ] }
            """)));

        Assert.Equal(["AssemblyLists"], Assert.IsType<SettingsProblem.ValuesLeftOut>(problem).Fields);
        var list = Assert.Single(loaded.AssemblyLists);
        Assert.Equal("Work", list.Name);
        Assert.NotNull(list.Documents);
        Assert.Equal("document-2", Assert.Single(list.Documents.Tabs).Panel);
    }

    [Fact]
    public void Display_options_round_trip_and_a_font_missing_its_file_is_left_out_alone()
    {
        var settings = AppRoot.ReadSettingsFile(FullFilePath.Of(SettingsFile)).Settings;
        settings.Display = new DisplayOptions(CodeFontSize.Clamp(14f), false, true, false, true, false, 1.25f, new CodeFont("Mono", "/mono.ttf", 1),
            TokenFormat.Hex, true, true, true, false);
        Assert.IsType<SettingsWriteResult.Written>(TempSettings.Save(settings));

        Assert.Equal(settings.Display, AppRoot.ReadSettingsFile(FullFilePath.Of(SettingsFile)).Settings.Display);
        var (loaded, problem) = AppRoot.ReadSettingsFile(FullFilePath.Of(Written("""{ "Display": { "ShowLineNumbers": false, "Font": { "Family": "Mono", "Path": null } } }""")));
        Assert.IsType<SettingsProblem.None>(problem);
        Assert.Equal(DisplayOptions.Default with { ShowLineNumbers = false }, loaded.Display);
    }

    [Theory]
    [InlineData("""{ "Display": { "FontSize": 900, "UiScale": 1.3 } }""", CodeFontSize.Max, 1.25f)]
    [InlineData("""{ "Display": { "FontSize": 1, "UiScale": 0.01 } }""", CodeFontSize.Min, 0.75f)]
    public void A_saved_font_size_out_of_range_and_a_ui_scale_not_offered_load_within_what_is(string json, float fontSize, float uiScale)
    {
        var (loaded, problem) = AppRoot.ReadSettingsFile(FullFilePath.Of(Written(json)));

        Assert.IsType<SettingsProblem.None>(problem);
        Assert.NotNull(loaded.Display);
        Assert.Equal(fontSize, loaded.Display.FontSize?.Points);
        Assert.Equal(uiScale, loaded.Display.UiScale);
    }

    [Fact]
    public void A_list_whose_documents_layout_is_missing_a_part_keeps_its_tabs_without_it_and_is_reported()
    {
        var (loaded, problem) = AppRoot.ReadSettingsFile(FullFilePath.Of(Written($$"""
            { "AssemblyLists": [ { "Name": "Work", "Assemblies": [], "Documents": {
                "Tabs": [ { "kind": "comparison", "Panel": "document-1", "LeftPath": "{{AInJson}}", "RightPath": "{{BInJson}}" } ],
                "Layout": { "Root": { "kind": "group", "Panels": [ null ] } } } } ] }
            """)));

        Assert.Equal(["AssemblyLists"], Assert.IsType<SettingsProblem.ValuesLeftOut>(problem).Fields);
        var documents = Assert.Single(loaded.AssemblyLists).Documents;
        Assert.NotNull(documents);
        Assert.Single(documents.Tabs);
        Assert.Null(documents.Layout);
    }

    [Fact]
    public void Filters_and_bookmark_views_load_without_the_parts_they_hold_as_null()
    {
        var (loaded, problem) = AppRoot.ReadSettingsFile(FullFilePath.Of(Written($$"""
            { "FlagsFilters": { "TypeDef/Attributes": { "Groups": { "Visibility": null, "Layout": [ 8 ] }, "Mode": "Any" }, "Gone": null },
              "Bookmarks": [ { "Name": "B", "FileName": "{{AInJson}}",
                "ViewState": { "ExpandedFoldings": [ null, { "Start": 1, "End": 2 } ], "SelectedTreeNodePath": [ "{{AInJson}}", null ] } } ] }
            """)));

        Assert.IsType<SettingsProblem.None>(problem);
        Assert.NotNull(loaded.FlagsFilters);
        var filter = Assert.Single(loaded.FlagsFilters).Value;
        Assert.NotNull(filter.Groups);
        Assert.Equal(["Layout"], filter.Groups.Keys);
        var view = Assert.Single(loaded.Bookmarks).ViewState;
        Assert.NotNull(view);
        Assert.Equal([new BookmarkFoldingRange(1, 2)], view.ExpandedFoldings);
        Assert.Null(view.SelectedTreeNodePath);
    }

    [Fact]
    public void Saved_values_missing_what_they_name_load_without_them()
    {
        var (loaded, problem) = AppRoot.ReadSettingsFile(FullFilePath.Of(Written($$"""
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
        Assert.NotNull(list.Analyzed);
        Assert.Empty(list.Analyzed);
        Assert.NotNull(list.Documents);
        var diagram = Assert.IsType<DiagramDocumentRecord>(Assert.Single(list.Documents.Tabs));
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
        var (_, problem) = AppRoot.ReadSettingsFile(FullFilePath.Of(Written("null")));

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
        var loadResult = AppRoot.ReadSettingsFile(FullFilePath.Of(Written($$"""{ "Theme": "light", {{json[1..]}}""")));
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
        var (loaded, problem) = AppRoot.ReadSettingsFile(FullFilePath.Of(Written("""
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
        var (loaded, problem) = AppRoot.ReadSettingsFile(FullFilePath.Of(Written("""{ "Layout": { "NextId": 7 } }""")));

        Assert.IsType<SettingsProblem.None>(problem);
        Assert.NotNull(loaded.Layout);
        AssertEmptyGroup(loaded.Layout.Root);
        Assert.Empty(loaded.Layout.Windows);
        Assert.Equal(7, loaded.Layout.NextId);
    }

    [Fact]
    public void A_layout_leaving_out_everything_is_an_empty_group_numbering_from_one()
    {
        var (loaded, problem) = AppRoot.ReadSettingsFile(FullFilePath.Of(Written("""{ "Layout": { } }""")));

        Assert.IsType<SettingsProblem.None>(problem);
        Assert.NotNull(loaded.Layout);
        AssertEmptyGroup(loaded.Layout.Root);
        Assert.Empty(loaded.Layout.Windows);
        Assert.Equal(1, loaded.Layout.NextId);
    }

    [Fact]
    public void A_floating_window_leaving_out_its_root_has_an_empty_group_there()
    {
        var (loaded, problem) = AppRoot.ReadSettingsFile(FullFilePath.Of(Written("""{ "Layout": { "Windows": [ { "Id": 4, "X": 10, "Width": 300 } ] } }""")));

        Assert.IsType<SettingsProblem.None>(problem);
        Assert.NotNull(loaded.Layout);
        var window = Assert.Single(loaded.Layout.Windows);
        Assert.Equal((4, 10f, 300f), (window.Id, window.X, window.Width));
        AssertEmptyGroup(window.Root);
    }

    [Fact]
    public void A_split_leaving_out_its_first_part_has_an_empty_group_there()
    {
        var (loaded, problem) = AppRoot.ReadSettingsFile(FullFilePath.Of(Written("""
            { "Layout": { "Root": { "kind": "split", "Id": 1, "Ratio": 0.25,
                "Second": { "kind": "group", "Id": 2, "Panels": [ "search" ] } } } }
            """)));

        Assert.IsType<SettingsProblem.None>(problem);
        var split = Assert.IsType<DockSplit>(loaded.Layout?.Root);
        AssertEmptyGroup(split.First);
        Assert.Equal(["search"], Assert.IsType<DockGroup>(split.Second).Panels);
        Assert.Equal(0.25f, split.Ratio);
    }

    [Fact]
    public void A_split_leaving_out_its_second_part_has_an_empty_group_there()
    {
        var (loaded, problem) = AppRoot.ReadSettingsFile(FullFilePath.Of(Written("""
            { "Layout": { "Root": { "kind": "split", "Id": 1, "Ratio": 0.25,
                "First": { "kind": "group", "Id": 2, "Panels": [ "search" ] } } } }
            """)));

        Assert.IsType<SettingsProblem.None>(problem);
        var split = Assert.IsType<DockSplit>(loaded.Layout?.Root);
        Assert.Equal(["search"], Assert.IsType<DockGroup>(split.First).Panels);
        AssertEmptyGroup(split.Second);
    }

    [Fact]
    public void A_split_leaving_out_both_parts_has_two_empty_groups_which_do_not_count_as_one_id_twice()
    {
        var (loaded, problem) = AppRoot.ReadSettingsFile(FullFilePath.Of(Written("""{ "Layout": { "Root": { "kind": "split", "Id": 1 } } }""")));

        Assert.IsType<SettingsProblem.None>(problem);
        var split = Assert.IsType<DockSplit>(loaded.Layout?.Root);
        AssertEmptyGroup(split.First);
        AssertEmptyGroup(split.Second);
    }

    [Fact]
    public void A_split_leaving_out_its_ratio_divides_the_space_evenly()
    {
        var (loaded, problem) = AppRoot.ReadSettingsFile(FullFilePath.Of(Written("""
            { "Layout": { "Root": { "kind": "split", "Id": 1, "Direction": "Vertical",
                "First": { "kind": "group", "Id": 2, "Panels": [ "tree" ] },
                "Second": { "kind": "group", "Id": 3, "Panels": [ "search" ] } } } }
            """)));

        Assert.IsType<SettingsProblem.None>(problem);
        var split = Assert.IsType<DockSplit>(loaded.Layout?.Root);
        Assert.Equal(0.5f, split.Ratio);
        Assert.Equal(Direction.Vertical, split.Direction);
    }

    [Fact]
    public void A_group_leaving_out_its_panels_is_empty_and_keeps_what_else_it_holds()
    {
        var (loaded, problem) = AppRoot.ReadSettingsFile(FullFilePath.Of(Written("""{ "Layout": { "Root": { "kind": "group", "Id": 3, "Active": "search" } } }""")));

        Assert.IsType<SettingsProblem.None>(problem);
        var group = Assert.IsType<DockGroup>(loaded.Layout?.Root);
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
        var (loaded, problem) = AppRoot.ReadSettingsFile(FullFilePath.Of(Written(json)));

        Assert.Equal(["Layout"], Assert.IsType<SettingsProblem.ValuesLeftOut>(problem).Fields);
        Assert.Null(loaded.Layout);
    }

    [Fact]
    public void A_tab_of_no_kind_at_all_leaves_out_that_one_alone()
    {
        var (loaded, problem) = AppRoot.ReadSettingsFile(FullFilePath.Of(Written($$"""
            { "AssemblyLists": [ { "Name": "Work", "Assemblies": [], "Documents": { "Tabs": [
                { "Panel": "document-1" },
                { "kind": "comparison", "Panel": "document-2", "LeftPath": "{{AInJson}}", "RightPath": "{{BInJson}}" } ] } } ] }
            """)));

        Assert.Equal(["AssemblyLists"], Assert.IsType<SettingsProblem.ValuesLeftOut>(problem).Fields);
        var documents = Assert.Single(loaded.AssemblyLists).Documents;
        Assert.NotNull(documents);
        Assert.Equal("document-2", Assert.Single(documents.Tabs).Panel);
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
        var settings = AppRoot.ReadSettingsFile(FullFilePath.Of(SettingsFile)).Settings;
        settings.AssemblyLists = Enumerable.Range(0, 200).Select(i => new AssemblyListRecord($"list-{i}", [])).ToList();
        TempSettings.Save(settings);
        settings.AssemblyLists = [new AssemblyListRecord("one", [])];

        TempSettings.Save(settings);

        var json = JsonDocument.Parse(File.ReadAllText(SettingsFile));
        Assert.Equal(1, json.RootElement.GetProperty("AssemblyLists").GetArrayLength());
    }
}
