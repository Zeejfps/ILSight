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

        var greeter = (await d.FixtureAsync("Greeter")).Definition;
        await d.RunAsync(() => new ShowInApiReferenceHandler(reference).HandleAsync(d.AppState.ViewCommands, greeter, CancellationToken.None));

        var opened = Assert.IsType<ViewCommand.OpenWebPage>(Assert.Single(d.TakeViewCommands()));
        Assert.Equal("https://learn.microsoft.com/dotnet/api/greeter", opened.Url);
        Assert.Empty(reference.Reported);
    }

    [Fact]
    public async Task HandleAsync_NoPageOrLookupFailed_ReportsThereIsNone()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        d.TakeViewCommands();
        var hidden = (await d.FixtureAsync("Hidden")).Definition;
        var none = new FakeShowInApiReferenceHandlerPorts { Page = new Outcome<Lookup<string>>.Ok(Lookup.FromNullable<string>(null)) };
        var failed = new FakeShowInApiReferenceHandlerPorts { Page = new Outcome<Lookup<string>>.Failed(new IOException("unreadable")) };

        await d.RunAsync(() => new ShowInApiReferenceHandler(none).HandleAsync(d.AppState.ViewCommands, hidden, CancellationToken.None));
        await d.RunAsync(() => new ShowInApiReferenceHandler(failed).HandleAsync(d.AppState.ViewCommands, hidden, CancellationToken.None));

        Assert.Equal([new Refusal.NoApiReferencePage { Name = "Hidden" }], none.Refused);
        Assert.Equal([new Refusal.NoApiReferencePage { Name = "Hidden" }], failed.Refused);
        Assert.Empty(none.Reported);
        Assert.Empty(failed.Reported);
        Assert.Empty(d.TakeViewCommands());
    }
}
