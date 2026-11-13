using ILSight.Core;
using static ILSight.AssemblyListArea.AssemblyLists;
using static ILSight.SettingArea.Settings;
using static ILSight.App.Tests.TestPaths;
using ILSight.SettingArea;
using ILSight.CommandArea;

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
        app.Start((a, ct) => a.AssemblyOpening.OpenAssembliesAsync(a, [AssemblyPath.Of(copy)], ct));
        app.Frames.WaitUntil(() => app.AppState.Assemblies.IsListed(PathOf(copy)), Patience);
        app.Settle(2);
        Assert.Null(LoadedAssemblies.EnsureDebugSymbols(OpenAssemblyOf(app.Driver)));
        app.Frames.FilePicker.Answer = FixturePdb;

        app.PickFromTreeMenu(OpenAssemblyOf(app.Driver).Name, "Select PDB…");
        app.Frames.WaitUntil(() => app.AppState.Assemblies.Pdbs.All.Count > 0, Patience);
        app.Settle(2);

        Assert.Equal(FixturePdb, LoadedAssemblies.EnsureDebugSymbols(OpenAssemblyOf(app.Driver))?.SourceFileName);
        Assert.Empty(app.AppState.Errors.Entries);
    }

    [Fact]
    public void A_picked_pdb_is_kept_with_the_list_for_the_next_run()
    {
        using var temp = new TempDir();
        var copy = CopyFixtureWithoutPdb(temp);
        var d = OpenedCopy(temp, copy);

        Pick(d, FixturePdb);
        Assert.IsType<SettingsWriteResult.Written>(d.AppState.SettingsSaving.Save(d.AppState));
        var next = new AppStateDriver(temp.Settings());
        next.Start(AppStateDriver.OpenAtStartupAsync);
        next.Settle();

        Assert.Equal(FixturePdb, Assert.Single(temp.Settings().AssemblyLists).Pdbs?[PathOf(copy)]);
        Assert.Equal(FixturePdb, LoadedAssemblies.EnsureDebugSymbols(OpenAssemblyOf(next))?.SourceFileName);
    }

    [Fact]
    public void A_pdb_of_another_build_is_reported_and_not_picked()
    {
        var d = AppStateDriver.Opened();

        d.Start((a, ct) => a.AssemblyReloading.UsePdbAsync(a.ListSession, a.Reopens, a.Decompiler, OpenAssemblyOf(d), OtherBuildPdb, ct));
        d.RunUntil(() => d.AppState.Errors.Entries.Count > 0);

        var error = Assert.Single(d.AppState.Errors.Entries);
        Assert.Empty(d.AppState.Assemblies.Pdbs.All);
        Assert.Contains("another build", error.Words().Details);
    }

    [Fact]
    public void Clearing_the_list_forgets_the_picked_pdbs()
    {
        using var temp = new TempDir();
        var copy = CopyFixtureWithoutPdb(temp);
        var d = OpenedCopy(temp, copy);
        Pick(d, FixturePdb);

        Commands.CloseAllAssemblies(d.AppState);
        RememberOpenAssemblies(d.AppState.Lists, d.AppState.ListSession);
        Assert.IsType<SettingsWriteResult.Written>(d.AppState.SettingsSaving.Save(d.AppState));

        Assert.Empty(d.AppState.Assemblies.Pdbs.All);
        Assert.Null(Assert.Single(temp.Settings().AssemblyLists).Pdbs);
    }

    /// <summary>The fixture assembly copied where no PDB is beside it.</summary>
    private static string CopyFixtureWithoutPdb(TempDir temp)
    {
        return temp.Copy(AppStateDriver.FixturePath, Path.GetFileName(AppStateDriver.FixturePath));
    }

    private static AppStateDriver OpenedCopy(TempDir temp, string copy)
    {
        var d = new AppStateDriver(temp.Settings());
        d.Start((a, ct) => a.AssemblyOpening.OpenAssembliesAsync(a, [AssemblyPath.Of(copy)], ct));
        d.RunUntil(() => d.AppState.Assemblies.IsListed(PathOf(copy)));
        d.Settle();
        return d;
    }

    private static void Pick(AppStateDriver d, string pdb)
    {
        d.Start((a, ct) => a.AssemblyReloading.UsePdbAsync(a.ListSession, a.Reopens, a.Decompiler, OpenAssemblyOf(d), pdb, ct));
        d.RunUntil(() => d.AppState.Assemblies.Pdbs.All.Count > 0);
        d.Settle();
    }

    private static LoadedModule OpenAssemblyOf(AppStateDriver d)
    {
        return Assert.Single(d.AppState.Assemblies.Assemblies);
    }
}
