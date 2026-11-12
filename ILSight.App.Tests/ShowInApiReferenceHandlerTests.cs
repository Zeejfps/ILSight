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

        await new ShowInApiReferenceHandler(reference).HandleAsync(d.AppState.ViewCommands, d.Fixture("Greeter").Definition, CancellationToken.None);

        var opened = Assert.IsType<ViewCommand.OpenWebPage>(Assert.Single(d.TakeViewCommands()));
        Assert.Equal("https://learn.microsoft.com/dotnet/api/greeter", opened.Url);
        Assert.Empty(reference.Reported);
    }

    [Fact]
    public async Task HandleAsync_NoPageOrLookupFailed_ReportsThereIsNone()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        d.TakeViewCommands();
        var hidden = d.Fixture("Hidden").Definition;
        var none = new FakeShowInApiReferenceHandlerPorts { Page = new Outcome<Lookup<string>>.Ok(Lookup.FromNullable<string>(null)) };
        var failed = new FakeShowInApiReferenceHandlerPorts { Page = new Outcome<Lookup<string>>.Failed(new IOException("unreadable")) };

        await new ShowInApiReferenceHandler(none).HandleAsync(d.AppState.ViewCommands, hidden, CancellationToken.None);
        await new ShowInApiReferenceHandler(failed).HandleAsync(d.AppState.ViewCommands, hidden, CancellationToken.None);

        Assert.Equal("Hidden", Assert.IsType<AppProblem.NoApiReferencePage>(Assert.Single(none.Reported)).Name);
        Assert.Equal("Hidden", Assert.IsType<AppProblem.NoApiReferencePage>(Assert.Single(failed.Reported)).Name);
        Assert.Empty(d.TakeViewCommands());
    }
}
