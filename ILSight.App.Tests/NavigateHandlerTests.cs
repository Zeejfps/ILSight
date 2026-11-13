using System.Reflection.Metadata.Ecma335;
using ILSight.Core;

namespace ILSight.App.Tests;

public sealed class NavigateHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReferenceFound_ShowsItAndRecordsItInTheHistory()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var appState = d.AppState;
        var farewell = await d.FixtureAsync("Farewell");
        var reference = farewell.Definition;
        var resolvingWhileLooking = false;
        var lookup = new FakeNavigateHandlerPorts { Found = new Outcome<Lookup<BrowseNode>>.Ok(Lookup.FromNullable<BrowseNode>(farewell)), WhileLooking = () => resolvingWhileLooking = appState.IsResolving };

        var load = await new NavigateHandler(lookup).HandleAsync(appState, reference, appState.Navigation.BeginResolving(inNewTab: false), CancellationToken.None);

        Assert.Same(reference, Assert.Single(lookup.LookedUp));
        Assert.Same(farewell, appState.Navigation.ActiveTab?.Node);
        var active = appState.Navigation.ActiveTab;
        Assert.NotNull(active);
        Assert.Same(active, load?.Tab);
        Assert.Equal(active.Location, Assert.IsType<HistoryEntry.Node>(appState.Navigation.HistoryEntries[appState.Navigation.HistoryIndex]).Place);
        Assert.True(resolvingWhileLooking);
        Assert.False(appState.IsResolving);
        Assert.Empty(lookup.Reported);
    }

    [Fact]
    public async Task HandleAsync_ReferenceLeadsNowhereOrLookupFailed_ReportsItUnresolved()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var shown = d.AppState.Navigation.ActiveTab?.Node;
        Assert.NotNull(shown);
        var reference = (await d.FixtureAsync("Farewell")).Definition;
        var error = new IOException("unreadable");
        var missing = new FakeNavigateHandlerPorts { Found = new Outcome<Lookup<BrowseNode>>.Ok(Lookup.FromNullable<BrowseNode>(null)) };
        var failed = new FakeNavigateHandlerPorts { Found = new Outcome<Lookup<BrowseNode>>.Failed(error) };

        var missingLoad = await new NavigateHandler(missing).HandleAsync(d.AppState, reference, d.AppState.Navigation.BeginResolving(inNewTab: false), CancellationToken.None);
        var failedLoad = await new NavigateHandler(failed).HandleAsync(d.AppState, reference, d.AppState.Navigation.BeginResolving(inNewTab: false), CancellationToken.None);

        var notThere = Assert.IsType<AppProblem.Unresolved>(Assert.Single(missing.Reported));
        Assert.Same(reference, notThere.Reference);
        Assert.Null(notThere.Error);
        Assert.Same(error, Assert.IsType<AppProblem.Unresolved>(Assert.Single(failed.Reported)).Error);
        Assert.Same(shown, d.AppState.Navigation.ActiveTab?.Node);
        Assert.False(d.AppState.IsResolving);
        Assert.Null(missingLoad);
        Assert.Null(failedLoad);
    }

    [Fact]
    public async Task HandleAsync_SomethingElseAskedForMeanwhile_NeitherShowsNorReports()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var appState = d.AppState;
        var shown = appState.Navigation.ActiveTab?.Node;
        Assert.NotNull(shown);
        var farewell = await d.FixtureAsync("Farewell");
        var found = new FakeNavigateHandlerPorts { Found = new Outcome<Lookup<BrowseNode>>.Ok(Lookup.FromNullable<BrowseNode>(farewell)), WhileLooking = () => appState.Navigation.BeginRequest() };
        var missing = new FakeNavigateHandlerPorts { Found = new Outcome<Lookup<BrowseNode>>.Ok(Lookup.FromNullable<BrowseNode>(null)), WhileLooking = () => appState.Navigation.BeginRequest() };

        var foundLoad = await new NavigateHandler(found).HandleAsync(appState, farewell.Definition, appState.Navigation.BeginResolving(inNewTab: false), CancellationToken.None);
        var missingLoad = await new NavigateHandler(missing).HandleAsync(appState, farewell.Definition, appState.Navigation.BeginResolving(inNewTab: false), CancellationToken.None);

        Assert.Same(shown, appState.Navigation.ActiveTab?.Node);
        Assert.Null(foundLoad);
        Assert.Empty(found.Reported);
        Assert.Empty(missing.Reported);
        Assert.Null(missingLoad);
    }

    [Fact]
    public void HandleAsync_ReferenceIntoAModuleWhoseTypesCannotBeRead_ReportsWhereTheUserIsWhichModuleAndWhy()
    {
        using var temp = new TempDir();
        var path = BrokenAssemblies.WriteUnreadableTypeSystem(temp, AppStateDriver.FixturePath);
        var d = AppStateDriver.OpenedWithoutScreen();
        var shown = d.AppState.Navigation.ActiveTab?.Node;
        Assert.NotNull(shown);
        var reference = CodeReferences.CreateEntity(EntityKind.Type, "Greeter", AssemblyPath.Of(path), MetadataTokens.TypeDefinitionHandle(2));

        d.Start((a, ct) => a.NavigateHandler.HandleAsync(a, reference, a.Navigation.BeginResolving(inNewTab: false), ct));
        d.Settle();

        var reported = Assert.Single(d.AppState.Errors.Entries);
        var problem = Assert.IsType<AppProblem.ModuleUnreadable>(reported.Problem);
        Assert.Equal("UnreadableTypes.dll", problem.Module);
        Assert.NotEmpty(problem.Why.Message);
        Assert.StartsWith("UnreadableTypes.dll couldn't be read — ", reported.Words().Summary);
        Assert.Same(shown, d.AppState.Navigation.ActiveTab?.Node);
        Assert.False(d.AppState.IsResolving);
    }

    [Fact]
    public async Task HandleAsync_ReferenceIntoAModuleThatCouldNotBeRead_ReportsTheModuleNotTheReference()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var greeter = await d.FixtureAsync("Greeter");
        var module = d.AppState.Assemblies.Assemblies.OfType<LoadedModule>().Single(a => a.FilePath.Text == AppStateDriver.FixturePath);
        var why = new BadImageFormatException("Bad namespace.");
        var unreadable = new Lookup<BrowseNode>.Unreadable { Module = new UnreadableModule { Module = module, Why = why } };
        var lookup = new FakeNavigateHandlerPorts { Found = new Outcome<Lookup<BrowseNode>>.Ok(unreadable) };

        var load = await new NavigateHandler(lookup).HandleAsync(d.AppState, greeter.Definition, d.AppState.Navigation.BeginResolving(inNewTab: false), CancellationToken.None);

        var problem = Assert.IsType<AppProblem.ModuleUnreadable>(Assert.Single(lookup.Reported));
        Assert.Equal(module.FilePath.FileName, problem.Module);
        Assert.Same(why, problem.Why);
        Assert.Null(load);
        Assert.False(d.AppState.IsResolving);
    }
}
