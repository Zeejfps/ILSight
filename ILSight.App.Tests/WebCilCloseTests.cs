using ILSight.CommandArea;
using ILSight.Core;
using static ILSight.App.Tests.AppStateDriver;

namespace ILSight.App.Tests;

public sealed class WebCilCloseTests
{
    private static readonly string LargeAppPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "WebCil", "LargeApp.wasm");

    /// <summary>An app showing Part159 of a copy of the WebCIL fixture over 64 KB, put in <paramref name="folder"/>, so
    /// the test may replace its file.</summary>
    private static (AppDriver App, LoadedModule Module, string Path) ShowingLargeApp(TempDir folder)
    {
        var path = folder.Write("LargeApp.wasm", File.ReadAllBytes(LargeAppPath));
        var app = new AppDriver(height: 500f);
        app.Driver.Start((a, ct) => a.AssemblyOpening.OpenAssembliesAsync(a, [AssemblyPath.Of(path)], ct));
        app.Driver.RunUntil(() => app.AppState.Assemblies.Assemblies.Count == 1);
        app.Settle(1);
        var module = app.AppState.Assemblies.Assemblies.Single();
        app.Show(module.Root.Child("LargeApp", "Part159"), 2);
        return (app, module, path);
    }

    [Fact]
    public void A_webcil_assembly_closed_while_a_member_decompiles_keeps_the_tab_and_frees_its_file_once_the_reads_end()
    {
        using var folder = new TempDir();
        var (app, module, path) = ShowingLargeApp(folder);
        var whole = File.ReadAllBytes(path);
        var tab = app.AppState.Navigation.ActiveTab ?? throw new InvalidOperationException("Part159 was shown in no tab.");
        var code = tab.Document;

        app.Driver.StartShowing(module.Root.Child("LargeApp", "Part158"), inNewTab: true);
        var decompiling = app.AppState.Navigation.ActiveTab ?? throw new InvalidOperationException("Part158 was shown in no tab.");
        Commands.CloseAssemblies(app.AppState, [module]);
        app.Settle(2);
        var released = LoadedAssemblies.WaitForReleaseAsync(module, CancellationToken.None);
        app.Frames.WaitUntil(() => released.IsCompleted, Patience);
        File.WriteAllBytes(path, whole);

        Assert.Same(code, tab.Document);
        Assert.Equal("LargeApp.wasm", tab.Load.ClosedAssembly);
        Assert.Equal("LargeApp.wasm", decompiling.Load.ClosedAssembly);
        Assert.False(decompiling.Load.IsBusy);
        Assert.Empty(app.AppState.Errors.Entries);
    }

    [Fact]
    public void Switching_the_language_after_a_webcil_assembly_closed_starts_nothing_for_its_tab()
    {
        using var folder = new TempDir();
        var (app, module, _) = ShowingLargeApp(folder);
        var tab = app.AppState.Navigation.ActiveTab ?? throw new InvalidOperationException("Part159 was shown in no tab.");
        var code = tab.Document;
        Commands.CloseAssemblies(app.AppState, [module]);
        app.Settle(2);

        app.AppState.Decompiler.UseLanguage(DecompilationLanguage.IL);
        app.Settle(2);

        Assert.Equal(DecompilationLanguage.CSharp, tab.Load.Language);
        Assert.Same(code, tab.Document);
        Assert.False(tab.Load.IsBusy);
        Assert.Empty(app.AppState.Errors.Entries);
    }
}
