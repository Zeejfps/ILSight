using ILSight.Core;

namespace ILSight;

/// <summary>Something navigated to: a node's place, or a diagram.</summary>
public abstract record HistoryEntry
{
    private HistoryEntry()
    {
    }

    /// <summary>A place navigated to, the icon and accessibility its node showed, the language it was shown
    /// in, and where its view stood when it was left.</summary>
    public sealed record Node(NodePath Place, NodeIcon Icon, NodeAccessibility Accessibility, DecompilationLanguage Language, ViewPosition? Position)
        : HistoryEntry;

    /// <summary>A diagram worked in, under the title of its tab.</summary>
    public sealed record Diagram(TypeDiagram Shown, string Title) : HistoryEntry;
}
