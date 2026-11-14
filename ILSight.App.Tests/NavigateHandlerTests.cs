using System.Reflection.Metadata.Ecma335;
using ILSight.Core;

namespace ILSight.App.Tests;

public sealed class NavigateHandlerTests
{
    [Fact]
    public void FailResolving_TheLookupsWorkflowFailed_StopsResolvingUnlessANewerRequestBegan()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var navigation = d.AppState.ListSession.Navigation;
        var failing = navigation.BeginResolving(inNewTab: false);

        navigation.FailResolving(failing);
        var stopped = navigation.IsResolving;
        var older = navigation.BeginResolving(inNewTab: false);
        var newer = navigation.BeginResolving(inNewTab: false);
        navigation.FailResolving(older);

        Assert.False(stopped);
        Assert.True(navigation.IsResolving);
        navigation.FailResolving(newer);
        Assert.False(navigation.IsResolving);
    }

    [Fact]
    public async Task HandleAsync_ReferenceFound_ShowsItAndRecordsItInTheHistory()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var appState = d.AppState;
        var farewell = await d.FixtureAsync("Farewell");
        var reference = farewell.Definition;
        var resolvingWhileLooking = false;
        var lookup = new FakeNavigateHandlerPorts { Found = new Outcome<Lookup<BrowseNode>>.Ok(Lookup.FromNullable<BrowseNode>(farewell)), WhileLooking = () => resolvingWhileLooking = appState.ListSession.Navigation.IsResolving };

        var load = await d.RunAsync(() => new NavigateHandler(lookup).HandleAsync(appState.ListSession.Navigation, appState.Decompiler, reference, appState.ListSession.Navigation.BeginResolving(inNewTab: false), CancellationToken.None));

        Assert.Same(reference, Assert.Single(lookup.LookedUp));
        Assert.Same(farewell, appState.ListSession.Navigation.ActiveTab?.Node);
        var active = appState.ListSession.Navigation.ActiveTab;
        Assert.NotNull(active);
        Assert.Same(active, load?.Tab);
        Assert.Equal(active.Location, Assert.IsType<HistoryEntry.Node>(appState.ListSession.Navigation.HistoryEntries[appState.ListSession.Navigation.HistoryIndex]).Place);
        Assert.True(resolvingWhileLooking);
        Assert.False(appState.ListSession.Navigation.IsResolving);
        Assert.Empty(lookup.Reported);
    }

    [Fact]
    public async Task HandleAsync_ReferenceLeadsNowhereOrLookupFailed_ReportsItUnresolved()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var shown = d.AppState.ListSession.Navigation.ActiveTab?.Node;
        Assert.NotNull(shown);
        var reference = (await d.FixtureAsync("Farewell")).Definition;
        var error = new IOException("unreadable");
        var missing = new FakeNavigateHandlerPorts { Found = new Outcome<Lookup<BrowseNode>>.Ok(Lookup.FromNullable<BrowseNode>(null)) };
        var failed = new FakeNavigateHandlerPorts { Found = new Outcome<Lookup<BrowseNode>>.Failed(error) };

        var missingLoad = await d.RunAsync(() => new NavigateHandler(missing).HandleAsync(d.AppState.ListSession.Navigation, d.AppState.Decompiler, reference, d.AppState.ListSession.Navigation.BeginResolving(inNewTab: false), CancellationToken.None));
        var failedLoad = await d.RunAsync(() => new NavigateHandler(failed).HandleAsync(d.AppState.ListSession.Navigation, d.AppState.Decompiler, reference, d.AppState.ListSession.Navigation.BeginResolving(inNewTab: false), CancellationToken.None));

        var notThere = Assert.IsType<Refusal.Unresolved>(Assert.Single(missing.Refused));
        Assert.Same(reference, notThere.Reference);
        Assert.Empty(missing.Reported);
        Assert.Same(error, Assert.IsType<AppProblem.Unresolved>(Assert.Single(failed.Reported)).Error);
        Assert.Same(shown, d.AppState.ListSession.Navigation.ActiveTab?.Node);
        Assert.False(d.AppState.ListSession.Navigation.IsResolving);
        Assert.Null(missingLoad);
        Assert.Null(failedLoad);
    }

    [Fact]
    public async Task HandleAsync_SomethingElseAskedForMeanwhile_NeitherShowsNorReports()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var appState = d.AppState;
        var shown = appState.ListSession.Navigation.ActiveTab?.Node;
        Assert.NotNull(shown);
        var farewell = await d.FixtureAsync("Farewell");
        var found = new FakeNavigateHandlerPorts { Found = new Outcome<Lookup<BrowseNode>>.Ok(Lookup.FromNullable<BrowseNode>(farewell)), WhileLooking = () => appState.ListSession.Navigation.BeginRequest() };
        var missing = new FakeNavigateHandlerPorts { Found = new Outcome<Lookup<BrowseNode>>.Ok(Lookup.FromNullable<BrowseNode>(null)), WhileLooking = () => appState.ListSession.Navigation.BeginRequest() };

        var foundLoad = await d.RunAsync(() => new NavigateHandler(found).HandleAsync(appState.ListSession.Navigation, appState.Decompiler, farewell.Definition, appState.ListSession.Navigation.BeginResolving(inNewTab: false), CancellationToken.None));
        var missingLoad = await d.RunAsync(() => new NavigateHandler(missing).HandleAsync(appState.ListSession.Navigation, appState.Decompiler, farewell.Definition, appState.ListSession.Navigation.BeginResolving(inNewTab: false), CancellationToken.None));

        Assert.Same(shown, appState.ListSession.Navigation.ActiveTab?.Node);
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
        var shown = d.AppState.ListSession.Navigation.ActiveTab?.Node;
        Assert.NotNull(shown);
        var reference = CodeReferences.CreateEntity(EntityKind.Type, "Greeter", AssemblyPath.Of(path), MetadataTokens.TypeDefinitionHandle(2));

        d.Start((a, ct) => a.NavigateHandler.HandleAsync(a.ListSession.Navigation, a.Decompiler, reference, a.ListSession.Navigation.BeginResolving(inNewTab: false), ct));
        d.Settle();

        var reported = Assert.Single(d.AppState.Errors.Entries);
        var problem = Assert.IsType<AppProblem.ModuleUnreadable>(reported.Problem);
        Assert.Equal("UnreadableTypes.dll", problem.Module);
        Assert.NotEmpty(problem.Why.Message);
        Assert.StartsWith("UnreadableTypes.dll couldn't be read — ", reported.Words().Summary);
        Assert.Same(shown, d.AppState.ListSession.Navigation.ActiveTab?.Node);
        Assert.False(d.AppState.ListSession.Navigation.IsResolving);
    }

    [Fact]
    public async Task HandleAsync_ReferenceIntoAModuleThatCouldNotBeRead_ReportsTheModuleNotTheReference()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var greeter = await d.FixtureAsync("Greeter");
        var module = d.AppState.ListSession.Assemblies.Assemblies.OfType<LoadedModule>().Single(a => a.FilePath.Text == AppStateDriver.FixturePath);
        var why = new BadImageFormatException("Bad namespace.");
        var unreadable = new Lookup<BrowseNode>.Unreadable { Module = new UnreadableModule { Module = module, Why = why } };
        var lookup = new FakeNavigateHandlerPorts { Found = new Outcome<Lookup<BrowseNode>>.Ok(unreadable) };

        var load = await d.RunAsync(() => new NavigateHandler(lookup).HandleAsync(d.AppState.ListSession.Navigation, d.AppState.Decompiler, greeter.Definition, d.AppState.ListSession.Navigation.BeginResolving(inNewTab: false), CancellationToken.None));

        var problem = Assert.IsType<AppProblem.ModuleUnreadable>(Assert.Single(lookup.Reported));
        Assert.Equal(module.FilePath.FileName, problem.Module);
        Assert.Same(why, problem.Why);
        Assert.Null(load);
        Assert.False(d.AppState.ListSession.Navigation.IsResolving);
    }
}
