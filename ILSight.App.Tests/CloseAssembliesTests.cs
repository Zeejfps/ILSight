using ILSight.Core;
using ILSight.Localization;
using ZGF.KeyboardModule;
using static ILSight.AssemblyClosing;
using static ILSight.Assemblies;

namespace ILSight.App.Tests;

public sealed class CloseAssembliesTests
{
    private static readonly string KeyboardPath = typeof(KeyboardKey).Assembly.Location;
    private static readonly string CorePath = typeof(AssemblyList).Assembly.Location;

    private static AppStateDriver OpenedThree()
    {
        var d = AppStateDriver.Opened();
        OpenAssembly(d.AppState, KeyboardPath);
        OpenAssembly(d.AppState, CorePath);
        d.RunUntil(() => d.AppState.Assemblies.Listed.Count == 3);
        d.Settle();
        return d;
    }

    private static LoadedAssembly Listed(AppStateDriver d, string path)
    {
        return d.AppState.Assemblies.Listed.Single(a => a.FilePath.Text == path);
    }

    private static List<string> ListedPaths(AppStateDriver d)
    {
        return d.AppState.Assemblies.Listed.Select(static a => a.FilePath.Text).ToList();
    }

    [Fact]
    public void The_dialog_lists_every_item_in_the_list_order_with_the_picked_one_ticked()
    {
        var d = OpenedThree();
        var closer = d.Ui.Closer;

        OpenCloser(closer, d.AppState.Assemblies, Listed(d, KeyboardPath));

        Assert.True(closer.IsOpen);
        Assert.Equal(ListedPaths(d), closer.Order.Select(static c => c.Assembly.FilePath.Text));
        Assert.Equal([false, true, false], closer.Order.Select(static c => c.Included));
    }

    [Fact]
    public void With_nothing_picked_nothing_starts_ticked()
    {
        var d = OpenedThree();
        var closer = d.Ui.Closer;
        var core = Listed(d, CorePath);

        OpenCloser(closer, d.AppState.Assemblies, null);
        Assert.All(closer.Order, static c => Assert.False(c.Included));

        OpenCloser(closer, d.AppState.Assemblies, core);
        Assert.Equal([core], ListToClose(closer));
    }

    [Fact]
    public void Ticking_all_shown_ticks_only_what_the_filter_shows_and_close_takes_every_ticked_one()
    {
        var d = OpenedThree();
        var closer = d.Ui.Closer;
        OpenCloser(closer, d.AppState.Assemblies, Listed(d, AppStateDriver.FixturePath));

        closer.Filter = "ILSight.Core";
        var shown = ListShown(closer);
        IncludeShown(shown, true);

        Assert.Equal([CorePath], shown.Select(static c => c.Assembly.FilePath.Text));
        Assert.Equal(true, GetAllShownIncluded(shown));
        Assert.Equal([AppStateDriver.FixturePath, CorePath], ListToClose(closer).Select(static a => a.FilePath.Text));
    }

    [Fact]
    public void Selected_only_shows_the_ticked_ones_and_clearing_the_filter_shows_all_again()
    {
        var d = OpenedThree();
        var closer = d.Ui.Closer;
        OpenCloser(closer, d.AppState.Assemblies, Listed(d, KeyboardPath));

        closer.SelectedOnly = true;
        Assert.Equal([KeyboardPath], ListShown(closer).Select(static c => c.Assembly.FilePath.Text));

        ClearFilter(closer);
        Assert.Equal(3, ListShown(closer).Count);
    }

    [Fact]
    public void A_shift_click_gives_the_range_from_the_anchor_the_clicked_tick()
    {
        var d = OpenedThree();
        var closer = d.Ui.Closer;
        OpenCloser(closer, d.AppState.Assemblies, null);
        var shown = ListShown(closer);

        shown[0].Included = true;
        TickClicked(closer, shown, shown[0], extend: false);
        shown[2].Included = true;
        TickClicked(closer, shown, shown[2], extend: true);

        Assert.Equal([true, true, true], closer.Order.Select(static c => c.Included));
        Assert.Equal(3, CountIncluded(closer));
    }

    [Fact]
    public void Closing_takes_the_ticked_items_off_the_list_and_leaves_the_rest()
    {
        var d = OpenedThree();
        var closer = d.Ui.Closer;
        OpenCloser(closer, d.AppState.Assemblies, Listed(d, KeyboardPath));
        closer.Order[2].Included = true;

        CloseAssemblies(d.AppState, ListToClose(closer));

        Assert.Equal([AppStateDriver.FixturePath], ListedPaths(d));
    }

    [Fact]
    public void A_list_changed_while_the_dialog_is_up_is_shown_with_the_ticks_kept()
    {
        var d = AppStateDriver.Opened();
        OpenAssembly(d.AppState, KeyboardPath);
        d.RunUntil(() => d.AppState.Assemblies.Listed.Count == 2);
        var closer = d.Ui.Closer;
        OpenCloser(closer, d.AppState.Assemblies, Listed(d, KeyboardPath));

        OpenAssembly(d.AppState, CorePath);
        d.RunUntil(() => d.AppState.Assemblies.Listed.Count == 3);
        Update(closer, d.AppState.Assemblies);

        Assert.Equal(ListedPaths(d), closer.Order.Select(static c => c.Assembly.FilePath.Text));
        Assert.Equal([KeyboardPath], ListToClose(closer).Select(static a => a.FilePath.Text));
    }

    [Fact]
    public void The_trash_button_opens_the_dialog_and_its_close_button_closes_the_ticked_ones()
    {
        var app = AppDriver.Opened();
        OpenAssembly(app.AppState, KeyboardPath);
        app.Frames.WaitUntil(() => app.AppState.Assemblies.Listed.Count == 2, Patience);
        app.Run(2);

        app.Frames.ClickOn(Selector.AtPath("dock/assemblies/close-assembly")).Frame(2);
        var closer = app.Ui.Closer;
        Assert.True(closer.IsOpen);
        closer.Order[1].Included = true;
        app.Run(2);
        app.Frames.ClickOn(Selector.AtPath("close-ticked")).Frame(2);
        app.Frames.WaitUntil(() => app.AppState.Assemblies.Listed.Count == 1, Patience);

        Assert.False(closer.IsOpen);
        Assert.Equal(AppStateDriver.FixturePath, Assert.Single(app.AppState.Assemblies.Listed).FilePath.Text);
    }
}
