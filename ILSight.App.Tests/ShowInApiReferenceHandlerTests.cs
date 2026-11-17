using ILSight.Core;

namespace ILSight.App.Tests;

public sealed class ShowInApiReferenceHandlerTests
{
    [Fact]
    public async Task HandleAsync_PageFound_HasTheViewOpenIt()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        d.TakeViewCommands();
        var reference = new FakeShowInApiReferenceHandlerPorts { Page = new Outcome<Lookup<string>>.Ok(Lookup.FromNullable<string>("https://learn.microsoft.com/dotnet/api/greeter")) };

        var greeterNode = await d.FixtureAsync("Greeter");
        var greeter = greeterNode.Definition;
        await d.RunAsync(() => new ShowInApiReferenceHandler(reference).HandleAsync(d.AppState.ViewCommands, greeter, CancellationToken.None));

        var viewCommands = d.TakeViewCommands();
        var opened = Assert.IsType<ViewCommand.OpenWebPage>(Assert.Single(viewCommands));
        Assert.Equal("https://learn.microsoft.com/dotnet/api/greeter", opened.Url);
        Assert.Empty(reference.Reported);
    }

    [Fact]
    public async Task HandleAsync_NoPageRefusesItButALookupThatFailedIsReported()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        d.TakeViewCommands();
        var hiddenNode = await d.FixtureAsync("Hidden");
        var hidden = hiddenNode.Definition;
        var none = new FakeShowInApiReferenceHandlerPorts { Page = new Outcome<Lookup<string>>.Ok(Lookup.FromNullable<string>(null)) };
        var failed = new FakeShowInApiReferenceHandlerPorts { Page = new Outcome<Lookup<string>>.Failed(new IOException("unreadable")) };

        await d.RunAsync(() => new ShowInApiReferenceHandler(none).HandleAsync(d.AppState.ViewCommands, hidden, CancellationToken.None));
        await d.RunAsync(() => new ShowInApiReferenceHandler(failed).HandleAsync(d.AppState.ViewCommands, hidden, CancellationToken.None));

        Assert.Equal([new Refusal.NoApiReferencePage { Name = "Hidden" }], none.Refused);
        Assert.Empty(failed.Refused);
        Assert.Empty(none.Reported);
        Assert.Equal("unreadable", UnionAsserts.GetCase<AppProblem.Unresolved>(Assert.Single(failed.Reported)).Error.Message);
        var viewCommands = d.TakeViewCommands();
        Assert.Empty(viewCommands);
    }
}
