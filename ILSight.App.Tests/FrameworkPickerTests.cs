using ILSight.Core;
using ILSight.FrameworkArea;
using ILSight.DiagramArea;
using static ILSight.App.Tests.TestPaths;

namespace ILSight.App.Tests;

public sealed class FrameworkPickerTests
{
    private static FrameworkPicker CreatePicker(FakeFrameworkBrowserPorts runtime)
    {
        return new FrameworkPicker { Browser = new FrameworkBrowser(runtime) };
    }

    /// <summary>Opens <paramref name="picker"/> and runs the scan it starts, if any, as the edge does.</summary>
    private static Task OpenAsync(FrameworkPicker picker)
    {
        var scan = picker.Open();
        if (scan is null)
        {
            return Task.CompletedTask;
        }

        return picker.Browser.ScanAsync(scan, scan.Token);
    }

    [Fact]
    public async Task A_failed_scan_ends_loading_with_an_error_and_reports_it()
    {
        var runtime = new FakeFrameworkBrowserPorts { Failure = new InvalidDataException("no runtime") };
        var picker = CreatePicker(runtime);

        await OpenAsync(picker);

        Assert.Contains("no runtime", Assert.IsType<RuntimeScan.Failed>(picker.Browser.Scan).Error);
        Assert.Single(runtime.Reported);
    }

    [Fact]
    public async Task Opening_again_scans_again_only_after_a_failed_scan()
    {
        var runtime = new FakeFrameworkBrowserPorts { Failure = new InvalidDataException("no runtime") };
        var picker = CreatePicker(runtime);
        await OpenAsync(picker);
        picker.IsOpen = false;

        runtime.Failure = null;
        await OpenAsync(picker);
        await OpenAsync(picker);

        Assert.IsType<RuntimeScan.Scanned>(picker.Browser.Scan);
        Assert.Equal(2, runtime.Scans);
        Assert.Single(runtime.Reported);
        Assert.True(picker.IsOpen);
    }

    [Fact]
    public async Task Opening_again_after_a_failed_scan_shows_it_scanning_from_that_frame()
    {
        var runtime = new FakeFrameworkBrowserPorts { Failure = new InvalidDataException("no runtime") };
        var picker = CreatePicker(runtime);
        await OpenAsync(picker);
        picker.IsOpen = false;

        runtime.Failure = null;
        var scan = picker.Open();
        var shownAtOnce = picker.Browser.Scan;
        Assert.NotNull(scan);
        await picker.Browser.ScanAsync(scan, scan.Token);

        Assert.IsType<RuntimeScan.Scanning>(shownAtOnce);
        Assert.IsType<RuntimeScan.Scanned>(picker.Browser.Scan);
    }

    [Fact]
    public async Task The_filter_takes_focus_once_each_time_the_dialog_opens()
    {
        var picker = CreatePicker(new FakeFrameworkBrowserPorts());

        await OpenAsync(picker);
        var first = picker.Browser.TryTakeFilterFocus();
        var second = picker.Browser.TryTakeFilterFocus();
        await OpenAsync(picker);

        Assert.True(first);
        Assert.False(second);
        Assert.True(picker.Browser.TryTakeFilterFocus());
    }

    [Fact]
    public async Task Filtering_keeps_only_the_picked_out_assemblies_still_shown()
    {
        var assemblies = new[]
        {
            new RuntimeAssembly("System.Net.Http", "Microsoft.NETCore.App", FakePath("a.dll")),
            new RuntimeAssembly("System.Net.Sockets", "Microsoft.NETCore.App", FakePath("b.dll")),
            new RuntimeAssembly("System.Text.Json", "Microsoft.NETCore.App", FakePath("c.dll")),
        };
        var picker = CreatePicker(new FakeFrameworkBrowserPorts { Assemblies = assemblies });
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        await OpenAsync(picker);
        picker.Update(list);

        picker.Browser.Selection.SelectAll(picker.Browser.Filtered);
        picker.Filter = "Net";
        picker.Update(list);

        Assert.Equal([assemblies[0], assemblies[1]], picker.Browser.ListSelected());
    }

    [Fact]
    public async Task Assemblies_already_open_are_not_offered()
    {
        var open = new RuntimeAssembly("Fixtures", "Microsoft.NETCore.App", PathOf(AppStateDriver.FixturePath));
        var other = new RuntimeAssembly("System.Text.Json", "Microsoft.NETCore.App", FakePath("c.dll"));
        var picker = CreatePicker(new FakeFrameworkBrowserPorts { Assemblies = [open, other] });
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        await OpenAsync(picker);
        picker.Update(list);
        Assert.Equal([open, other], picker.Browser.Filtered);

        list.Open(AppStateDriver.FixturePath);
        picker.Update(list);

        Assert.Equal([other], picker.Browser.Filtered);
    }

    [Fact]
    public void OpenFromFramework_PickedFromTheFileMenu_ShowsTheDialogThatFrameThenScansInTheBackground()
    {
        var app = new AppDriver();
        app.Settle();

        app.Frames.ClickOn(Selector.WithText(Strings.En.MenuFile));
        app.Frames.ClickOn(Selector.OfRole(SemanticRole.MenuItem, Strings.En.FileOpenFromFramework));
        var shownThatFrame = app.Frames.Drew(Strings.En.PickerTitle);
        app.Settle();

        Assert.True(shownThatFrame);
        Assert.True(app.Ui.Picker.IsOpen);
        Assert.IsType<RuntimeScan.Scanned>(app.Ui.Picker.Browser.Scan);
        Assert.Empty(app.AppState.Errors.Entries);
    }

    [Fact]
    public void Only_an_open_tab_can_be_active_or_the_preview()
    {
        var tabs = new DocumentTabs();
        var closed = new DocumentTab(1, new TabDocument());

        Assert.Throws<InvalidOperationException>(() => Tabs.MakeActive(tabs, closed));
        Assert.Throws<InvalidOperationException>(() => Tabs.MakePreview(tabs, closed));
    }

    [Fact]
    public void Closing_a_tab_stops_it_being_active_or_the_preview_and_counts_on_upward()
    {
        var tabs = new DocumentTabs();
        var tab = tabs.AddNew();
        Tabs.MakeActive(tabs, tab);
        Tabs.MakePreview(tabs, tab);
        var before = tabs.Changes;

        Tabs.Remove(tabs, tab);

        Assert.Null(tabs.Active);
        Assert.Null(tabs.Preview);
        Assert.True(tabs.Changes > before);
    }

    [Fact]
    public void Showing_hidden_members_counts_as_a_change_only_when_it_is_one()
    {
        var view = new DiagramViewState();
        var before = view.Changes;

        Diagrams.ShowHiddenMembers(view, false);
        var unchanged = view.Changes;
        Diagrams.ShowHiddenMembers(view, true);
        Diagrams.ShowHiddenMembers(view, true);

        Assert.Equal(before, unchanged);
        Assert.Equal(before + 1, view.Changes);
        Assert.True(view.ShowsHiddenMembers);
    }
}
