using ILSight.Core;

namespace ILSight.App.Tests;

public sealed class ShowInApiReferenceHandlerTests
{
    /// <summary>Finds <see cref="Page"/> for any reference; keeps the problems reported.</summary>
    private sealed class FakeApiReference : ShowInApiReferenceHandler.IPorts
    {
        public required Outcome<string?> Page { get; init; }

        public List<AppProblem> Reported { get; } = [];

        public void ReportError(AppProblem problem)
        {
            Reported.Add(problem);
        }

        public Task<Outcome<string?>> LookUpApiReferencePageAsync(CodeReference.Entity reference, CancellationToken ct)
        {
            return Task.FromResult(Page);
        }
    }

    [Fact]
    public async Task HandleAsync_PageFound_HasTheViewOpenIt()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        d.TakeViewCommands();
        var reference = new FakeApiReference { Page = new Outcome<string?>.Ok("https://learn.microsoft.com/dotnet/api/greeter") };

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
        var none = new FakeApiReference { Page = new Outcome<string?>.Ok(null) };
        var failed = new FakeApiReference { Page = new Outcome<string?>.Failed(new IOException("unreadable")) };

        await new ShowInApiReferenceHandler(none).HandleAsync(d.AppState.ViewCommands, hidden, CancellationToken.None);
        await new ShowInApiReferenceHandler(failed).HandleAsync(d.AppState.ViewCommands, hidden, CancellationToken.None);

        Assert.Equal("Hidden", Assert.IsType<AppProblem.NoApiReferencePage>(Assert.Single(none.Reported)).Name);
        Assert.Equal("Hidden", Assert.IsType<AppProblem.NoApiReferencePage>(Assert.Single(failed.Reported)).Name);
        Assert.Empty(d.TakeViewCommands());
    }
}
