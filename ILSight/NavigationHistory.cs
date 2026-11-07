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
    private const int Capacity = 200;

    /// <summary>Every place recorded, oldest first, and the one being shown. An index rather than back and
    /// forward stacks, as the history menus name the entry to go to by where it is.</summary>
    public readonly CursorList<HistoryEntry> Entries = new(Capacity);

    /// <summary>The index in <see cref="Entries"/> of the place being shown, or -1 before any.</summary>
    public int CurrentIndex => Entries.CurrentIndex;

    /// <summary>Changes whenever an entry is added or dropped, or another becomes current.</summary>
    public int Revision => Entries.Revision;

    public bool CanGoBack => CurrentIndex > 0;
    public bool CanGoForward => CurrentIndex < Entries.Count - 1;

    public HistoryEntry? Current => Entries.Current;
}
