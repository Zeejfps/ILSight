using System.Diagnostics;
using ILSight.Core;

namespace ILSight;

/// <summary>
/// The tooltips of references hovered in code: each is looked up once, in the background, and kept
/// while the open assemblies stay the same. A lookup that finishes after they changed is dropped.
/// </summary>
public sealed class CodeTooltips
{
    public required AssemblyList Assemblies { get; init; }
    public required Action<AppProblem> ReportError { get; init; }
    public readonly Dictionary<CodeReference.Entity, CodeTooltip?> Found = new();
    public readonly HashSet<CodeReference.Entity> Pending = new();
    public required int Version;

    /// <summary>Whether a lookup is still running in the background.</summary>
    public bool IsLookingUp => Pending.Count > 0;
}

/// <summary>Looking up the tooltips of references hovered in code.</summary>
public static class TooltipLookups
{
    /// <summary>Whether the lookup of <paramref name="reference"/> is done, with its
    /// <paramref name="tooltip"/>, null when it has none. Starts the lookup the first time.</summary>
    public static bool TryGetTooltip(CodeTooltips tooltips, CodeReference.Entity reference, out CodeTooltip? tooltip)
    {
        ForgetIfStale(tooltips);
        if (tooltips.Found.TryGetValue(reference, out tooltip))
            return true;
        LookUp(tooltips, reference);
        return false;
    }

    private static void ForgetIfStale(CodeTooltips tooltips)
    {
        if (tooltips.Version == tooltips.Assemblies.Version)
            return;
        tooltips.Version = tooltips.Assemblies.Version;
        tooltips.Found.Clear();
        tooltips.Pending.Clear();
    }

    private static void LookUp(CodeTooltips tooltips, CodeReference.Entity reference)
    {
        Background.Start(LookUpAsync(tooltips, reference), tooltips.ReportError);
    }

    private static async Task LookUpAsync(CodeTooltips tooltips, CodeReference.Entity reference)
    {
        if (!tooltips.Pending.Add(reference))
            return;
        var version = tooltips.Version;
        var outcome = await Background.Run(() => AssemblyListing.DescribeReference(tooltips.Assemblies, reference));
        if (version != tooltips.Version)
            return;
        tooltips.Pending.Remove(reference);
        tooltips.Found[reference] = outcome switch
        {
            Outcome<CodeTooltip>.Ok ok => ok.Value,
            Outcome<CodeTooltip>.Failed => null,
            _ => throw new UnreachableException(),
        };
    }
}
