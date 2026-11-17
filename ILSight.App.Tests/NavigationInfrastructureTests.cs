using System.Collections.Immutable;
using ILSight.Core;

namespace ILSight.App.Tests;

/// <summary>The navigation's adapter over real assemblies: what its lookups let through.</summary>
public sealed class NavigationInfrastructureTests
{
    private static NavigationInfrastructure Create()
    {
        return new NavigationInfrastructure(new ErrorLog(), new RefusalLine(), new AssemblyList { Runtime = TestRuntime.Installed }, new BrowseSession());
    }

    private static NodePath FixtureRoot()
    {
        return new NodePath { FilePath = AssemblyPath.Of(AppStateDriver.FixturePath), Steps = ImmutableArray<NodePathStep>.Empty };
    }

    [Fact]
    public async Task LookUpPlaceAsync_TokenCancelled_ThrowsThatCancellation()
    {
        using var navigationClosed = new CancellationTokenSource();
        await navigationClosed.CancelAsync();

        var thrown = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Create().LookUpPlaceAsync(FixtureRoot(), navigationClosed.Token));

        Assert.Equal(navigationClosed.Token, thrown.CancellationToken);
    }

    [Fact]
    public async Task OpenAndFindIdAsync_MemberTheLibraryCannotRead_AnswersTheModuleUnreadableWithWhy()
    {
        using var temp = new TempDir();
        var greet = BrokenAssemblies.GetMethod(AppStateDriver.FixturePath, "Greeter", "Greet");
        var unreadablePath = BrokenAssemblies.WriteUnreadableMethodName(temp, AppStateDriver.FixturePath, greet);
        var path = AssemblyPath.Of(unreadablePath);

        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var navigation = new NavigationInfrastructure(new ErrorLog(), new RefusalLine(), list, new BrowseSession());

        var found = await navigation.OpenAndFindIdAsync([path], "M:ILSight.App.Tests.Fixtures.Greeter.Greet(System.Int32)", list.Work, CancellationToken.None);

        var unreadable = Assert.IsType<Lookup<BrowseNode>.Unreadable>(UnionAsserts.GetCase<Outcome<Lookup<BrowseNode>>.Ok>(found).Value);
        Assert.Equal(path, unreadable.Module.Module.FilePath);
        Assert.IsType<BadImageFormatException>(unreadable.Module.Why);
    }

    [Fact]
    public async Task OpenAndShowId_ModuleCorruptOnTheWay_SaysItInPlaceAsTheModulesWithNoReportOffer()
    {
        using var temp = new TempDir();
        var greet = BrokenAssemblies.GetMethod(AppStateDriver.FixturePath, "Greeter", "Greet");
        var unreadablePath = BrokenAssemblies.WriteUnreadableMethodName(temp, AppStateDriver.FixturePath, greet);
        var path = AssemblyPath.Of(unreadablePath);
        var d = AppStateDriver.WithoutScreen();
        var errors = new ErrorLog();
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var navigation = new NavigationInfrastructure(errors, new RefusalLine(), list, new BrowseSession());

        var load = await d.RunAsync(() => new OpenAndShowIdHandler(navigation).HandleAsync(d.AppState.ListSession.Navigation, d.AppState.Decompiler, [path],
            "M:ILSight.App.Tests.Fixtures.Greeter.Greet(System.Int32)", list.Work, CancellationToken.None));

        // The library's failure on the user's module is the module's: said by its message, no step and no bug report.
        var reported = Assert.IsType<AppProblem.ModuleUnreadable>(Assert.Single(errors.Entries).Problem);
        Assert.IsType<BadImageFormatException>(reported.Why);
        var text = ProblemTexts.Describe(Strings.En, reported);
        Assert.Equal(reported.Why.Message, text.Details);
        Assert.DoesNotContain(Strings.En.ErrorRecoveryReportBug, text.Details);
        Assert.Null(load);
    }

    [Fact]
    public async Task LookUpPlaceAsync_BugInTheAppsWorkOnThePathsWay_ReachesTheEdgeRecordedOnce()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var greeter = await d.FixtureAsync("Greeter");
        var place = greeter.Path;
        var errors = new ErrorLog();
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var session = new BrowseSession();
        var navigation = new NavigationInfrastructure(errors, new RefusalLine(), list, session);
        var opened = AssemblyListing.Open(list, place.FilePath);
        var root = session.EnsureRoot(opened);
        BrokenListing.Settle(session, root);

        var work = new BackgroundWork();
        new WorkflowRunner(work, errors).Start("look up a place", ct => navigation.LookUpPlaceAsync(place, ct));
        await WorkflowRunnerTests.WaitUntilEndedAsync(work);

        Assert.Equal(BrokenListing.Message, Assert.IsType<AppProblem.Unexpected>(Assert.Single(errors.Entries).Problem).Error.Message);
    }

    [Fact]
    public async Task LookUpListedPlacesAsync_TokenCancelled_ThrowsThatCancellation()
    {
        using var navigationClosed = new CancellationTokenSource();
        await navigationClosed.CancelAsync();

        var thrown = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Create().LookUpListedPlacesAsync([FixtureRoot()], navigationClosed.Token));

        Assert.Equal(navigationClosed.Token, thrown.CancellationToken);
    }
}
