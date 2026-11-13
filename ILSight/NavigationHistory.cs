using ILSight.Core;
using ILSight.AssemblyArea;
using ILSight.ExportArea;
using ILSight.DiagramArea;

namespace ILSight;

/// <summary>Back/forward over the places, diagrams, comparisons and reports navigated to. Each is kept as a value
/// naming its place, so it outlives reloads and closed tabs and pins no assembly and no document.</summary>
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
        var place = node.Path;
        if (IsCurrentPlace(place))
        {
            return;
        }

        _entries.Add(new HistoryEntry.Node
        {
            Place = place,
            Icon = NodeIcons.Of(node.Subject),
            Accessibility = node.Visibility.Accessibility,
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

    /// <summary>Records <paramref name="diagram"/>, given <paramref name="id"/> and titled <paramref name="title"/>, as
    /// worked in now, as it stands, dropping what was forward of the current entry; as it stands now in place of how
    /// it stood when it is the current entry already.</summary>
    public void RecordDiagram(int id, TypeDiagram diagram, string title)
    {
        var place = new HistoryEntry.Diagram
        {
            Id = id,
            Title = title,
            Types = diagram.ListAnchors(),
            HomeAssemblies = diagram.HomeAssemblies.ToList(),
        };
        if (Current is HistoryEntry.Diagram current && current.Id == id)
        {
            _entries.TryReplaceCurrent(place);
            return;
        }

        _entries.Add(place);
    }

    /// <summary>Records <paramref name="comparison"/>, given <paramref name="id"/> and titled <paramref name="title"/>,
    /// as worked in now, as <see cref="RecordDiagram"/> records a diagram.</summary>
    public void RecordComparison(int id, AssemblyComparison comparison, string title)
    {
        var place = new HistoryEntry.Comparison
        {
            Id = id,
            Title = title,
            LeftPath = comparison.LeftPath,
            RightPath = comparison.RightPath,
            Scope = comparison.Scope,
        };
        if (Current is HistoryEntry.Comparison current && current.Id == id)
        {
            _entries.TryReplaceCurrent(place);
            return;
        }

        _entries.Add(place);
    }

    /// <summary>Records <paramref name="report"/> as shown now, dropping what was forward of the current entry.</summary>
    public void RecordReport(ExportReport report)
    {
        if (Current is HistoryEntry.Report current && current.Run == report.Run)
        {
            return;
        }

        _entries.Add(new HistoryEntry.Report { Run = report.Run });
    }

    /// <summary>Forgets every place.</summary>
    public void Clear()
    {
        _entries.Clear();
    }

    /// <summary>Keeps <paramref name="diagram"/>, given <paramref name="id"/>, as it stands as it is left, on each entry
    /// of it, so that going back to it once no tab shows it makes it again as it was left.</summary>
    public void KeepDiagram(int id, TypeDiagram diagram)
    {
        for (var i = 0; i < _entries.Count; i++)
        {
            if (_entries[i] is not HistoryEntry.Diagram entry)
            {
                continue;
            }

            if (entry.Id != id)
            {
                continue;
            }

            _entries.ReplaceAt(i, entry with { Types = diagram.ListAnchors(), HomeAssemblies = diagram.HomeAssemblies.ToList() });
        }
    }

    /// <summary>Keeps <paramref name="comparison"/>, given <paramref name="id"/>, as it stands as it is left, on each
    /// entry of it, as <see cref="KeepDiagram"/> does.</summary>
    public void KeepComparison(int id, AssemblyComparison comparison)
    {
        for (var i = 0; i < _entries.Count; i++)
        {
            if (_entries[i] is not HistoryEntry.Comparison entry)
            {
                continue;
            }

            if (entry.Id != id)
            {
                continue;
            }

            _entries.ReplaceAt(i, entry with { LeftPath = comparison.LeftPath, RightPath = comparison.RightPath, Scope = comparison.Scope });
        }
    }

    /// <summary>Puts <paramref name="entry"/>, the current one made again in a tab of its own, in place of the current
    /// one.</summary>
    public void ReplaceCurrent(HistoryEntry entry)
    {
        _entries.TryReplaceCurrent(entry);
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
