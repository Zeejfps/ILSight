using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core.Comparison;
using ILSight.BuiltInArea;

namespace ILSight;

/// <summary>A comparison's entries as the tree view reads them (ZGF's ITreeModel), with the view's expansion and
/// selection: while <see cref="OnlyDifferences"/>, only the entries that differ or hold one that does, else all of
/// them. Each row is colored by how its entry changed, and says so, and its tooltip shows how the entry is
/// declared on each side. A comparison shown anew opens with its namespaces that differ expanded. What it shows, and
/// in which style, changes only through its methods, which build the rows again when it does.</summary>
public sealed class ComparisonRows : ITreeModel<ApiEntry>
{
    private readonly Dictionary<ApiEntry, TreeRow> _rows = new(ReferenceEqualityComparer.Instance);
    private UiTheme _theme = UiTheme.Dark;
    private NodeIconPalette _iconColors = BuiltInThemes.Dark.Icons;
    private AppIcons _icons = BuiltInIconThemes.Reference.Icons;
    private Strings _strings = Strings.For(Locale.En);
    private ApiDiff? _shownDiff;
    private IReadOnlyList<ApiEntry> _roots = [];
    private bool _onlyDifferences = true;
    private int _changes;

    public TreeViewState<ApiEntry> View { get; } = new(ReferenceEqualityComparer.Instance);

    public IReadOnlyList<ApiEntry> Roots => _roots;

    public bool OnlyDifferences => _onlyDifferences;

    /// <summary>Counts up whenever the rows start or stop showing only what differs; it never counts down.</summary>
    public int Changes => _changes;

    /// <summary>Draws the rows in <paramref name="theme"/>, <paramref name="icons"/> and <paramref name="strings"/>,
    /// building them again when any of them changed.</summary>
    public void SetStyle(UiTheme theme, NodeIconPalette iconColors, AppIcons icons, Strings strings)
    {
        if (IsStyledAs(theme, iconColors, icons, strings))
        {
            return;
        }

        _theme = theme;
        _iconColors = iconColors;
        _icons = icons;
        _strings = strings;
        _rows.Clear();
    }

    private bool IsStyledAs(UiTheme theme, NodeIconPalette iconColors, AppIcons icons, Strings strings)
    {
        var isStyled = theme == _theme && iconColors == _iconColors;
        if (!isStyled)
        {
            return false;
        }

        return icons == _icons && strings == _strings;
    }

    /// <summary>Shows <paramref name="diff"/>, listing the rows anew when it is not the one shown.</summary>
    public void FollowDiff(ApiDiff? diff)
    {
        if (diff == _shownDiff)
        {
            return;
        }

        _shownDiff = diff;
        _rows.Clear();
        ListRoots();
        ExpandChangedNamespaces();
    }

    /// <summary>Shows only the entries that differ when <paramref name="only"/>, else all of them.</summary>
    public void ShowOnlyDifferences(bool only)
    {
        if (only == _onlyDifferences)
        {
            return;
        }

        _onlyDifferences = only;
        _changes++;
        ListRoots();
    }

    public ComparisonViewRecord SaveView()
    {
        return new ComparisonViewRecord(_onlyDifferences);
    }

    /// <summary>Shows the rows as <paramref name="saved"/> says they were shown.</summary>
    public void RestoreView(ComparisonViewRecord saved)
    {
        ShowOnlyDifferences(saved.OnlyDifferences);
    }

    private void ListRoots()
    {
        _roots = ListShown(ListNamespaces());
        View.Refresh();
    }

    private IReadOnlyList<ApiEntry> ListNamespaces()
    {
        var shownDiff = _shownDiff;
        if (shownDiff is null)
        {
            return [];
        }

        return shownDiff.Namespaces;
    }

    private void ExpandChangedNamespaces()
    {
        foreach (var root in _roots)
        {
            if (root.HasChanges)
            {
                View.SetExpanded(root, true);
            }
        }
        View.Refresh();
    }

