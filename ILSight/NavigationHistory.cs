using ILSight.Core;
using ILSight.AssemblyArea;
using ILSight.ExportArea;

namespace ILSight;

/// <summary>Back/forward over the places, diagrams, comparisons and reports navigated to. Places are kept as paths
/// so they outlive reloads and pin no assembly; a diagram, a comparison or a report is forgotten once no tab shows
/// it.</summary>
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

        _entries.Add(new HistoryEntry.Node
        {
            Place = place,
            Icon = NodeIcons.Of(node.Subject),
            Accessibility = NodeDefinitions.GetVisibility(node.Subject).Accessibility,
            Language = language,
            Position = null,
            ShownAs = null,
        });
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

    /// <summary>Records <paramref name="comparison"/>, titled <paramref name="title"/>, as worked in now, dropping what
    /// was forward of the current entry.</summary>
    public void RecordComparison(AssemblyComparison comparison, string title)
    {
        if (Current is HistoryEntry.Comparison current && current.Shown == comparison)
        {
            return;
        }

        _entries.Add(new HistoryEntry.Comparison { Shown = comparison, Title = title });
    }

    /// <summary>Records <paramref name="report"/> as shown now, dropping what was forward of the current entry.</summary>
    public void RecordReport(ExportReport report)
    {
        if (Current is HistoryEntry.Report current && current.Shown == report)
        {
            return;
        }

        _entries.Add(new HistoryEntry.Report { Shown = report });
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

    /// <summary>Forgets <paramref name="comparison"/>, closed for good, as <see cref="ForgetDiagram"/> does.</summary>
    public void ForgetComparison(AssemblyComparison comparison)
    {
        for (var i = _entries.Count - 1; i >= 0; i--)
        {
            if (_entries[i] is not HistoryEntry.Comparison entry)
            {
                continue;
            }

            if (entry.Shown != comparison)
            {
                continue;
            }

            _entries.RemoveAt(i);
        }
    }

    /// <summary>Forgets <paramref name="report"/>, which no tab shows any more, as <see cref="ForgetDiagram"/>
    /// does.</summary>
    public void ForgetReport(ExportReport report)
    {
        for (var i = _entries.Count - 1; i >= 0; i--)
        {
            if (_entries[i] is not HistoryEntry.Report entry)
            {
                continue;
            }

            if (entry.Shown != report)
            {
                continue;
            }

            _entries.RemoveAt(i);
        }
    }

    /// <summary>Remembers where the view of the current place stands as it is left, and what of the tab it is left
    /// from it was shown as, <paramref name="shownAs"/>, for <see cref="KeepPosition"/>.</summary>
    public void LeaveEntry(DecompilationLanguage language, ViewPosition position, TabVersion shownAs)
    {
        if (Current is not HistoryEntry.Node node)
        {
            return;
        }

        _entries.TryReplaceCurrent(node with { Language = language, Position = position, ShownAs = shownAs });
    }

    /// <summary>Keeps <paramref name="position"/>, where a tab's view stood with <paramref name="shownAs"/>, which the
    /// tab no longer shows, on the entry left from it: the view was read back after the tab moved on. Nothing when
    /// no entry was left from it. Allocates nothing while the entry holds that position already.</summary>
    public void KeepPosition(TabVersion shownAs, ViewPosition position)
    {
        for (var i = 0; i < _entries.Count; i++)
        {
            if (_entries[i] is not HistoryEntry.Node node)
            {
                continue;
            }

            if (node.ShownAs != shownAs)
            {
                continue;
            }

            if (node.Position == position)
            {
                continue;
            }

            _entries.ReplaceAt(i, node with { Position = position });
        }
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
