using ILSight.Core;

namespace ILSight.App.Tests;

/// <summary>The tree's adapter over real assemblies: what the core answers for a module the library can't read.</summary>
public sealed class TreeInfrastructureTests
{
    [Fact]
    public async Task BuildAssemblyChildrenAsync_ModuleTheLibraryCannotRead_AnswersItsFailureWithWhy()
    {
        using var temp = new TempDir();
        var module = new AssemblyList { Runtime = TestRuntime.Installed }.Open(BrokenAssemblies.WriteUnreadableTypeSystem(temp, AppStateDriver.FixturePath));
        var root = await Assert.IsType<LoadedModule>(module).ReadRootAsync();

        var built = await new TreeInfrastructure(TestSession.Shared).BuildAssemblyChildrenAsync(root, CancellationToken.None);

        Assert.IsType<BadImageFormatException>(Assert.IsType<ChildrenState.Failed>(built).Error);
    }
}
