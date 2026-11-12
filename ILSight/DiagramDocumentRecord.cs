using ILSight.Core;

namespace ILSight;

/// <summary>A tab showing a diagram of <see cref="Types"/>, in the order added, the assemblies at
/// <see cref="HomeAssemblies"/> its own, and how it was viewed.</summary>
public sealed record DiagramDocumentRecord(
    string Panel,
    string? Title,
    List<TypeAnchor> Types,
    List<AssemblyPath>? HomeAssemblies,
    DiagramViewRecord? View) : DocumentRecord(Panel, Title);
