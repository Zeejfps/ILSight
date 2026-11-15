using ILSight.AssemblyListArea;
using ILSight.CommandArea;
using ILSight.Core;

namespace ILSight.App.Tests;

/// <summary>Work of the list's generation in flight when a change ends that generation and stops no reload (one written
/// later, standing in here as <see cref="OpenListSession.ClearAssemblies"/> called directly): the work ends quietly, by
/// what the generation's end does, nothing listed from it and nothing logged. The end runs on the thread that owns the
/// list, between the work's start and its check, through the port the root hands the work. The reload it ends is not
/// finished (no end of the generation stopped it, as the change that ends it owns doing), so these wait for the work to
/// end, not for the app to settle.</summary>
public sealed class GenerationEndTests
{
    [Fact]
    public void A_reload_whose_generation_ends_by_a_change_that_stops_no_reload_ends_quietly()
    {
        EndingTheGenerationBeforeReopening? ports = null;
        var d = AppStateDriver.OpenedWith(new AdapterHooks { Reloading = real => ports = new EndingTheGenerationBeforeReopening(real) });
        Assert.NotNull(ports);
        var session = d.AppState.ListSession;
        ports.End = () => LoadedAssemblies.Release(session.ClearAssemblies());

        d.InTurn(() => Commands.StartReloading(d.AppState, []));
        d.RunUntil(() => d.AppState.Work.Outstanding.Count == 0);

        Assert.Empty(session.Assemblies.Assemblies);
        Assert.Empty(d.AppState.Errors.Entries);
    }

    [Fact]
    public void A_lists_reopening_whose_generation_ends_by_a_change_that_stops_no_reload_ends_quietly()
    {
        EndingTheGenerationBeforeOpening? ports = null;
        var d = AppStateDriver.OpenedWith(new AdapterHooks { Reopening = real => ports = new EndingTheGenerationBeforeOpening(real) });
        Assert.NotNull(ports);
        var appState = d.AppState;
        var session = appState.ListSession;
        appState.Lists.Add("Other", [TestPaths.PathOf(AppStateDriver.FixturePath)]);
        ports.End = () => LoadedAssemblies.Release(session.ClearAssemblies());

        d.InTurn(() => Commands.StartOpeningList(appState, AssemblyLists.SwitchAssemblyList(appState.Lists, session, appState.KeptScreen.View, "Other")));
        d.RunUntil(() => d.AppState.Work.Outstanding.Count == 0);

        Assert.Empty(session.Assemblies.Assemblies);
        Assert.Empty(appState.Errors.Entries);
    }
}
