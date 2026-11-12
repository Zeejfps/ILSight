using ILSight.Core;

namespace ILSight.ComparisonPickerArea;

/// <summary>What the "compare with" dialog offers, and the rules it keeps: the assembly to compare, and the other
/// open assemblies whose API it can be compared with, those of its name first, filtered by name or path, one of
/// them selected. It closes when the assembly to compare is closed.</summary>
public sealed class ComparisonChoice
{
    private readonly List<LoadedModule> _candidates = new();
    private readonly List<LoadedModule> _filtered = new();
    private int _listedVersion = -1;
    private string? _appliedFilter;
    private bool _focusFilter;

    /// <summary>The assemblies offered, in the order shown.</summary>
    public IReadOnlyList<LoadedModule> Filtered => _filtered;

    private ComparisonDialog Dialog { get; set; } = new ComparisonDialog.Closed();

    /// <summary>The assembly selected to compare with, held by itself, so that it can't point past the
    /// assemblies offered as they change.</summary>
    public ItemSelection<LoadedModule> Selection { get; } = new();

    public bool IsOpen => Dialog is ComparisonDialog.Open;

    /// <summary>The assembly to compare, or null while the dialog is closed.</summary>
    public LoadedModule? Compared
    {
        get
        {
            return Dialog.Match<LoadedModule?>(
                static _ => null,
                static open => open.Compared,
                static going => going.Compared);
        }
    }

    /// <summary>The assembly selected to compare with, or null when none is.</summary>
    public LoadedModule? Selected => Selection.Item;

    /// <summary>Opens the dialog to pick what to compare <paramref name="compared"/> with.</summary>
    public void Open(LoadedModule compared)
    {
        Dialog = new ComparisonDialog.Open(compared);
        ItemSelections.Clear(Selection);
        _focusFilter = true;
        _listedVersion = -1;
        _appliedFilter = null;
    }

    public void Close()
    {
        Dialog = new ComparisonDialog.Closed();
    }

    /// <summary>Has the dialog go out, as ZGF dismisses it through a <see langword="ref"/> bool: it keeps
    /// what it shows while it goes, where closing it from the buttons drops it at once.</summary>
    public void LetGo()
    {
        if (Dialog is ComparisonDialog.Open open)
        {
            Dialog = new ComparisonDialog.Going(open.Compared);
        }
    }

    /// <summary>True once after <see cref="Open"/>: the filter box should take focus.</summary>
    public bool TryTakeFilterFocus()
    {
        var focus = _focusFilter;
        _focusFilter = false;
        return focus;
    }

    /// <summary>While open, lists the assemblies again when the open ones or <paramref name="filter"/> changed
    /// since the last frame; closes the dialog when the assembly to compare is no longer open.</summary>
    public void Update(AssemblyList assemblies, string filter)
    {
        if (Dialog is not ComparisonDialog.Open open)
        {
            return;
        }

        RefreshCandidates(assemblies, open.Compared);
        ApplyFilter(filter);
    }

    private void RefreshCandidates(AssemblyList assemblies, LoadedModule compared)
    {
        if (assemblies.Version == _listedVersion)
        {
            return;
        }

        _listedVersion = assemblies.Version;
        var open = assemblies.Assemblies;
        if (!open.Contains(compared))
        {
            Close();
            return;
        }
        _candidates.Clear();
        _candidates.AddRange(ListCandidates(open, compared));
        _appliedFilter = null;
    }

    private static IEnumerable<LoadedModule> ListCandidates(IReadOnlyList<LoadedModule> open, LoadedModule compared)
    {
        return open
            .Where(a => a != compared)
            .OrderByDescending(a => string.Equals(a.Name, compared.Name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Filters the candidates, selecting the first shown unless the one selected still is.</summary>
    private void ApplyFilter(string filter)
    {
        if (!IsOpen)
        {
            return;
        }

        if (filter == _appliedFilter)
        {
            return;
        }

        _appliedFilter = filter;
        var selected = Selected;
        _filtered.Clear();
        var trimmed = filter.Trim();
        foreach (var candidate in _candidates)
        {
            if (Matches(candidate, trimmed))
            {
                _filtered.Add(candidate);
            }
        }
        SelectAgain(selected);
    }

    private static bool Matches(LoadedModule assembly, string filter)
    {
        if (filter.Length == 0)
        {
            return true;
        }

        if (assembly.NameAndVersion.Contains(filter, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return assembly.FilePath.Text.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private void SelectAgain(LoadedModule? selected)
    {
        if (_filtered.Count == 0)
        {
            ItemSelections.Clear(Selection);
            return;
        }
        var index = 0;
        if (selected is not null)
        {
            index = Math.Max(0, _filtered.IndexOf(selected));
        }

        ItemSelections.Select(Selection, _filtered[index], _filtered);
    }
}
