using ZGF.Gui.Immediate;
using ZGF.Gui.Testing;
using ILSight.Core;

namespace ILSight.App.Tests;

public sealed class PreviewTabTests
{
    /// <summary>The app with the fixture types showing in the tree, none of them selected, and the
    /// assembly shown in the preview tab.</summary>
    private static AppDriver Revealed()
    {
        var app = AppDriver.Opened();
        Trees.RevealNode(app.Ui.Tree, app.Fixture("Greeter"));
        app.Ui.Tree.View.ClearSelection();
        app.Run(2);
        return app;
    }

    private static void Pick(AppDriver app, string row)
    {
        app.Frames.ClickOn(GetFixtureRow(row)).Frame(2);
        app.Settle(2);
    }

    /// <summary>Where the preview tab's title, the one title drawn in italics, reads <paramref name="title"/>.</summary>
    private static UiRect PreviewTitle(AppDriver app, string title)
    {
        var drawn = app.Frames.Canvas.Texts
            .Where(t => t.Inputs.Text == title)
            .Single(IsItalic);
        return app.Frames.ToUi(drawn.Inputs.Position);
    }

    private static bool IsItalic(RecordedText text)
    {
        var family = text.Inputs.Style.FontFamily;
        if (!family.IsSet)
        {
            return false;
        }

        return family.Value == TextFonts.Italic;
    }

    [Fact]
    public void Clicking_a_node_shows_it_in_the_preview_tab_and_clicking_another_shows_that_in_its_place()
    {
        var app = Revealed();
        Pick(app, "Greeter");
        var preview = app.AppState.Navigation.PreviewTab;
        var shownFirst = preview?.Node?.Text;

        Pick(app, "Farewell");

        Assert.Equal("Greeter", shownFirst);
        Assert.Same(preview, app.AppState.Navigation.PreviewTab);
        Assert.Equal("Farewell", preview?.Node?.Text);
        Assert.Single(app.AppState.Navigation.OpenTabs);
    }

    [Fact]
    public void The_frame_a_node_is_clicked_in_draws_the_preview_tab_showing_it()
    {
        var app = Revealed();
        Pick(app, "Greeter");
        var preview = app.AppState.Navigation.PreviewTab;
        Assert.NotNull(preview);
        app.Frames.MoveTo(GetFixtureRow("Farewell"));

        app.Frames.Press().Frame();

        Assert.Equal("Farewell", preview.Node?.Text);
        Assert.Contains(app.Frames.Canvas.Texts, t => t.Inputs.Text == "Farewell" && IsItalic(t));
        Assert.DoesNotContain(app.Frames.Canvas.Texts, t => t.Inputs.Text == "Greeter" && IsItalic(t));
    }

    [Fact]
    public void Double_clicking_a_node_keeps_its_tab_open_and_the_next_node_picked_opens_a_new_preview_tab()
    {
        var app = Revealed();
        app.DoubleClickOn(Selector.WithText("Greeter")).Frame(2);
        app.Settle(2);
        var kept = app.AppState.Navigation.ActiveTab;
        Assert.NotNull(kept);
        var previewAfterOpening = app.AppState.Navigation.PreviewTab;

        Pick(app, "Farewell");

        Assert.Null(previewAfterOpening);
        Assert.Equal("Greeter", kept.Node?.Text);
        Assert.Equal(2, app.AppState.Navigation.OpenTabs.Count);
        Assert.Equal("Farewell", app.AppState.Navigation.PreviewTab?.Node?.Text);
        Assert.Same(app.AppState.Navigation.PreviewTab, app.AppState.Navigation.ActiveTab);
    }

    [Fact]
    public void Clicking_a_node_a_kept_tab_shows_brings_that_tab_forward_instead_of_previewing_it()
    {
        var app = Revealed();
        app.Show(app.Fixture("Greeter"), 2);
        var kept = app.AppState.Navigation.ActiveTab;
        Assert.NotNull(kept);
        app.AppState.Navigation.KeepTabOpen(kept);
        Pick(app, "Farewell");
        var preview = app.AppState.Navigation.PreviewTab;

        Pick(app, "Greeter");

        Assert.Same(kept, app.AppState.Navigation.ActiveTab);
        Assert.Same(preview, app.AppState.Navigation.PreviewTab);
        Assert.Equal("Farewell", preview?.Node?.Text);
        Assert.Equal(2, app.AppState.Navigation.OpenTabs.Count);
    }

    [Fact]
    public void Following_a_reference_from_a_kept_tab_shows_it_in_the_preview_tab_and_leaves_the_kept_tab_alone()
    {
        var app = AppDriver.Opened();
        var greeter = app.Fixture("Greeter");
        app.Show(greeter, 2);
        var kept = app.AppState.Navigation.ActiveTab;
        Assert.NotNull(kept);
        app.AppState.Navigation.KeepTabOpen(kept);

        var farewell = app.Fixture("Farewell").Definition;
        app.Start((a, ct) => a.NavigateHandler.HandleAsync(a, farewell, a.Navigation.BeginResolving(inNewTab: false), ct));
        app.Settle(2);

        Assert.Same(greeter, kept.Node);
        Assert.Equal("Farewell", app.AppState.Navigation.ActiveTab?.Node?.Text);
        Assert.Same(app.AppState.Navigation.ActiveTab, app.AppState.Navigation.PreviewTab);
    }

    [Fact]
    public void The_preview_tabs_title_is_in_italics_until_the_tab_is_double_clicked()
    {
        var app = AppDriver.Opened();
        app.Show(app.Fixture("Greeter"), 2);
        var title = PreviewTitle(app, "Greeter");

        app.Frames.DoubleClick(title).Frame(2);

        Assert.Null(app.AppState.Navigation.PreviewTab);
        Assert.DoesNotContain(app.Frames.Canvas.Texts, IsItalic);
    }

    [Fact]
    public void The_preview_tabs_menu_keeps_it_open()
    {
        var app = AppDriver.Opened();
        app.Show(app.Fixture("Greeter"), 2);
        var title = PreviewTitle(app, "Greeter");

        app.Frames.Click(title.X + 2f, title.Y + title.Height / 2, PointerButton.Right).Frame();
        app.Frames.ClickOn(Selector.OfRole(SemanticRole.MenuItem, "Keep tab open")).Frame(2);

        Assert.Null(app.AppState.Navigation.PreviewTab);
        Assert.Equal("Greeter", app.AppState.Navigation.ActiveTab?.Node?.Text);
    }

    [Fact]
    public void An_empty_tab_made_active_shows_what_is_picked_next_and_the_preview_tab_before_it_stays_open()
    {
        var app = Revealed();
        Pick(app, "Greeter");
        var before = app.AppState.Navigation.PreviewTab;
        Assert.NotNull(before);

        var empty = app.AppState.Navigation.OpenEmptyTab();
        Assert.Null(app.AppState.Navigation.FocusTab(empty, app.AppState.Decompiler));
        app.AppState.ViewCommands.Enqueue(new ViewCommand.ShowTab(empty, before));
        app.Run(2);
        Pick(app, "Farewell");

        Assert.Equal("Greeter", before.Node?.Text);
        Assert.Equal("Farewell", app.AppState.Navigation.PreviewTab?.Node?.Text);
        Assert.Same(empty, app.AppState.Navigation.PreviewTab);
    }
}
