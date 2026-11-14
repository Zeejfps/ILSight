using ILSight.AssemblyArea;
using ILSight.Core;
using ILSight.Core.Comparison;

namespace ILSight.App.Tests;

/// <summary>The comparison's adapter over real assemblies: what the core answers for a member the library can't read.</summary>
public sealed class CompareAssembliesHandlerAdapterTests
{
    [Fact]
    public async Task CompareAsync_MemberTheLibraryCannotRead_AnswersItsFailureWithWhy()
    {
        using var temp = new TempDir();
        var greet = BrokenAssemblies.GetMethod(AppStateDriver.FixturePath, "Greeter", "Greet");
        var assemblies = new AssemblyList { Runtime = TestRuntime.Installed };
        var left = Assert.IsType<LoadedModule>(AssemblyListing.Open(assemblies, AssemblyPath.Of(AppStateDriver.FixturePath)));
        var right = Assert.IsType<LoadedModule>(AssemblyListing.Open(assemblies,
            AssemblyPath.Of(BrokenAssemblies.WriteUnreadableMethodName(temp, AppStateDriver.FixturePath, greet))));

        var compared = await new CompareAssembliesHandlerAdapter(new ErrorLog()).CompareAsync(left, right, ApiScope.PublicApi, CancellationToken.None);

        Assert.IsType<BadImageFormatException>(Assert.IsType<Outcome<ModuleRead<ApiDiff>>.Failed>(compared).Error);
    }
}
