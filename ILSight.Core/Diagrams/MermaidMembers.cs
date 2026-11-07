// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.Diagrams;

/// <summary>Members as lines of a mermaid class definition: visibility, then type and name, with *
/// marking an abstract member and $ a static one (https://mermaid.js.org/syntax/classDiagram.html).</summary>
internal sealed class MermaidMembers
{
    public required DiagramNames Names { get; init; }
}
