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

/// <summary>Back/forward over the places and diagrams navigated to. Places are kept as paths so they
/// outlive reloads and pin no assembly; a diagram is forgotten once its tab closes.</summary>
public sealed class NavigationHistory
{
    /// <summary>Every place recorded, oldest first.</summary>
    public readonly List<HistoryEntry> Entries = new();

    /// <summary>The index in <see cref="Entries"/> of the place being shown, or -1 before any.</summary>
    public int CurrentIndex = -1;

    /// <summary>Changes whenever an entry is added or dropped, or another becomes current.</summary>
    public int Revision;

    public bool CanGoBack => CurrentIndex > 0;
    public bool CanGoForward => CurrentIndex < Entries.Count - 1;

    public HistoryEntry? Current => CurrentIndex >= 0 ? Entries[CurrentIndex] : null;
}
