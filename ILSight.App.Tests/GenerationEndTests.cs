using ILSight.AssemblyListArea;
using ILSight.CommandArea;
using ILSight.DocumentViewArea;
using ILSight.Core;

namespace ILSight.App.Tests;

/// <summary>Work of the list's generation in flight when a change ends that generation and stops no reload (one written
/// later, standing in here as <see cref="OpenListSession.ClearAssemblies"/> called directly): the work ends quietly, by
/// what the generation's end does, nothing listed from it and nothing logged. The end runs on the thread that owns the
/// list, between the work's start and its check, through the port the root hands the work; or, for the check landing
/// after the list's lock is released and before the end's cancel has reached the edge's token, on a thread of its own,
/// held inside that cancel. The reload's own quiet end ends it (its ticket, and the tabs it left reloading), so nothing
/// stays pending once it has settled; a newer reload that ended the generation keeps its own.</summary>
public sealed class GenerationEndTests
{
    [Fact]
    public void A_reload_whose_generation_ends_by_a_change_that_stops_no_reload_ends_quietly_and_leaves_nothing_reloading()
    {
        EndingTheGenerationBeforeReopening? ports = null;
        var d = AppStateDriver.OpenedWith(new AdapterHooks { Reloader = real => ports = new EndingTheGenerationBeforeReopening(real) });
        Assert.NotNull(ports);
        var session = d.AppState.ListSession;
        var tab = session.Navigation.ActiveTab;
        Assert.NotNull(tab);
        ports.End = () => LoadedAssemblies.Release(session.ClearAssemblies());

        d.InTurn(() => Commands.StartReloading(d.AppState, []));
        d.RunUntil(() => d.AppState.Work.Outstanding.Count == 0);

        Assert.Empty(session.Assemblies.Assemblies);
        Assert.Empty(d.AppState.Errors.Entries);
        Assert.False(session.Reload.IsRunning);
        Assert.Equal(ReloadEnd.AssemblyClosed, Assert.IsType<TabContent.NotReloaded>(tab.Content).End);
    }

    [Fact]
    public void A_reload_stopped_by_closing_every_assembly_leaves_no_tab_reloading()
    {
        EndingTheGenerationBeforeReopening? ports = null;
        var d = AppStateDriver.OpenedWith(new AdapterHooks { Reloader = real => ports = new EndingTheGenerationBeforeReopening(real) });
        Assert.NotNull(ports);
        var appState = d.AppState;
        var tab = appState.ListSession.Navigation.ActiveTab;
        Assert.NotNull(tab);
        ports.End = () => Commands.CloseAllAssemblies(appState);

        d.InTurn(() => Commands.StartReloading(appState, []));
        d.RunUntil(() => appState.Work.Outstanding.Count == 0);

        Assert.False(appState.ListSession.Reload.IsRunning);
        var shown = Assert.IsType<TabContent.NotReloaded>(tab.Content);
        Assert.Equal(ReloadEnd.AssemblyClosed, shown.End);
        Assert.Equal($"{shown.Location.FilePath.FileName} was closed.", TabPlaceholders.Write(Strings.For(Locale.En), tab.Content));
        Assert.Empty(appState.Errors.Entries);
    }

    [Fact]
    public void A_reload_stopped_with_its_assembly_listed_again_says_the_tab_was_not_reloaded_never_gone()
    {
        EndingTheGenerationBeforeReopening? ports = null;
        var d = AppStateDriver.OpenedWith(new AdapterHooks { Reloader = real => ports = new EndingTheGenerationBeforeReopening(real) });
        Assert.NotNull(ports);
        var appState = d.AppState;
        var session = appState.ListSession;
        var tab = session.Navigation.ActiveTab;
        Assert.NotNull(tab);
        ports.AfterFirstReopen = () => session.Reload.Stop();

        d.InTurn(() => Commands.StartReloading(appState, []));
        d.RunUntil(() => appState.Work.Outstanding.Count == 0);

        Assert.False(session.Reload.IsRunning);
        var shown = Assert.IsType<TabContent.NotReloaded>(tab.Content);
        Assert.Equal(ReloadEnd.Stopped, shown.End);
        Assert.True(session.Assemblies.IsListed(shown.Location.FilePath));
        Assert.Equal($"{shown.Location.Label} was not reloaded: the reload was stopped. Use Reload all to look for it again.",
            TabPlaceholders.Write(Strings.For(Locale.En), tab.Content));
        Assert.Empty(appState.Errors.Entries);
    }

    [Fact]
    public void A_reload_whose_generation_new_load_options_end_leaves_the_new_reload_pending_until_its_own_end()
    {
        EndingTheGenerationBeforeReopening? ports = null;
        var d = AppStateDriver.OpenedWith(new AdapterHooks { Reloader = real => ports = new EndingTheGenerationBeforeReopening(real) });
        Assert.NotNull(ports);
        var appState = d.AppState;
        var session = appState.ListSession;
        var tab = session.Navigation.ActiveTab;
        Assert.NotNull(tab);
        var newReopen = new TaskCompletionSource();
        ports.LaterReopensWaitFor = newReopen.Task;
        var options = DecompilerToggles.SetToggle(appState.Decompiler.Setup.Options, DecompilerToggle.AutoLoadAssemblyReferences,
            !DecompilerToggles.IsOn(appState.Decompiler.Setup.Options, DecompilerToggle.AutoLoadAssemblyReferences));
        ports.End = () =>
        {
            // As the app reloads on new load options: their change ends the generation, then a new reload starts.
            appState.Decompiler.UseOptions(options);
            Assert.True(AssemblyListing.TryChangeLoadOptions(session.Assemblies, options));
            Commands.StartReloading(appState, []);
        };

        d.InTurn(() => Commands.StartReloading(appState, []));
        d.RunUntil(() => appState.Work.Outstanding.Count == 1);

        Assert.True(session.Reload.IsRunning);
        Assert.IsType<TabContent.Reloading>(tab.Content);
        newReopen.SetResult();
        d.RunUntil(() => appState.Work.Outstanding.Count == 0);
        Assert.False(session.Reload.IsRunning);
        Assert.Empty(appState.Errors.Entries);
    }

    [Fact]
    public void A_reload_whose_check_lands_while_the_generations_end_is_still_cancelling_ends_quietly()
    {
        HoldingTheGenerationsEndAtTheReopen? ports = null;
        var d = AppStateDriver.OpenedWith(new AdapterHooks { Reloader = real => ports = new HoldingTheGenerationsEndAtTheReopen(real) });
        Assert.NotNull(ports);
        var session = d.AppState.ListSession;
        var removed = RemovedModules.None;
        ports.End = () => removed = session.ClearAssemblies();

        d.InTurn(() => Commands.StartReloading(d.AppState, []));
        d.RunUntil(() => d.AppState.Work.Outstanding.Count == 0);
        ports.Release();
        LoadedAssemblies.Release(removed);

        Assert.Equal(false, ports.LinkedReachedAtCheck);
        Assert.Equal(true, ports.EndingMarkedAtCheck);
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
        Assert.False(session.Reload.IsRunning);
    }
}
