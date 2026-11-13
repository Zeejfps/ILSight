using ILSight.Core;
using ILSight.CodeTooltipArea;

namespace ILSight.App.Tests;

public sealed class LookUpTooltipHandlerTests
{
    private static async Task<TooltipLookup> AskForGreeterAsync(AppStateDriver d, CodeTooltips tooltips)
    {
        Assert.False(tooltips.TryGetOrAskTooltip((await d.FixtureAsync("Greeter")).Definition, out _));
        return Assert.Single(tooltips.TakeAsked());
    }

    [Fact]
    public async Task HandleAsync_Described_KeepsTheTooltip()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var assemblies = d.AppState.Assemblies;
        var tooltips = new CodeTooltips { Assemblies = assemblies, Version = assemblies.Version };
        var lookup = await AskForGreeterAsync(d, tooltips);
        var tooltip = await AssemblyListing.DescribeReferenceAsync(assemblies, lookup.Reference, CancellationToken.None);
        var ports = new FakeLookUpTooltipHandlerPorts { Answer = new Outcome<Lookup<CodeTooltip>>.Ok(tooltip) };

        await d.RunAsync(() => new LookUpTooltipHandler(ports).HandleAsync(tooltips, lookup, CancellationToken.None));

        Assert.True(tooltips.TryGetOrAskTooltip(lookup.Reference, out var kept));
        Assert.Same(tooltip, kept);
    }

    [Fact]
    public async Task HandleAsync_Failed_KeepsNoTooltip()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var assemblies = d.AppState.Assemblies;
        var tooltips = new CodeTooltips { Assemblies = assemblies, Version = assemblies.Version };
        var lookup = await AskForGreeterAsync(d, tooltips);
        var ports = new FakeLookUpTooltipHandlerPorts { Answer = new Outcome<Lookup<CodeTooltip>>.Failed(new InvalidDataException("unreadable")) };

        await d.RunAsync(() => new LookUpTooltipHandler(ports).HandleAsync(tooltips, lookup, CancellationToken.None));

        Assert.True(tooltips.TryGetOrAskTooltip(lookup.Reference, out var kept));
        Assert.IsType<Lookup<CodeTooltip>.NotFound>(kept);
    }

    [Fact]
    public async Task HandleAsync_InAModuleThatCouldNotBeRead_KeepsThatToShowInPlace()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var assemblies = d.AppState.Assemblies;
        var tooltips = new CodeTooltips { Assemblies = assemblies, Version = assemblies.Version };
        var lookup = await AskForGreeterAsync(d, tooltips);
        var module = assemblies.Assemblies.OfType<LoadedModule>().Single(a => a.FilePath.Text == AppStateDriver.FixturePath);
        var unreadable = new Lookup<CodeTooltip>.Unreadable { Module = new UnreadableModule { Module = module, Why = new BadImageFormatException("Bad namespace.") } };
        var ports = new FakeLookUpTooltipHandlerPorts { Answer = new Outcome<Lookup<CodeTooltip>>.Ok(unreadable) };

        await d.RunAsync(() => new LookUpTooltipHandler(ports).HandleAsync(tooltips, lookup, CancellationToken.None));

        Assert.Same(unreadable, tooltips.FindShown(lookup.Reference));
    }

    [Fact]
    public async Task TryGetTooltip_AskedAgainBeforeItsLookupEnds_AsksOnce()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var assemblies = d.AppState.Assemblies;
        var tooltips = new CodeTooltips { Assemblies = assemblies, Version = assemblies.Version };
        var lookup = await AskForGreeterAsync(d, tooltips);

        var found = tooltips.TryGetOrAskTooltip(lookup.Reference, out _);
        var shown = tooltips.FindShown(lookup.Reference);

        Assert.False(found);
        Assert.Null(shown);
        Assert.False(tooltips.HasAsked);
    }
}
