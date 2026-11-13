
namespace ILSight.App.Tests;

public sealed class LayoutResetTests
{
    [Fact]
    public void Cancelling_the_reset_question_leaves_the_layout_as_it_was()
    {
        var app = new AppDriver();
        var screen = app.Ui;
        ScreenLayout.OpenPanel(screen, DockView.Bookmarks);
        app.Run(2);

        app.Frames.ClickOn(Selector.WithText("View"));
        app.Frames.ClickOn(Selector.OfRole(SemanticRole.MenuItem, "Reset layout")).Frame(2);
        Assert.True(screen.ConfirmingLayoutReset);
        app.Frames.ClickOn(Selector.WithText("Cancel")).Frame(2);

        Assert.False(screen.ConfirmingLayoutReset);
        Assert.True(screen.Layout.Contains(DockPanels.Bookmarks));
    }
}
