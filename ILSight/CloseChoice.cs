using ILSight.Core;

namespace ILSight;

/// <summary>An item of the list in the "close assemblies" dialog, and whether it is ticked to close.</summary>
public sealed class CloseChoice
{
    public required LoadedAssembly Assembly { get; init; }
    public bool Included;
}
