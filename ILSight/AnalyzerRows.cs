using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Localization;

namespace ILSight;

/// <summary>A class because ZGF's ITreeModel demands the methods here, which forward. The analyzer's tree as the
/// tree view reads it, with the view's expansion and selection.
/// Expanding an analysis's row the first time asks for it to run, and it shows a loading row until it
/// is done.</summary>
public sealed class AnalyzerRows : ITreeModel<AnalyzerNode>
{
    /// <summary>Where the rows ask for an analysis to run.</summary>
    public required Queue<AppCommand> AppCommands { get; init; }

    public readonly HashSet<AnalyzerKindNode> Asked = new();
    public readonly AnalyzerMessageNode Loading = new();
    public UiTheme Theme = UiTheme.Dark;
    public NodeIconPalette IconColors = BuiltInThemes.Dark.Icons;
    public AppIcons Icons = BuiltInIconThemes.Reference.Icons;
    public Strings Strings = Strings.For(Locale.En);
    public int SeenChanges = -1;
    public int SeenFindings = -1;

    public TreeViewState<AnalyzerNode> View { get; } = new(ReferenceEqualityComparer.Instance);

    public IReadOnlyList<AnalyzerNode> Roots = Array.Empty<AnalyzerNode>();

    public bool HasChildren(AnalyzerNode node)
    {
        return Analysis.HasChildren(this, node);
    }

    public TreeChildren<AnalyzerNode> GetChildren(AnalyzerNode node)
    {
        return TreeChildren<AnalyzerNode>.Of(Analysis.ListChildren(this, node));
    }

    public TreeRow Row(AnalyzerNode node)
    {
        return Analysis.GetRow(this, node);
    }
}
