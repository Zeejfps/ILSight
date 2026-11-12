using ILSight.Core;
using ILSight.CodeViewArea;

namespace ILSight.App.Tests;

public sealed class DecompileIntoTabHandlerTests
{
    private static readonly ViewPosition AtLineTen = ViewPosition.OfCode(new CodeViewPosition(10, 0f, default, default));

    /// <summary>Cancels <paramref name="shutdown"/> at once, on this thread, as the workflow runner does as the app
    /// shuts down, so that the handler goes on here rather than through the test's posted actions.</summary>
    private static void ShutDown(CancellationTokenSource shutdown)
    {
        shutdown.Cancel();
    }

    /// <summary>The first tab of a navigation whose view commands go to <paramref name="viewCommands"/>.</summary>
    private static (Navigation Navigation, DocumentTab Tab) FirstTab(Queue<ViewCommand> viewCommands)
    {
        var navigation = new Navigation { ViewCommands = viewCommands, PreviewTabs = false };
        navigation.OpenFirstTab();
        var tab = navigation.ActiveTab ?? throw new InvalidOperationException("The first tab opened is the active one.");
        return (navigation, tab);
    }

    private static DecompilerSetup In(DecompilationLanguage language)
    {
        return new DecompilerSetup { Language = language, Options = DecompilerOptions.Default };
    }

    private static NodeContent.Code CodeOf(string title)
    {
        return new NodeContent.Code(Documents.CreateFromText(title, "// " + title));
    }

