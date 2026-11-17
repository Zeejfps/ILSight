using ILSight.Core;
using ILSight.SettingArea;
using ILSight.CommandArea;
using ILSight.AssemblyListArea;

namespace ILSight.App.Tests;

public sealed class PickedPdbTests
{
    private static readonly string FixturePdb = Path.ChangeExtension(AppStateDriver.FixturePath, ".pdb");
    private static readonly string OtherBuildPdb = Path.ChangeExtension(typeof(AppState).Assembly.Location, ".pdb");

    [Fact]
    public void A_pdb_picked_from_the_tree_menu_is_read_after_the_reload()
    {
        using var temp = new TempDir();
        var copy = CopyFixtureWithoutPdb(temp);
        var app = new AppDriver(width: 1600f, settings: temp.Settings());
        app.Open([AssemblyPath.Of(copy)]);
        app.Frames.WaitUntil(() => app.AppState.ListSession.Assemblies.IsListed(TestPaths.PathOf(copy)), Waits.Patience);
        app.Settle(2);
        var symbolsBefore = LoadedAssemblies.EnsureDebugSymbols(OpenAssemblyOf(app.Driver));
        Assert.Null(symbolsBefore);
        app.Frames.FilePicker.Answer = FixturePdb;

        app.PickFromTreeMenu(OpenAssemblyOf(app.Driver).Name, "Select PDB…");
        app.Frames.WaitUntil(() => app.AppState.ListSession.Assemblies.Pdbs.All.Count > 0, Waits.Patience);
        app.Settle(2);

        var symbols = LoadedAssemblies.EnsureDebugSymbols(OpenAssemblyOf(app.Driver));
        Assert.Equal(FixturePdb, symbols?.SourceFileName);
        Assert.Empty(app.AppState.Errors.Entries);
    }

    [Fact]
    public void A_picked_pdb_is_kept_with_the_list_for_the_next_run()
    {
        using var temp = new TempDir();
        var copy = CopyFixtureWithoutPdb(temp);
        var d = OpenedCopy(temp, copy);

        Pick(d, FixturePdb);
        var write = d.AppState.SettingsSaver.Save(d.AppState);
        Assert.IsType<SettingsWriteResult.Written>(write);
        var next = AppStateDriver.Started(temp.Settings());
        next.Settle();

        Assert.Equal(FixturePdb, Assert.Single(temp.Settings().AssemblyLists).Pdbs?[TestPaths.PathOf(copy)]);
        var symbols = LoadedAssemblies.EnsureDebugSymbols(OpenAssemblyOf(next));
        Assert.Equal(FixturePdb, symbols?.SourceFileName);
    }

    [Fact]
    public void A_pdb_of_another_build_is_refused_and_not_picked()
    {
        var d = AppStateDriver.Opened();

        d.Start((a, ct) => a.AssemblyReloader.UsePdbAsync(a.ListSession, a.Reopens, a.Decompiler, OpenAssemblyOf(d), OtherBuildPdb, ct));
        d.Settle();

        Refusal refused = UnionAsserts.GetCase<Refusal.PdbUnusable>(d.AppState.Refusals.Shown);
        Assert.Empty(d.AppState.ListSession.Assemblies.Pdbs.All);
        Assert.Contains("another build", refused.Words(Strings.For(Locale.En)).Why);
        Assert.Empty(d.AppState.Errors.Entries);
    }

    [Fact]
    public void Clearing_the_list_forgets_the_picked_pdbs()
    {
        using var temp = new TempDir();
        var copy = CopyFixtureWithoutPdb(temp);
        var d = OpenedCopy(temp, copy);
        Pick(d, FixturePdb);

        d.InTurn(() => Commands.CloseAllAssemblies(d.AppState));
        AssemblyLists.RememberOpenAssemblies(d.AppState.Lists, d.AppState.ListSession);
        var write = d.AppState.SettingsSaver.Save(d.AppState);
        Assert.IsType<SettingsWriteResult.Written>(write);

        Assert.Empty(d.AppState.ListSession.Assemblies.Pdbs.All);
        Assert.Null(Assert.Single(temp.Settings().AssemblyLists).Pdbs);
    }

    private static string CopyFixtureWithoutPdb(TempDir temp)
    {
        return temp.Copy(AppStateDriver.FixturePath, Path.GetFileName(AppStateDriver.FixturePath));
    }

    private static AppStateDriver OpenedCopy(TempDir temp, string copy)
    {
        var d = AppStateDriver.Started(temp.Settings());
        d.Open([AssemblyPath.Of(copy)]);
        d.RunUntil(() => d.AppState.ListSession.Assemblies.IsListed(TestPaths.PathOf(copy)));
        d.Settle();
        return d;
    }

    private static void Pick(AppStateDriver d, string pdb)
    {
        d.Start((a, ct) => a.AssemblyReloader.UsePdbAsync(a.ListSession, a.Reopens, a.Decompiler, OpenAssemblyOf(d), pdb, ct));
        d.RunUntil(() => d.AppState.ListSession.Assemblies.Pdbs.All.Count > 0);
        d.Settle();
    }

    private static LoadedModule OpenAssemblyOf(AppStateDriver d)
    {
        return Assert.Single(d.AppState.ListSession.Assemblies.Assemblies);
    }
}
