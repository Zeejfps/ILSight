using ILSight.Core;

namespace ILSight;

/// <summary>Back/forward over the places and diagrams navigated to. Places are kept as paths so they
/// outlive reloads and pin no assembly; a diagram is forgotten once its tab closes.</summary>
public sealed class NavigationHistory
{
    private const int Capacity = 200;

    /// <summary>Every place recorded, oldest first, and the one being shown. An index rather than back and
    /// forward stacks, as the history menus name the entry to go to by where it is.</summary>
    private readonly CursorList<HistoryEntry> _entries = new(Capacity);

    /// <summary>Every place recorded, oldest first.</summary>
    public IReadOnlyList<HistoryEntry> Entries => _entries;

    /// <summary>The index in <see cref="Entries"/> of the place being shown, or -1 before any.</summary>
    public int CurrentIndex => _entries.CurrentIndex;

    /// <summary>Changes whenever an entry is added or dropped, or another becomes current.</summary>
    public int Revision => _entries.Revision;

    public bool CanGoBack => CurrentIndex > 0;
    public bool CanGoForward => CurrentIndex < _entries.Count - 1;

    public HistoryEntry? Current => _entries.Current;

    /// <summary>Records a new place, dropping what was forward of the current one.</summary>
    public void RecordPlace(BrowseNode node, DecompilationLanguage language)
    {
        var place = BrowseNodes.GetPath(node);
        if (IsCurrentPlace(place))
        {
            return;
        }

        _entries.Add(new HistoryEntry.Node(place, NodeIcons.Of(node.Subject), NodeDefinitions.GetVisibility(node.Subject).Accessibility, language, null));
    }

    /// <summary>Whether the place being shown is <paramref name="place"/>.</summary>
    public bool IsCurrentPlace(NodePath? place)
    {
        if (Current is not HistoryEntry.Node node)
        {
            return false;
        }

        return node.Place.Equals(place);
    }

    /// <summary>Records <paramref name="diagram"/>, titled <paramref name="title"/>, as worked in now,
    /// dropping what was forward of the current entry.</summary>
    public void RecordDiagram(TypeDiagram diagram, string title)
    {
        if (Current is HistoryEntry.Diagram current && current.Shown == diagram)
        {
            return;
        }

        _entries.Add(new HistoryEntry.Diagram(diagram, title));
    }

    /// <summary>Forgets every place.</summary>
    public void Clear()
    {
        _entries.Clear();
    }

    /// <summary>Forgets <paramref name="diagram"/>, closed for good; the entry before a forgotten current
    /// one becomes current.</summary>
    public void ForgetDiagram(TypeDiagram diagram)
    {
        for (var i = _entries.Count - 1; i >= 0; i--)
        {
            if (_entries[i] is not HistoryEntry.Diagram entry)
            {
                continue;
            }

            if (entry.Shown != diagram)
            {
                continue;
            }

            _entries.RemoveAt(i);
        }
    }

    /// <summary>Remembers where the view of the current place stands as it is left.</summary>
    public void LeaveEntry(DecompilationLanguage language, ViewPosition position)
    {
        if (Current is not HistoryEntry.Node node)
        {
            return;
        }

        _entries.TryReplaceCurrent(node with { Language = language, Position = position });
    }

    /// <summary>Makes the entry at <paramref name="index"/> current, keeping every entry on either side.
    /// Null when there is no entry there.</summary>
    public HistoryEntry? MoveToEntry(int index)
    {
        if (!_entries.TryMoveTo(index))
        {
            return null;
        }

        return _entries[index];
    }
}
