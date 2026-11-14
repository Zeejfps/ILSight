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
        var path = AssemblyPath.Of(BrokenAssemblies.WriteUnreadableMethodName(temp, AppStateDriver.FixturePath, greet));
        var module = Assert.IsType<LoadedModule>(AssemblyListing.Open(new AssemblyList { Runtime = TestRuntime.Installed }, path));
        var adapter = new RunSearchHandlerAdapter(new ErrorLog(), new BrowseSession());

        var found = new List<SearchFind>();
        await foreach (var batch in adapter.SearchAsync(SearchQueries.ParseQuery("Greet"), [module], CancellationToken.None))
        {
            found.AddRange(Assert.IsType<Outcome<IReadOnlyList<SearchFind>>.Ok>(batch).Value);
        }

        var unsearched = Assert.IsType<SearchFind.Unsearched>(Assert.Single(found, static f => f is SearchFind.Unsearched));
        Assert.Same(module, unsearched.Assembly);
        Assert.NotEmpty(unsearched.Why);
    }
}
