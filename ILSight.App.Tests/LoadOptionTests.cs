using ILSight.Core;

namespace ILSight.App.Tests;

public sealed class LoadOptionTests
{
    [Fact]
    public async Task Turning_off_loading_references_reads_the_open_assemblies_again_without_loading_what_they_reference()
    {
        var app = AppDriver.Opened();
        app.Settle(2);
        var rootBefore = await app.NodeAsync();
        var before = rootBefore.Assembly;

        Preferences.SetDecompilerToggle(app.AppState, DecompilerToggle.AutoLoadAssemblyReferences, false);
        app.Run(2);
        app.Settle(2);
        var runtime = await app.NodeAsync("References", "System.Runtime");

        var rootAfter = await app.NodeAsync();
        Assert.NotSame(before, rootAfter.Assembly);
        Assert.All(await runtime.ListChildrenAsync(), c => Assert.Equal(NodeIcon.ReferencedTypes, c.Icon));
    }

    [Fact]
    public void Changing_a_load_option_cancels_the_opens_under_way_once_it_has_ended_the_lists_generation()
    {
        var app = AppDriver.Opened();
        app.Settle(2);
        var opens = app.AppState.ListSession.Assemblies.Work.Ending;
        var generation = app.AppState.ListSession.Assemblies.Generation;
        int? cancelledIn = null;
        using var seen = opens.Register(() => cancelledIn = app.AppState.ListSession.Assemblies.Generation);

        Preferences.SetDecompilerToggle(app.AppState, DecompilerToggle.AutoLoadAssemblyReferences, false);
        app.Run(1);

        Assert.Equal(generation + 1, cancelledIn);
        Assert.NotEqual(generation, app.AppState.ListSession.Assemblies.Generation);
    }

    [Fact]
    public void A_load_option_set_as_it_was_cancels_nothing()
    {
        var app = AppDriver.Opened();
        app.Settle(2);
        var opens = app.AppState.ListSession.Assemblies.Work.Ending;

        Preferences.SetDecompilerToggle(app.AppState, DecompilerToggle.AutoLoadAssemblyReferences, true);
        app.Run(1);

        Assert.False(opens.IsCancellationRequested);
    }
}
