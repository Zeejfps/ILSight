// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

namespace ILSight.Core.Diagrams;

/// <summary>The types of an assembly as the HTML diagrammer reads them: those it lets you pick, by
/// namespace and then by id, and the display names of the types they refer to but that it leaves out,
/// by id.</summary>
internal sealed class DiagramModel
{
    public required Dictionary<string, string> OutsideReferences { get; init; }
    public required Dictionary<string, Dictionary<string, DiagramType>> TypesByNamespace { get; init; }
}
