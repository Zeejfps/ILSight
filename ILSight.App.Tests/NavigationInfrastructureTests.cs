using System.Collections.Immutable;
using ILSight.Core;

namespace ILSight.App.Tests;

/// <summary>The navigation's adapter over real assemblies: what its lookups let through.</summary>
public sealed class NavigationInfrastructureTests
{
    private static NavigationInfrastructure Create()
    {
        return new NavigationInfrastructure(new ErrorLog(), new AssemblyList { Runtime = TestRuntime.Installed }, new BrowseSession(), new OpenRequests());
    }

    private static NodePath FixtureRoot()
    {
        return new NodePath { FilePath = AssemblyPath.Of(AppStateDriver.FixturePath), Steps = ImmutableArray<NodePathStep>.Empty };
    }

    [Fact]
    public async Task LookUpPlaceAsync_TokenCancelled_ThrowsThatCancellation()
    {
        using var navigationClosed = new CancellationTokenSource();
        await navigationClosed.CancelAsync();

        var thrown = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Create().LookUpPlaceAsync(FixtureRoot(), navigationClosed.Token));

        Assert.Equal(navigationClosed.Token, thrown.CancellationToken);
    }

    [Fact]
    public async Task LookUpListedPlacesAsync_TokenCancelled_ThrowsThatCancellation()
    {
        using var navigationClosed = new CancellationTokenSource();
        await navigationClosed.CancelAsync();

        var thrown = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Create().LookUpListedPlacesAsync([FixtureRoot()], navigationClosed.Token));

        Assert.Equal(navigationClosed.Token, thrown.CancellationToken);
    }
}
