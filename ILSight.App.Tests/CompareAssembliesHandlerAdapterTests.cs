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
        var leftOpened = AssemblyListing.Open(assemblies, AssemblyPath.Of(AppStateDriver.FixturePath));
        var left = Assert.IsType<LoadedModule>(leftOpened);
        var unreadablePath = BrokenAssemblies.WriteUnreadableMethodName(temp, AppStateDriver.FixturePath, greet);
        var rightOpened = AssemblyListing.Open(assemblies, AssemblyPath.Of(unreadablePath));
        var right = Assert.IsType<LoadedModule>(rightOpened);

        var compared = await new CompareAssembliesHandlerAdapter(new ErrorLog()).CompareAsync(left, right, ApiScope.PublicApi, CancellationToken.None);

        Assert.IsType<BadImageFormatException>(Assert.IsType<Outcome<ModuleRead<ApiDiff>>.Failed>(compared).Error);
    }
}
