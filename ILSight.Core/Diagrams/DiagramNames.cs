// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.Diagrams;

/// <summary>The ids and display names of the types a diagram draws and refers to. Ids are unique
/// across the diagram and safe in a DOM id; types outside the diagram that it refers to are
/// recorded with their display names.</summary>
internal sealed class DiagramNames
{
    public required HashSet<IType> Picked { get; init; }

    public required Dictionary<IType, string> Ids { get; init; }

    public readonly Dictionary<IType, string> NamesByType = new();

    /// <summary>The display names of the types outside the diagram it refers to, by id.</summary>
    public Dictionary<string, string> OutsideReferences { get; } = new();
}
