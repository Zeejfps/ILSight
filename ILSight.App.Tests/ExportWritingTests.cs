using ILSight.Core;
using ILSight.ExportArea;

namespace ILSight.App.Tests;

public sealed class ExportWritingTests
{
    private static readonly string Folder = Path.Combine(Path.GetTempPath(), "exported");

    private static async Task<ExportWork.CodeFile> GreeterCodeAsync(AppStateDriver d)
    {
        return new ExportWork.CodeFile
        {
            Node = await d.FixtureAsync("Greeter"),
            Language = DecompilationLanguage.CSharp,
            Options = DecompilerOptions.Default,
            File = Path.Combine(Folder, "Greeter.cs"),
        };
    }

    [Fact]
    public async Task HandleAsync_Written_ReportsWhatItWroteAndEndsItsTurn()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var work = await GreeterCodeAsync(d);
        var queue = new ExportQueue();
        var ticket = queue.Enter(work.Job);
        var ports = new FakeExportWritingPorts { CodeFile = new ExportOutcome<string>.Done(work.File) };
        var finished = new List<ExportReport>();

        await new ExportWriting(ports).HandleAsync(finished, new Queue<ViewCommand>(), work, ticket, CancellationToken.None);

        var report = Assert.Single(finished);
        Assert.Equal(ticket.Run, report.Run);
        Assert.Equal(ExportKind.CSharpFile, report.Job.Kind);
        Assert.Equal(Folder, report.Folder);
        Assert.Equal([work.File], Assert.IsType<ExportEnd.Finished>(report.End).Written);
        Assert.Equal(TimeSpan.FromSeconds(2), report.Elapsed);
        Assert.True(report.FolderExists);
        Assert.False(queue.IsRunning);
    }

    [Fact]
    public async Task HandleAsync_Stopped_ReportsItCancelled()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var work = await GreeterCodeAsync(d);
        var queue = new ExportQueue();
        var ticket = queue.Enter(work.Job);
        var ports = new FakeExportWritingPorts { CodeFile = new ExportOutcome<string>.Stopped(), AreFoldersThere = false };
        var finished = new List<ExportReport>();

        await new ExportWriting(ports).HandleAsync(finished, new Queue<ViewCommand>(), work, ticket, CancellationToken.None);

        var report = Assert.Single(finished);
        Assert.IsType<ExportEnd.Cancelled>(report.End);
        Assert.False(report.FolderExists);
        Assert.False(queue.IsRunning);
    }

    [Fact]
    public async Task HandleAsync_Failed_ReportsWhyAndWhatItWasExporting()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var work = await GreeterCodeAsync(d);
        var queue = new ExportQueue();
        var ticket = queue.Enter(work.Job);
        var error = new IOException("Disk full.");
        var ports = new FakeExportWritingPorts { CodeFile = new ExportOutcome<string>.Failed(error) };
        var finished = new List<ExportReport>();

        await new ExportWriting(ports).HandleAsync(finished, new Queue<ViewCommand>(), work, ticket, CancellationToken.None);

        var failed = Assert.IsType<ExportEnd.Failed>(Assert.Single(finished).End);
        Assert.Equal("Greeter", failed.Subject);
        Assert.Same(error, failed.Error);
    }

    [Fact]
    public async Task HandleAsync_WithdrawnBeforeItsTurn_WritesNothing()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var work = await GreeterCodeAsync(d);
        var queue = new ExportQueue();
        var running = queue.Enter(TestJobs.Named("Running"));
        var ticket = queue.Enter(work.Job);
        var ports = new FakeExportWritingPorts();
        var finished = new List<ExportReport>();
        queue.Withdraw(ticket.Run);

        await new ExportWriting(ports).HandleAsync(finished, new Queue<ViewCommand>(), work, ticket, CancellationToken.None);

        Assert.Empty(finished);
        Assert.Empty(ports.Asked);
        Assert.Equal(running.Run, queue.Running?.Export.Run);
    }

    [Fact]
    public async Task HandleAsync_DiagramWritten_OpensItsPage()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var assembly = Assert.IsType<LoadedModule>((await d.NodeAsync()).Assembly);
        var work = new ExportWork.Diagram { Assembly = assembly, Folder = Exports.GetDiagramFolder(assembly, Folder) };
        var page = Path.Combine(work.Folder, "index.html");
        var queue = new ExportQueue();
        var ticket = queue.Enter(work.Job);
        var ports = new FakeExportWritingPorts { Diagram = new ExportOutcome<string>.Done(page) };
        var finished = new List<ExportReport>();
        var viewCommands = new Queue<ViewCommand>();

        await new ExportWriting(ports).HandleAsync(finished, viewCommands, work, ticket, CancellationToken.None);

        Assert.Equal(page, Assert.IsType<ViewCommand.OpenFile>(Assert.Single(viewCommands)).Path);
        Assert.Equal([page], Assert.IsType<ExportEnd.Finished>(Assert.Single(finished).End).Written);
    }
}
