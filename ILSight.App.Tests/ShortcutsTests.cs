namespace ILSight.App.Tests;

public sealed class ShortcutsTests
{
    [Fact]
    public async Task The_shortcuts_that_act_on_a_document_do_nothing_while_a_text_box_takes_the_keys()
    {
        var app = AppDriver.Opened(width: 1600f);
        app.Settle(2);
        app.Show(await app.FixtureAsync("Greeter"), 2, inNewTab: true);
        app.Settle(2);
        var navigation = app.AppState.ListSession.Navigation;
        var active = navigation.ActiveTab;
        Assert.NotNull(active);
        var open = navigation.OpenTabs.Count;
        app.Frames.Key(Shortcuts.Search).Frame(2);
        app.Frames.Type("Gre").Frame(2);
        Assert.Equal("Gre", app.Ui.Search.Query);

        app.Frames.Key(Shortcuts.CloseTab).Frame(2);
        app.Frames.Key(Shortcuts.NextTab).Frame(2);
        app.Frames.Key(Shortcuts.PreviousTab).Frame(2);
        app.Frames.Key(Shortcuts.Export).Frame(2);
        app.Settle(2);

        Assert.Equal(open, navigation.OpenTabs.Count);
        Assert.Same(active, navigation.ActiveTab);
        Assert.False(app.Ui.ExportSetup.IsOpen);
        Assert.Equal("Gre", app.Ui.Search.Query);
    }
}
