using ILSight.Core;
using ILSight.CodeTooltipping;

namespace ILSight;

/// <summary>The lookup of a hovered reference's tooltip <see cref="CodeTooltips"/> asked for, for
/// <see cref="LookUpTooltipHandler"/> to make: the reference, and the version of the open assemblies it was asked for
/// over. Dropped, the reference is looked up never, and the tooltips say they are looking up for good.</summary>
[MustUse]
public sealed class TooltipLookup
{
    public required CodeReference.Entity Reference { get; init; }

    public required int Version { get; init; }
}
