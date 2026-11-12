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

    private static NodeContent.Code CodeOf(string title)
    {
        return new NodeContent.Code(Documents.CreateFromText(title, "// " + title));
    }

    [Fact]
    public async Task HandleAsync_Decompiled_ShowsItAndCommandsTheViewWhereToStand()
    {
        var greeter = AppStateDriver.OpenedWithoutScreen().Fixture("Greeter");
        var tab = new DocumentTab { Id = 1 };
        var viewCommands = new Queue<ViewCommand>();
        var ports = new FakeDecompileIntoTabHandlerPorts();
        var load = Decompilation.StartDecompiling(viewCommands, tab, greeter, DecompilationLanguage.IL, DecompilerOptions.Default,
            new Arrival(AtLineTen));
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
        Assert.False(tab.IsBusy);
        Assert.Equal([new ViewCommand.RestoreView(tab, AtLineTen)], viewCommands);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task HandleAsync_CorruptFile_ShowsAndReportsTheFailure()
    {
        var greeter = AppStateDriver.OpenedWithoutScreen().Fixture("Greeter");
        var tab = new DocumentTab { Id = 1 };
        var viewCommands = new Queue<ViewCommand>();
        var ports = new FakeDecompileIntoTabHandlerPorts();
        var load = Decompilation.StartDecompiling(viewCommands, tab, greeter, DecompilationLanguage.CSharp, DecompilerOptions.Default,
            new Arrival(AtLineTen));
        Assert.NotNull(load);
        var corrupt = new BadImageFormatException("Bad IL range.");

        var handling = new DecompileIntoTabHandler(ports).HandleAsync(viewCommands, load, CancellationToken.None);
        Assert.Single(ports.Requests).Answer.SetResult(new Outcome<NodeContent>.Failed(corrupt));
        await handling;

        var failed = Assert.IsType<TabContent.Failed>(tab.Content);
        Assert.Equal("Bad IL range.", failed.Message);
        Assert.False(tab.IsBusy);
        Assert.Same(corrupt, Assert.IsType<AppProblem.DecompilationFailed>(Assert.Single(ports.Reported)).Error);
        Assert.DoesNotContain(new ViewCommand.RestoreView(tab, AtLineTen), viewCommands);
    }

    [Fact]
    public async Task HandleAsync_NewerLoadStartedOnTheTab_DropsTheOlderOneAndShowsTheNewer()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var greeter = d.Fixture("Greeter");
        var farewell = d.Fixture("Farewell");
        var tab = new DocumentTab { Id = 1 };
        var viewCommands = new Queue<ViewCommand>();
        var ports = new FakeDecompileIntoTabHandlerPorts();
        var handler = new DecompileIntoTabHandler(ports);
        var older = Decompilation.StartDecompiling(viewCommands, tab, greeter, DecompilationLanguage.CSharp, DecompilerOptions.Default);
        Assert.NotNull(older);
        var olderHandling = handler.HandleAsync(viewCommands, older, CancellationToken.None);
        var newer = Decompilation.StartDecompiling(viewCommands, tab, farewell, DecompilationLanguage.CSharp, DecompilerOptions.Default);
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
        var tabs = new DocumentTabs();
        var tab = tabs.AddNew();
        var viewCommands = new Queue<ViewCommand>();
        var ports = new FakeDecompileIntoTabHandlerPorts();
        var load = Decompilation.StartDecompiling(viewCommands, tab, greeter, DecompilationLanguage.CSharp, DecompilerOptions.Default);
        Assert.NotNull(load);

        var handling = new DecompileIntoTabHandler(ports).HandleAsync(viewCommands, load, CancellationToken.None);
        Tabs.Remove(tabs, tab);
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
        var tab = new DocumentTab { Id = 1 };
        var viewCommands = new Queue<ViewCommand>();
        var ports = new FakeDecompileIntoTabHandlerPorts();
        var load = Decompilation.StartDecompiling(viewCommands, tab, greeter, DecompilationLanguage.CSharp, DecompilerOptions.Default);
        Assert.NotNull(load);
        using var shutdown = new CancellationTokenSource();

        var handling = new DecompileIntoTabHandler(ports).HandleAsync(viewCommands, load, shutdown.Token);
        ShutDown(shutdown);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handling);
        Assert.IsType<TabContent.Loading>(tab.Content);
        Assert.Empty(viewCommands);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public void StartDecompiling_TabShowsTheNodeAlready_OnlyCommandsTheViewWhereToStand()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var greeter = d.Fixture("Greeter");
        d.Show(greeter);
        var tab = d.AppState.Navigation.ActiveTab;
        Assert.NotNull(tab);
        var viewCommands = new Queue<ViewCommand>();
        var changes = tab.Changes;

        var load = Decompilation.StartDecompiling(viewCommands, tab, greeter, d.AppState.Language, d.AppState.DecompilerOptions, new Arrival(AtLineTen));

        Assert.Null(load);
        Assert.False(tab.IsBusy);
        Assert.Equal(changes, tab.Changes);
        Assert.Equal([new ViewCommand.RestoreView(tab, AtLineTen)], viewCommands);
    }
}
