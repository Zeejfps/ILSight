using ILSight.Core;

namespace ILSight.ExportArea;

/// <summary>An open assembly as the export dialog lists it: whether it is exported, and the name of
/// its project.</summary>
public sealed class ExportChoice
{
    public required LoadedModule Assembly { get; init; }
    public bool Included;
    public required string Name;
}
