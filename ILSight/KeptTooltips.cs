using ILSight.Core;

namespace ILSight;

internal static class KeptTooltips
{
    /// <summary>Keeps <paramref name="tooltip"/> for <paramref name="part"/>, unless there is none or the tooltip
    /// of another part, <paramref name="shownPart"/>, is still shown.</summary>
    public static void Keep(KeptTooltip kept, CodeTooltip? tooltip, int part, int shownPart)
    {
        if (tooltip == null)
        {
            return;
        }

        if (shownPart != 0 && shownPart != part)
        {
            return;
        }

        kept.Kept = (tooltip, part);
    }

    /// <summary>The tooltip kept for <paramref name="shownPart"/>, or null.</summary>
    public static CodeTooltip? Find(KeptTooltip kept, int shownPart)
    {
        if (kept.Kept is null)
        {
            return null;
        }

        var (tooltip, part) = kept.Kept.Value;
        if (part != shownPart)
        {
            return null;
        }

        return tooltip;
    }

    public static void Forget(KeptTooltip kept)
    {
        kept.Kept = null;
    }
}
