namespace ILSight.App.Tests;

public sealed class NamespacePageTests
{
    [Fact]
    public void A_type_named_on_its_namespaces_page_opens_when_clicked()
    {
        var app = AppDriver.Opened();
        var d = app.Driver;
        d.Show(d.Node(AppStateDriver.FixturesNamespace));
        var tab = d.AppState.Navigation.ActiveTab;
        Assert.NotNull(tab);
        var (line, column) = tab.Document.GetPosition("Greeter");
        var span = tab.Document.SpanAt(line, column);
        app.Run(3);

        var view = app.Frames.Canvas.GlyphRuns.First(r => r.Text == "Greeter");
        app.Frames.Click(view.Origin.X + 4f, app.Frames.Height - view.Origin.Y + 4f);
        d.Settle();

        Assert.False(span.IsDefinition);
        Assert.NotNull(tab.Node);
        Assert.Equal("Greeter", tab.Node.Text);
        Assert.Equal(NodeIcon.Class, tab.Node.Icon);
    }
}
