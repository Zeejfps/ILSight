using ILSight.AssemblyArea;
using ILSight.AssemblyListArea;
using ILSight.Core;

namespace ILSight.App.Tests;

/// <summary>The assembly list's adapter over real files: what the core answers for a file that can't be read again.</summary>
public sealed class AssemblyListInfrastructureTests
{
    [Fact]
    public async Task ReadAgainAsync_FileTheLibraryCanNoLongerRead_AnswersItsFailureWithWhy()
    {
        using var temp = new TempDir();
        var path = temp.Write("Changed.dll", await File.ReadAllBytesAsync(AppStateDriver.FixturePath));
        var assemblies = new AssemblyList { Runtime = TestRuntime.Installed };
        var item = AssemblyListing.Open(assemblies, AssemblyPath.Of(path));
        await File.WriteAllTextAsync(path, "no longer an assembly");
        var adapter = new AssemblyListInfrastructure(new ErrorLog(), new RefusalLine(), assemblies, new BrowseSession());

        var read = await adapter.ReadAgainAsync(item, [], CancellationToken.None);

        Assert.IsType<AssemblyLoadException>(UnionAsserts.GetCase<Outcome<ReopenedFile>.Failed>(read).Error);
    }
}
