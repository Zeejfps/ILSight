
namespace ILSight.App.Tests;

public sealed class SaveResourceHandlerTests
{
    [Fact]
    public void Handle_Saved_ReportsNothing()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var ports = new FakeSaveResourceHandlerPorts { Answer = new Outcome.Done() };

        new SaveResourceHandler(ports).Handle(d.Node("Resources", "data.json"), "data.json");

        Assert.Empty(ports.Reported);
    }

    [Fact]
    public void Handle_NotWritten_ReportsTheResourceAndWhy()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var ports = new FakeSaveResourceHandlerPorts { Answer = new Outcome.Failed(new IOException("Disk full.")) };

        new SaveResourceHandler(ports).Handle(d.Node("Resources", "data.json"), "data.json");

        var problem = Assert.IsType<AppProblem.ResourceNotSaved>(Assert.Single(ports.Reported));
        Assert.Equal("data.json", problem.Resource);
        Assert.Equal("Disk full.", problem.Reason);
    }
}
