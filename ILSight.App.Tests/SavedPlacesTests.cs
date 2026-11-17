using ILSight.CommandArea;
using ILSight.Core;
using System.Text.Json;
using ILSight.SettingArea;

namespace ILSight.App.Tests;

/// <summary>Tabs and bookmarks as settings files hold them, each with the versioned path to its node.</summary>
public sealed class SavedPlacesTests : IDisposable
{
    private const string Fixtures = AppStateDriver.FixturesNamespace;

    private readonly TempDir _temp = new();

    public void Dispose()
    {
        _temp.Dispose();
    }

    private const string GreeterPath = $$"""
        { "Version": 1, "Steps": [
            { "Step": "Namespace {{Fixtures}}", "Label": "{{Fixtures}}" },
            { "Step": "Definition T:{{Fixtures}}.Greeter", "Label": "Greeter" } ] }
        """;

    private static string FixturePath => AppStateDriver.FixturePath.Replace(@"\", @"\\");

    private AppStateDriver Restored(string json)
    {
        var (d, problem) = RestoredWith(json);
        UnionAsserts.GetCase<SettingsProblem.None>(problem);
        return d;
    }

    private (AppStateDriver, SettingsProblem) RestoredWith(string json)
    {
        var path = _temp.Write("settings.json", json);
        var read = AppRoot.ReadSettingsFile(FullFilePath.Of(path));
        var settings = read.Settings;
        var problem = read.Problem;
        var d = AppStateDriver.Started(settings);
        d.Settle();
        return (d, problem);
    }

    private static string WithTab(string place)
    {
        return $$"""
        { "ActiveAssemblyList": "Default",
          "AssemblyLists": [ { "Name": "Default", "Assemblies": [ "{{FixturePath}}" ],
            "Documents": { "Active": "document-1", "Tabs": [
              { "kind": "node", "Panel": "document-1", "Title": "Saved", "FilePath": "{{FixturePath}}", {{place}}, "Language": "CSharp" } ] } } ] }
        """;
    }

    private static string WithBookmark(string place)
    {
        return $$"""
        { "ActiveAssemblyList": "Default",
          "AssemblyLists": [ { "Name": "Default", "Assemblies": [ "{{FixturePath}}" ] } ],
          "Bookmarks": [ { "Name": "Saved", "Enabled": true, "FileName": "{{FixturePath}}", "Kind": "Line", "LineNumber": 1,
            {{place}}, "Language": "CSharp" } ] }
        """;
    }

    private JsonElement Saved(AppStateDriver d)
    {
        var write = d.AppState.SettingsSaver.Save(d.AppState);
        UnionAsserts.GetCase<SettingsWriteResult.Written>(write);
        using var json = JsonDocument.Parse(File.ReadAllText(_temp.PathOf("settings.json")));
        return json.RootElement.Clone();
    }

    private static string[] StepsOf(JsonElement path)
    {
        return path.GetProperty("Steps").EnumerateArray().Select(static s =>
        {
            var step = s.GetProperty("Step").GetString();
            Assert.NotNull(step);
            return step;
        }).ToArray();
    }

    [Fact]
    public async Task A_tab_saved_with_a_versioned_path_shows_its_node_again()
    {
        var d = Restored(WithTab($"\"Path\": {GreeterPath}"));

        var greeter = await d.FixtureAsync("Greeter");
        Assert.Same(greeter, Assert.Single(d.AppState.ListSession.Navigation.OpenTabs).Node);
    }

    [Fact]
    public async Task A_bookmark_saved_with_a_versioned_path_goes_to_its_node_again()
    {
        var d = Restored(WithBookmark($"\"Path\": {GreeterPath}"));

        var bookmark = Assert.Single(d.AppState.Bookmarks.Items);
        d.InTurn(() => Commands.StartGoingToBookmark(d.AppState, bookmark));
        d.Settle();

        var greeter = await d.FixtureAsync("Greeter");
        Assert.Same(greeter, d.AppState.ListSession.Navigation.ActiveTab?.Node);
    }

    [Theory]
    [InlineData("""{ "Version": 1, "Steps": [ { "Step": "Somewhere ZGF", "Label": "ZGF" } ] }""")]
    [InlineData("""{ "Version": 1, "Steps": [ { "Step": "Folder Nowhere", "Label": "Nowhere" } ] }""")]
    [InlineData("""{ "Version": 1, "Steps": [ { "Label": "Greeter" } ] }""")]
    [InlineData("""{ "Version": 1 }""")]
    [InlineData("""{ "Version": 2, "Steps": [] }""")]
    [InlineData("""{ "Steps": [] }""")]
    public void A_tab_whose_path_cannot_be_read_is_reported_and_left_out_rather_than_shown_at_the_root(string path)
    {
        var (d, problem) = RestoredWith(WithTab($"\"Path\": {path}"));

        Assert.Equal(["AssemblyLists[0].Documents.Tabs[0]"], UnionAsserts.GetCase<SettingsProblem.ValuesLeftOut>(problem).Fields);
        Assert.Null(Assert.Single(d.AppState.ListSession.Navigation.OpenTabs).Node);
    }

    [Fact]
    public void A_bookmark_whose_path_cannot_be_read_is_reported_and_left_out()
    {
        var (d, problem) = RestoredWith(WithBookmark("\"Path\": { \"Version\": 2, \"Steps\": [] }"));

        Assert.Equal(["Bookmarks[0]"], UnionAsserts.GetCase<SettingsProblem.ValuesLeftOut>(problem).Fields);
        Assert.Empty(d.AppState.Bookmarks.Items);
    }

    [Fact]
    public void A_bookmark_whose_node_is_not_found_keeps_its_path_as_read()
    {
        const string missingPath = $$"""
            { "Version": 1, "Steps": [ { "Step": "Namespace {{Fixtures}}", "Label": "{{Fixtures}}" }, { "Step": "Definition T:{{Fixtures}}.Missing", "Label": "Missing" } ] }
            """;
        var d = Restored(WithBookmark($"\"Path\": {missingPath}"));

        var bookmark = Assert.Single(d.AppState.Bookmarks.Items);
        d.InTurn(() => Commands.StartGoingToBookmark(d.AppState, bookmark));
        d.Settle();
        var saved = Saved(d).GetProperty("Bookmarks")[0];

        Assert.Equal([$"Namespace {Fixtures}", $"Definition T:{Fixtures}.Missing"], StepsOf(saved.GetProperty("Path")));
    }
}
