using System.Reflection.Metadata.Ecma335;
using ILSight.Core;
using ILSight.SearchArea;

namespace ILSight.App.Tests;

/// <summary>The search's adapter over real assemblies: what the core answers for a module the library can't read.</summary>
public sealed class RunSearchHandlerAdapterTests
{
    [Fact]
    public async Task SearchAsync_ModuleTheLibraryCannotRead_FindsItUnsearchedWithWhy()
    {
        using var temp = new TempDir();
        var greet = BrokenAssemblies.GetMethod(AppStateDriver.FixturePath, "Greeter", "Greet");
        var unreadablePath = BrokenAssemblies.WriteUnreadableMethodName(temp, AppStateDriver.FixturePath, greet);
        var path = AssemblyPath.Of(unreadablePath);
        var opened = AssemblyListing.Open(new AssemblyList { Runtime = TestRuntime.Installed }, path);
        var module = Assert.IsType<LoadedModule>(opened);
        var adapter = new RunSearchHandlerAdapter(new BrowseSession());

        var found = new List<SearchFind>();
        await foreach (var batch in adapter.SearchAsync(SearchQueries.ParseQuery("Greet"), [module], CancellationToken.None))
        {
            found.AddRange(batch);
        }

        var unsearched = Assert.IsType<SearchFind.Unsearched>(Assert.Single(found, static f => f is SearchFind.Unsearched));
        Assert.Same(module, unsearched.Assembly);
        Assert.NotEmpty(unsearched.Why);
        Assert.Equal(unsearched.Why, unsearched.Error.Message);
    }

    [Fact]
    public async Task SearchAsync_ModuleReadInPart_FindsTheHitAndSaysWhatItPassedOver()
    {
        using var temp = new TempDir();
        var unreadable = BrokenAssemblies.GetMethod(AppStateDriver.FixturePath, nameof(NumberLoads), nameof(NumberLoads.Unreadable));
        var unplaced = BrokenAssemblies.GetMethod(AppStateDriver.FixturePath, nameof(Unplaced.UnplacedNumberLoad), nameof(Unplaced.UnplacedNumberLoad.Unplaced));
        var partlyReadablePath = BrokenAssemblies.WritePartlyReadable(temp, AppStateDriver.FixturePath, unreadable, unplaced);
        var path = AssemblyPath.Of(partlyReadablePath);
        var opened = AssemblyListing.Open(new AssemblyList { Runtime = TestRuntime.Installed }, path);
        var module = Assert.IsType<LoadedModule>(opened);
        var adapter = new RunSearchHandlerAdapter(new BrowseSession());

        var found = new List<SearchFind>();
        await foreach (var batch in adapter.SearchAsync(SearchQueries.ParseQuery("c:7340033"), [module], CancellationToken.None))
        {
            found.AddRange(batch);
        }

        var hit = Assert.IsType<SearchFind.Hit>(Assert.Single(found, static f => f is SearchFind.Hit));
        Assert.StartsWith(nameof(NumberLoads.Shown), hit.Result.Text, StringComparison.Ordinal);
        var partly = Assert.IsType<SearchFind.PartlySearched>(Assert.Single(found, static f => f is SearchFind.PartlySearched));
        Assert.Same(module, partly.Assembly);
        Assert.Equal(1, partly.Count);
        Assert.NotEmpty(partly.Why);
        Assert.Equal(partly.Why, Assert.IsType<BadImageFormatException>(partly.Error).Message);
        var notShown = Assert.IsType<SearchFind.NotShown>(Assert.Single(found, static f => f is SearchFind.NotShown));
        Assert.Same(module, notShown.Assembly);
        Assert.Equal(MetadataTokens.GetToken(unplaced), notShown.Token);
        Assert.NotEmpty(notShown.Why);
        Assert.Equal(notShown.Why, notShown.Error.Message);
        Assert.DoesNotContain(found, static f => f is SearchFind.Unsearched);
    }
}
