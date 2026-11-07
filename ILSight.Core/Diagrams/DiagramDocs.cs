// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using System.Text.RegularExpressions;
using System.Xml;
using ICSharpCode.Decompiler.Documentation;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.Diagrams;

/// <summary>The XML documentation of an assembly's types and members as the diagrammer shows it:
/// plain text, with the tags around summaries, paragraphs and references taken out.</summary>
internal sealed class DiagramDocs
{
    public required IDocumentationProvider? Provider { get; init; }
}
