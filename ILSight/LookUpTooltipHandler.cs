using ILSight.Core;
using ILSight.CodeTooltipArea;

namespace ILSight;

/// <summary>Looks up the tooltip of a reference hovered in code through <see cref="IPorts"/>, without holding up the
/// screen, and keeps it; a reference that could not be described has none, and one into a module that could not be
/// read says so.</summary>
public sealed class LookUpTooltipHandler(LookUpTooltipHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>What the tooltip of <paramref name="reference"/> says; not found when it has none, unreadable when
        /// it is in a module whose type system the library could not read; worked out in the open assemblies without
        /// holding up the screen. Working it out that failed comes back as <see cref="Outcome{T}.Failed"/>; the
        /// cancellation of <paramref name="ct"/> is let through.</summary>
        Task<Outcome<Lookup<CodeTooltip>>> DescribeReferenceAsync(CodeReference.Entity reference, CancellationToken ct);
    }

    /// <summary>Makes <paramref name="lookup"/> and keeps what it found in <paramref name="tooltips"/>.</summary>
    public async Task HandleAsync(CodeTooltips tooltips, TooltipLookup lookup, CancellationToken ct)
    {
        var described = await ports.DescribeReferenceAsync(lookup.Reference, ct);
        tooltips.Finish(lookup, described.Match(static ok => ok.Value, static _ => new Lookup<CodeTooltip>.NotFound()));
    }
}
