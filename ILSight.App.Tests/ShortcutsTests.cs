namespace ILSight.App.Tests;

public sealed class ShortcutsTests
{
    [Fact]
    public async Task The_shortcuts_that_act_on_a_document_do_nothing_while_a_text_box_takes_the_keys()
    {
        var app = AppDriver.Opened(width: 1600f);
        app.Settle(2);
        var greeter = await app.FixtureAsync("Greeter");
        app.Show(greeter, 2, inNewTab: true);
        app.Settle(2);
        var navigation = app.AppState.ListSession.Navigation;
        var active = navigation.ActiveTab;
        Assert.NotNull(active);
        var open = navigation.OpenTabs.Count;
        app.Frames.Key(Shortcuts.Search);
        app.Frames.Frame(2);
        app.Frames.Type("Gre");
        app.Frames.Frame(2);
        Assert.Equal("Gre", app.Ui.Search.Query);

        app.Frames.Key(Shortcuts.CloseTab);
        app.Frames.Frame(2);
        app.Frames.Key(Shortcuts.NextTab);
        app.Frames.Frame(2);
        app.Frames.Key(Shortcuts.PreviousTab);
        app.Frames.Frame(2);
        app.Frames.Key(Shortcuts.Export);
        app.Frames.Frame(2);
        app.Settle(2);

        Assert.Equal(open, navigation.OpenTabs.Count);
        Assert.Same(active, navigation.ActiveTab);
        Assert.False(app.Ui.ExportSetup.IsOpen);
        Assert.Equal("Gre", app.Ui.Search.Query);
    }
}
