using System.Diagnostics;
using ILSight.Core;

namespace ILSight;

/// <summary>Looking up the tooltips of references hovered in code.</summary>
public static class TooltipLookups
{
    /// <summary>Whether the lookup of <paramref name="reference"/> is done, with its
    /// <paramref name="tooltip"/>, null when it has none. Starts the lookup the first time.</summary>
    public static bool TryGetTooltip(CodeTooltips tooltips, CodeReference.Entity reference, out CodeTooltip? tooltip)
    {
        ForgetIfStale(tooltips);
        if (tooltips.Found.TryGetValue(reference, out tooltip))
        {
            return true;
        }

        LookUp(tooltips, reference);
        return false;
    }

    private static void ForgetIfStale(CodeTooltips tooltips)
    {
        if (tooltips.Version == tooltips.Assemblies.Version)
        {
            return;
        }

        tooltips.Version = tooltips.Assemblies.Version;
        tooltips.Found.Clear();
        tooltips.Pending.Clear();
    }

    private static void LookUp(CodeTooltips tooltips, CodeReference.Entity reference)
    {
        Background.Start(tooltips.Work, nameof(LookUpAsync), LookUpAsync(tooltips, reference), tooltips.ReportError);
    }

    private static async Task LookUpAsync(CodeTooltips tooltips, CodeReference.Entity reference)
    {
        if (!tooltips.Pending.Add(reference))
        {
            return;
        }

        var version = tooltips.Version;
        var outcome = await Background.Run(() => AssemblyListing.DescribeReference(tooltips.Assemblies, reference));
        if (version != tooltips.Version)
        {
            return;
        }

        tooltips.Pending.Remove(reference);
        tooltips.Found[reference] = outcome switch
        {
            Outcome<CodeTooltip>.Ok ok => ok.Value,
            Outcome<CodeTooltip>.Failed => null,
            _ => throw new UnreachableException(),
        };
    }
}
