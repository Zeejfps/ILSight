using ILSight.Core;

namespace ILSight.App.Tests;

public sealed class LoadOptionTests
{
    [Fact]
    public async Task Turning_off_loading_references_reads_the_open_assemblies_again_without_loading_what_they_reference()
    {
        var app = AppDriver.Opened();
        app.Settle(2);
        var before = (await app.NodeAsync()).Assembly;

        Preferences.SetDecompilerToggle(app.AppState, DecompilerToggle.AutoLoadAssemblyReferences, false);
        app.Run(2);
        app.Settle(2);
        var runtime = await app.NodeAsync("References", "System.Runtime");

        Assert.NotSame(before, (await app.NodeAsync()).Assembly);
        Assert.All(await runtime.ListChildrenAsync(), c => Assert.Equal(NodeIcon.ReferencedTypes, c.Icon));
    }
}
