using Xunit.Abstractions;
using static ILSight.ScreenLayout;

namespace ILSight.App.Tests;

public sealed class AppFrameCostTests(ITestOutputHelper output)
{
    private const int WarmUpFrames = 120;
    private const int MeasuredFrames = 600;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_frame_of_the_app_showing_a_document_allocates_nothing(bool errorsOpen)
    {
        var app = AppDriver.Opened();
        app.Settle(2);
        app.Show(app.Fixture("Greeter"), 2);
        app.Settle(5);
        if (errorsOpen)
        {
            OpenErrors(app);
        }

        app.Frames.Canvas.Recording = false;
        app.Run(WarmUpFrames);
        var allocated = SteadyAllocations.PerFrame(MeasuredFrames, _ => app.Run());

        output.WriteLine($"{(errorsOpen ? "errors open" : "idle")}: {allocated:N0} B/frame allocated");
        Assert.Equal(errorsOpen, app.Ui.ErrorDialog.IsOpen);
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void A_frame_of_the_app_with_only_tool_panels_open_allocates_nothing()
    {
        var app = AppDriver.Opened();
        OpenPanel(app.Ui, DockView.Bookmarks);
        app.Settle(2);
        var tab = app.AppState.Tabs.Active;
        Assert.NotNull(tab);
        app.Ui.Layout.Close(DocumentPanels.GetPanel(app.Ui.Documents, tab));
        app.Settle(5);

        app.Frames.Canvas.Recording = false;
        app.Run(WarmUpFrames);
        var allocated = SteadyAllocations.PerFrame(MeasuredFrames, _ => app.Run());

        output.WriteLine($"tool panels only: {allocated:N0} B/frame allocated");
        Assert.Empty(app.AppState.Tabs.Open);
        Assert.True(app.Ui.Layout.Contains(DockPanels.Bookmarks));
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void A_frame_of_the_app_with_a_crumbs_children_listed_allocates_nothing()
    {
        var app = AppDriver.Opened();
        app.Settle(2);
        app.Show(app.Fixture("Greeter"), 2);
        app.Settle(5);
        var tab = app.AppState.Tabs.Active;
        Assert.NotNull(tab);
        var panel = DocumentPanels.GetPanel(app.Ui.Documents, tab);
        app.Ui.Omnibar.OpenMenu(panel, app.Node(AppStateDriver.FixturesNamespace), app.Fixture("Greeter"));
        app.Settle(5);

        app.Frames.Canvas.Recording = false;
        app.Run(WarmUpFrames);
        var allocated = SteadyAllocations.PerFrame(MeasuredFrames, _ => app.Run());

        output.WriteLine($"crumb menu open: {allocated:N0} B/frame allocated");
        var menu = app.Ui.Omnibar.Menu;
        Assert.NotNull(menu);
        Assert.NotEmpty(menu.Shown);
        Assert.Equal(0, allocated);
    }

    private static void OpenErrors(AppDriver app)
    {
        Errors.Report(app.AppState, new AppProblem.ResourceNotSaved("something.dll", "details"));
        app.Ui.ErrorDialog.IsOpen = true;
    }
}