    [Fact]
    public async Task HandleAsync_Decompiled_ShowsItAndCommandsTheViewWhereToStand()
    {
        var greeter = AppStateDriver.OpenedWithoutScreen().Fixture("Greeter");
        var viewCommands = new Queue<ViewCommand>();
        var (navigation, tab) = FirstTab(viewCommands);
        var ports = new FakeDecompileIntoTabHandlerPorts();
        var load = navigation.Restore(tab, greeter, In(DecompilationLanguage.IL), AtLineTen);
        Assert.NotNull(load);
        var code = CodeOf("Greeter");

        var handling = new DecompileIntoTabHandler(ports).HandleAsync(viewCommands, load, CancellationToken.None);
        var request = Assert.Single(ports.Requests);
        Assert.IsType<TabContent.Loading>(tab.Content);
        request.Answer.SetResult(new Outcome<NodeContent>.Ok(code));
        await handling;

        Assert.Same(greeter, request.Node);
        Assert.Equal(DecompilationLanguage.IL, request.Language);
        var shown = Assert.IsType<TabContent.Code>(tab.Content);
        Assert.Same(code.Document, shown.Document);
        Assert.False(tab.Load.IsBusy);
        Assert.Equal([new ViewCommand.RestoreView(tab, AtLineTen)], viewCommands);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task HandleAsync_CorruptFile_ShowsAndReportsTheFailure()
    {
        var greeter = AppStateDriver.OpenedWithoutScreen().Fixture("Greeter");
        var viewCommands = new Queue<ViewCommand>();
        var (navigation, tab) = FirstTab(viewCommands);
        var ports = new FakeDecompileIntoTabHandlerPorts();
        var load = navigation.Restore(tab, greeter, In(DecompilationLanguage.CSharp), AtLineTen);
        Assert.NotNull(load);
        var corrupt = new BadImageFormatException("Bad IL range.");

        var handling = new DecompileIntoTabHandler(ports).HandleAsync(viewCommands, load, CancellationToken.None);
        Assert.Single(ports.Requests).Answer.SetResult(new Outcome<NodeContent>.Failed(corrupt));
        await handling;

        var failed = Assert.IsType<TabContent.Failed>(tab.Content);
        Assert.Equal("Bad IL range.", failed.Message);
        Assert.False(tab.Load.IsBusy);
        Assert.Same(corrupt, Assert.IsType<AppProblem.DecompilationFailed>(Assert.Single(ports.Reported)).Error);
        Assert.DoesNotContain(new ViewCommand.RestoreView(tab, AtLineTen), viewCommands);
    }

    [Fact]
    public async Task HandleAsync_NewerLoadStartedOnTheTab_DropsTheOlderOneAndShowsTheNewer()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var greeter = d.Fixture("Greeter");
        var farewell = d.Fixture("Farewell");
        var viewCommands = new Queue<ViewCommand>();
        var (navigation, tab) = FirstTab(viewCommands);
        var ports = new FakeDecompileIntoTabHandlerPorts();
        var handler = new DecompileIntoTabHandler(ports);
        var older = navigation.Restore(tab, greeter, In(DecompilationLanguage.CSharp), position: null);
        Assert.NotNull(older);
        var olderHandling = handler.HandleAsync(viewCommands, older, CancellationToken.None);
        var newer = navigation.Restore(tab, farewell, In(DecompilationLanguage.CSharp), position: null);
        Assert.NotNull(newer);
        var newerHandling = handler.HandleAsync(viewCommands, newer, CancellationToken.None);
        var farewellCode = CodeOf("Farewell");

        ports.Requests[0].Answer.SetResult(new Outcome<NodeContent>.Ok(CodeOf("Greeter")));
        await olderHandling;
        Assert.Same(farewell, Assert.IsType<TabContent.Loading>(tab.Content).Node);
        ports.Requests[1].Answer.SetResult(new Outcome<NodeContent>.Ok(farewellCode));
        await newerHandling;

        Assert.True(older.Work.Token.IsCancellationRequested);
        Assert.False(newer.Work.Token.IsCancellationRequested);
        Assert.Same(farewellCode.Document, Assert.IsType<TabContent.Code>(tab.Content).Document);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task HandleAsync_TabClosedMeanwhile_DropsWhatArrives()
    {
        var greeter = AppStateDriver.OpenedWithoutScreen().Fixture("Greeter");
        var viewCommands = new Queue<ViewCommand>();
        var (navigation, tab) = FirstTab(viewCommands);
        var ports = new FakeDecompileIntoTabHandlerPorts();
        var load = navigation.Restore(tab, greeter, In(DecompilationLanguage.CSharp), position: null);
        Assert.NotNull(load);
        viewCommands.Clear();

        var handling = new DecompileIntoTabHandler(ports).HandleAsync(viewCommands, load, CancellationToken.None);
        navigation.CloseTab(tab);
        Assert.Single(ports.Requests).Answer.SetResult(new Outcome<NodeContent>.Failed(new BadImageFormatException("Bad IL range.")));
        await handling;

        Assert.True(load.Work.Token.IsCancellationRequested);
        Assert.IsType<TabContent.Loading>(tab.Content);
        Assert.Empty(viewCommands);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task HandleAsync_CancelledAtShutdown_EndsCancelledShowingAndReportingNothing()
    {
        var greeter = AppStateDriver.OpenedWithoutScreen().Fixture("Greeter");
        var viewCommands = new Queue<ViewCommand>();
        var (navigation, tab) = FirstTab(viewCommands);
        var ports = new FakeDecompileIntoTabHandlerPorts();
        var load = navigation.Restore(tab, greeter, In(DecompilationLanguage.CSharp), position: null);
        Assert.NotNull(load);
        viewCommands.Clear();
        using var shutdown = new CancellationTokenSource();

        var handling = new DecompileIntoTabHandler(ports).HandleAsync(viewCommands, load, shutdown.Token);
        ShutDown(shutdown);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handling);
        Assert.IsType<TabContent.Loading>(tab.Content);
        Assert.Empty(viewCommands);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public void Restore_TabShowsTheNodeAlready_OnlyCommandsTheViewWhereToStand()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var greeter = d.Fixture("Greeter");
        d.Show(greeter);
        var tab = d.AppState.Navigation.ActiveTab;
        Assert.NotNull(tab);
        var changes = tab.Changes;

        d.TakeViewCommands();

        var load = d.AppState.Navigation.Restore(tab, greeter, d.AppState.Decompiler.Setup, AtLineTen);

        Assert.Null(load);
        Assert.False(tab.Load.IsBusy);
        Assert.Equal(changes, tab.Changes);
        Assert.Equal([new ViewCommand.RestoreView(tab, AtLineTen)], d.TakeViewCommands());
    }
}
