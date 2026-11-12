using ILSight.Core;

namespace ILSight;

/// <summary>The tooltip a view keeps between frames: the one last looked up, with the part of the view it is
/// for, kept while the pointer crosses over to another part until that part's tooltip shows.</summary>
internal sealed class KeptTooltip
{
    public (CodeTooltip Tooltip, int Part)? Kept { get; set; }
}
