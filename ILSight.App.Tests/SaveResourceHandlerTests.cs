
namespace ILSight.App.Tests;

public sealed class SaveResourceHandlerTests
{
    [Fact]
    public async Task Handle_Saved_ReportsNothing()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var ports = new FakeSaveResourceHandlerPorts { Answer = new ResourceSave.Saved() };

        await new SaveResourceHandler(ports).HandleAsync(await d.NodeAsync("Resources", "data.json"), "data.json", CancellationToken.None);

        Assert.Empty(ports.Reported);
        Assert.Empty(ports.Refused);
    }

    [Fact]
    public async Task Handle_NotWritten_ReportsTheResourceAndWhy()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var ports = new FakeSaveResourceHandlerPorts { Answer = new ResourceSave.Failed { Error = new IOException("Disk full.") } };

        await new SaveResourceHandler(ports).HandleAsync(await d.NodeAsync("Resources", "data.json"), "data.json", CancellationToken.None);

        var problem = Assert.IsType<AppProblem.ResourceNotSaved>(Assert.Single(ports.Reported));
        Assert.Equal("data.json", problem.Resource);
        Assert.Equal("Disk full.", problem.Reason);
        Assert.Empty(ports.Refused);
    }

    [Fact]
    public async Task Handle_AssemblyClosed_SaysWhyAndReportsNothing()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var ports = new FakeSaveResourceHandlerPorts { Answer = new ResourceSave.Closed { Assembly = "Shop.dll" } };

        await new SaveResourceHandler(ports).HandleAsync(await d.NodeAsync("Resources", "data.json"), "data.json", CancellationToken.None);

        Assert.Equal([new Refusal.ResourceNotSaved { Resource = "data.json", Assembly = "Shop.dll" }], ports.Refused);
        Assert.Empty(ports.Reported);
    }
}
