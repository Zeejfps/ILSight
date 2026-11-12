using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core.Comparison;
using ILSight.Localization;
using ILSight.BuiltInAssets;

namespace ILSight;

/// <summary>A class because ZGF's ITreeModel demands the methods here, which forward. A comparison's entries as
/// the tree view reads them, with the view's expansion and selection:
/// while <see cref="OnlyDifferences"/>, only the entries that differ or hold one that does, else all of
/// them. Each row is colored by how its entry changed, and says so, and its tooltip shows how the entry is
/// declared on each side. A comparison shown anew opens with its namespaces that differ expanded.</summary>
public sealed class ComparisonRows : ITreeModel<ApiEntry>
{
    public readonly Dictionary<ApiEntry, TreeRow> Rows = new(ReferenceEqualityComparer.Instance);
    public UiTheme Theme = UiTheme.Dark;
    public NodeIconPalette IconColors = BuiltInThemes.Dark.Icons;
    public AppIcons Icons = BuiltInIconThemes.Reference.Icons;
    public Strings Strings = Strings.For(Locale.En);
    public ApiDiff? ShownDiff;

    public TreeViewState<ApiEntry> View { get; } = new(ReferenceEqualityComparer.Instance);

    public IReadOnlyList<ApiEntry> Roots = [];

    public bool OnlyDifferences = true;

    /// <summary>Counts up whenever the rows start or stop showing only what differs; it never counts down.</summary>
    public int Changes;

    public bool HasChildren(ApiEntry entry)
    {
        return Comparisons.HasChildren(this, entry);
    }

    public TreeChildren<ApiEntry> GetChildren(ApiEntry entry)
    {
        return TreeChildren<ApiEntry>.Of(Comparisons.ListChildren(this, entry));
    }

    public TreeRow Row(ApiEntry entry)
    {
        return Comparisons.GetRow(this, entry);
    }

    public string KeyOf(ApiEntry entry)
    {
        return Comparisons.GetKey(entry);
    }
}