    public void ExpandAll()
    {
        foreach (var root in _roots)
        {
            ExpandWithChildren(root);
        }

        View.Refresh();
    }

    private void ExpandWithChildren(ApiEntry entry)
    {
        if (!HasChildren(entry))
        {
            return;
        }

        View.SetExpanded(entry, true);
        foreach (var child in ListChildren(entry))
        {
            ExpandWithChildren(child);
        }
    }

    public void CollapseAll()
    {
        View.CollapseAll();
    }

    public bool HasChildren(ApiEntry entry)
    {
        if (!_onlyDifferences)
        {
            return entry.Children.Count > 0;
        }

        return HoldsChanges(entry);
    }

    // A loop, not Any, so it allocates nothing.
    private static bool HoldsChanges(ApiEntry entry)
    {
        foreach (var child in entry.Children)
        {
            if (child.HasChanges)
            {
                return true;
            }
        }
        return false;
    }

    public TreeChildren<ApiEntry> GetChildren(ApiEntry entry)
    {
        return TreeChildren<ApiEntry>.Of(ListChildren(entry));
    }

    private IReadOnlyList<ApiEntry> ListChildren(ApiEntry entry)
    {
        return ListShown(entry.Children);
    }

    private IReadOnlyList<ApiEntry> ListShown(IReadOnlyList<ApiEntry> entries)
    {
        if (!_onlyDifferences)
        {
            return entries;
        }

        return entries
            .Where(static e => e.HasChanges)
            .ToList();
    }

    /// <summary>What tells an entry apart from its siblings, for tests to find its row by: its label, which for a
    /// member is its signature, so overloads differ.</summary>
    public string KeyOf(ApiEntry entry)
    {
        return entry.Text;
    }

    public TreeRow Row(ApiEntry entry)
    {
        if (_rows.TryGetValue(entry, out var row))
        {
            return row;
        }

        row = CreateRow(entry);
        _rows.Add(entry, row);
        return row;
    }

    private TreeRow CreateRow(ApiEntry entry)
    {
        var icon = NodeIcons.Icon(NodeIcons.Of(entry.Shape), entry.Accessibility, _iconColors, _icons);
        var color = GetColor(entry);
        var tooltip = DescribeDeclarations(entry);
        var change = NameChange(entry.Change);
        return new TreeRow(entry.Text, icon, color, tooltip, change);
    }

    private uint GetColor(ApiEntry entry)
    {
        return entry.Change switch
        {
            ApiChange.Added => _theme.Success,
            ApiChange.Removed => _theme.Danger,
            ApiChange.Changed => _theme.Warning,
            _ => GetUnchangedColor(entry),
        };
    }

    private uint GetUnchangedColor(ApiEntry entry)
    {
        if (entry.HasChanges)
        {
            return 0;
        }

        return _theme.TextMuted;
    }

    private string? DescribeDeclarations(ApiEntry entry)
    {
        return entry.Match<string?>(
            static _ => null,
            entity => DescribeDeclarations(entity.Sides));
    }

    private string DescribeDeclarations(ApiSides sides)
    {
        return sides.Match(
            static left => left.Left.Declaration,
            static right => right.Right.Declaration,
            both => DescribeDeclarations(both));
    }

    private string DescribeDeclarations(ApiSides.Both sides)
    {
        if (!sides.IsDeclaredDifferently)
        {
            return sides.Right.Declaration;
        }

        var left = _strings.ComparisonLeftDeclaration(sides.Left.Declaration);
        var right = _strings.ComparisonRightDeclaration(sides.Right.Declaration);
        return left + Environment.NewLine + right;
    }

    private string? NameChange(ApiChange change)
    {
        return change switch
        {
            ApiChange.Added => _strings.ComparisonAdded,
            ApiChange.Removed => _strings.ComparisonRemoved,
            ApiChange.Changed => _strings.ComparisonChanged,
            _ => null,
        };
    }
}
