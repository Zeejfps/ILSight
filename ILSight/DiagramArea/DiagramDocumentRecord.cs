using ILSight.Core;

namespace ILSight.DiagramArea;

/// <summary>A tab showing a diagram of <see cref="Types"/>, in the order added, the assemblies at
/// <see cref="HomeAssemblies"/> its own, and how it was viewed.</summary>
public sealed record DiagramDocumentRecord : DocumentRecord
{
    public required List<TypeAnchor> Types { get; init; }

    public required List<AssemblyPath>? HomeAssemblies { get; init; }

    public required DiagramViewRecord? View { get; init; }
}
