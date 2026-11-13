using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.AppSettingArea;
using static ILSight.SettingArea.Settings;
using static ILSight.ScreenLayout;
using ILSight.SettingArea;

namespace ILSight.App.Tests;

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
        Assert.IsType<SettingsWriteResult.Written>(d.AppState.SettingsSaving.Save(d.AppState));

        var next = new AppStateDriver(temp.Settings());
        var restored = next.Ui.Layout;

        Assert.Same(next.AppState.Navigation.ActiveTab, next.Ui.Documents.FindTab(Assert.Single(Assert.IsType<DockGroup>(restored.Root).Panels)));
        var window = Assert.Single(restored.Windows);
        Assert.Equal(new UiRect(40f, 50f, 300f, 400f), window.Rect);
        var group = restored.GroupOf(DockPanels.Assemblies);
        Assert.NotNull(group);
        Assert.Same(window, restored.WindowOf(group));
    }

    [Fact]
    public void A_saved_layout_keeps_the_views_left_open_and_shows_the_first_tab_in_place_of_documents_not_kept()
    {
        var saved = new DockLayout { Root = DockLayout.Group("document-7", DockPanels.Search, "gone", DockPanels.Bookmarks) };

        var d = new AppStateDriver(new AppSettings { Layout = saved });
        var fresh = DockPanels.Restore(null);

        var panel = DocumentPanels.GetPanel(d.Ui.Documents, Assert.Single(d.AppState.Navigation.OpenTabs));
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
        var active = appState.Navigation.ActiveTab;
        Assert.NotNull(active);
        Assert.NotSame(search, d.Ui.Layout.GroupOf(DocumentPanels.GetPanel(d.Ui.Documents, active)));

        d.Ui.Layout.Close(DockPanels.Search);

        Assert.Equal("", d.Ui.Search.Query);
    }

    [Fact]
    public void The_search_mode_is_saved_and_restored()
    {
        using var temp = new TempDir();
        var d = new AppStateDriver(temp.Settings());

        d.Ui.Search.LookFor(SearchKinds.Literals);
        d.NextFrame();
        Assert.IsType<SettingsWriteResult.Written>(d.AppState.SettingsSaving.Save(d.AppState));

        Assert.Equal(SearchKinds.Literals, new AppStateDriver(temp.Settings()).Ui.Search.Kinds);
    }

    [Fact]
    public void Saving_leaves_the_settings_the_app_started_with_as_they_were()
    {
        using var temp = new TempDir();
        var settings = temp.Settings();
        var d = new AppStateDriver(settings);

        d.Ui.Search.LookFor(SearchKinds.Literals);
        d.AppState.Decompiler.UseLanguage(DecompilationLanguage.IL);
        d.NextFrame();
        Assert.IsType<SettingsWriteResult.Written>(d.AppState.SettingsSaving.Save(d.AppState));

        Assert.Equal(SearchKinds.TypesAndMembers, settings.SearchKinds);
        Assert.Equal(DecompilationLanguage.CSharp, settings.Language);
        var saved = temp.Settings();
        Assert.Equal(SearchKinds.Literals, saved.SearchKinds);
        Assert.Equal(DecompilationLanguage.IL, saved.Language);
    }

    [Fact]
    public async Task Scoping_the_search_to_a_namespace_keeps_the_terms_typed()
    {
        var driver = AppStateDriver.Opened();
        driver.Ui.Search.Query = "inassembly:Old Greet";

        ScopeSearch(driver.Ui, await driver.NodeAsync(AppStateDriver.FixturesNamespace));

        Assert.Equal($"innamespace:{AppStateDriver.FixturesNamespace} inassembly:Old Greet ", driver.Ui.Search.Query);
        Assert.True(driver.Ui.Layout.Contains(DockPanels.Search));
    }
}
