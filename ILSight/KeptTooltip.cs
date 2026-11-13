using ILSight.Core;

namespace ILSight;

/// <summary>The tooltip a view keeps between frames: the one last looked up, with the part of the view it is
/// for, kept while the pointer crosses over to another part until that part's tooltip shows.</summary>
internal sealed class KeptTooltip
{
    private (Lookup<CodeTooltip> Tooltip, int Part)? _kept;

    /// <summary>Keeps <paramref name="tooltip"/> for <paramref name="part"/>, unless there is none or the tooltip
    /// of another part, <paramref name="shownPart"/>, is still shown.</summary>
    public void Keep(Lookup<CodeTooltip>? tooltip, int part, int shownPart)
    {
        if (tooltip == null)
        {
            return;
        }

        if (shownPart != 0 && shownPart != part)
        {
            return;
        }

        _kept = (tooltip, part);
    }

    public Lookup<CodeTooltip>? Find(int shownPart)
    {
        if (_kept is null)
        {
            return null;
        }

        var (tooltip, part) = _kept.Value;
        if (part != shownPart)
        {
            return null;
        }

        return tooltip;
    }

    public void Forget()
    {
        _kept = null;
    }
}
