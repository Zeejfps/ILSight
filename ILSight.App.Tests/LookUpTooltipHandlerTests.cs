using ILSight.Core;
using ILSight.CodeTooltipArea;

namespace ILSight.App.Tests;

public sealed class LookUpTooltipHandlerTests
{
    private static TooltipLookup AskForGreeter(AppStateDriver d, CodeTooltips tooltips)
    {
        Assert.False(tooltips.TryGetTooltip(d.Fixture("Greeter").Definition, out _));
        return Assert.Single(tooltips.TakeAsked());
    }

    [Fact]
    public async Task HandleAsync_Described_KeepsTheTooltip()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var assemblies = d.AppState.Assemblies;
        var tooltips = new CodeTooltips { Assemblies = assemblies, Version = assemblies.Version };
        var lookup = AskForGreeter(d, tooltips);
        var tooltip = AssemblyListing.DescribeReference(assemblies, lookup.Reference);
        var ports = new FakeLookUpTooltipHandlerPorts { Answer = new Outcome<CodeTooltip?>.Ok(tooltip) };

        await new LookUpTooltipHandler(ports).HandleAsync(tooltips, lookup, CancellationToken.None);

        Assert.True(tooltips.TryGetTooltip(lookup.Reference, out var kept));
        Assert.Same(tooltip, kept);
        Assert.False(tooltips.IsLookingUp);
    }

    [Fact]
    public async Task HandleAsync_Failed_KeepsNoTooltip()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var assemblies = d.AppState.Assemblies;
        var tooltips = new CodeTooltips { Assemblies = assemblies, Version = assemblies.Version };
        var lookup = AskForGreeter(d, tooltips);
        var ports = new FakeLookUpTooltipHandlerPorts { Answer = new Outcome<CodeTooltip?>.Failed(new InvalidDataException("unreadable")) };

        await new LookUpTooltipHandler(ports).HandleAsync(tooltips, lookup, CancellationToken.None);

        Assert.True(tooltips.TryGetTooltip(lookup.Reference, out var kept));
        Assert.Null(kept);
    }

    [Fact]
    public void TryGetTooltip_AskedAgainBeforeItsLookupEnds_AsksOnce()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var assemblies = d.AppState.Assemblies;
        var tooltips = new CodeTooltips { Assemblies = assemblies, Version = assemblies.Version };
        var lookup = AskForGreeter(d, tooltips);

        var found = tooltips.TryGetTooltip(lookup.Reference, out _);

        Assert.False(found);
        Assert.False(tooltips.HasAsked);
        Assert.True(tooltips.IsLookingUp);
    }
}
