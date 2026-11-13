using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using System.Diagnostics;
using ILSight.Core.Analysis;
using ILSight.CommandArea;
using ILSight.BuiltInArea;

namespace ILSight;

/// <summary>The analyzer's tree as the tree view reads it (ZGF's ITreeModel), with the view's expansion and
/// selection. Expanding an analysis's row the first time asks for it to run, and it shows a loading row until it
/// is done. What it shows, and in which style, changes only through its methods.</summary>
public sealed class AnalyzerRows : ITreeModel<AnalyzerNode>
{
    /// <summary>Where the rows ask for an analysis to run.</summary>
    public required Queue<AppCommand> AppCommands { private get; init; }

    private readonly HashSet<AnalyzerKindNode> _asked = new();
    private UiTheme _theme = UiTheme.Dark;
    private NodeIconPalette _iconColors = BuiltInThemes.Dark.Icons;
    private AppIcons _icons = BuiltInIconThemes.Reference.Icons;
    private Strings _strings = Strings.For(Locale.En);
    private int _seenChanges = -1;
    private int _seenFindings = -1;
    private IReadOnlyList<AnalyzerNode> _roots = Array.Empty<AnalyzerNode>();

    public TreeViewState<AnalyzerNode> View { get; } = new(ReferenceEqualityComparer.Instance);

    public IReadOnlyList<AnalyzerNode> Roots => _roots;

    /// <summary>The row an analysis shows after what it found so far while it runs.</summary>
    public AnalyzerMessageNode Loading { get; } = new();

    public void SetStyle(UiTheme theme, NodeIconPalette iconColors, AppIcons icons, Strings strings)
    {
        _theme = theme;
        _iconColors = iconColors;
        _icons = icons;
        _strings = strings;
    }

    /// <summary>Shows the roots of <paramref name="analyzer"/>, listing the rows again when the roots changed,
    /// dropping a selection whose root is gone, or when an analysis found more.</summary>
    public void FollowTree(AnalyzerTree analyzer)
    {
        _roots = analyzer.Roots;
        _asked.Clear();
        FollowRoots(analyzer);
        FollowFindings(analyzer);
    }

    private void FollowRoots(AnalyzerTree analyzer)
    {
        if (analyzer.Changes == _seenChanges)
        {
            return;
        }

        _seenChanges = analyzer.Changes;
        View.Refresh();
        DropSelectionOfRemovedRoot(analyzer);
    }

    private void DropSelectionOfRemovedRoot(AnalyzerTree analyzer)
    {
        if (!View.TryGetSelectedRoot(out var root))
        {
            return;
        }

        if (analyzer.HasRoot(root))
        {
            return;
        }

        View.ClearSelection();
    }

    private void FollowFindings(AnalyzerTree analyzer)
    {
        if (analyzer.Findings == _seenFindings)
        {
            return;
        }

        _seenFindings = analyzer.Findings;
        View.Refresh();
    }

    /// <summary>Expands <paramref name="root"/> and selects it.</summary>
    public void ShowRoot(AnalyzerSymbolNode root)
    {
        View.SetExpanded(root, true);
        View.Refresh();
        View.Select(root);
    }

    /// <summary>The root the selection sits under, or null when nothing showing is selected.</summary>
    public AnalyzerNode? FindSelectedRoot()
    {
        if (!View.TryGetSelectedRoot(out var root))
        {
            return null;
        }

        return root;
    }

    /// <summary>Expands every root found and each analysis of it, running those not run yet.</summary>
    public void ExpandAll()
    {
        foreach (var root in _roots)
        {
            if (root is AnalyzerSymbolNode symbol)
            {
                ExpandWithAnalyses(symbol);
            }
        }
    }

    private void ExpandWithAnalyses(AnalyzerSymbolNode symbol)
    {
        View.SetExpanded(symbol, true);
        foreach (var child in Analysis.EnsureChildren(symbol))
        {
            if (child is AnalyzerSymbolNode accessor)
            {
                ExpandWithAnalyses(accessor);
            }
            else
            {
                View.SetExpanded(child, true);
            }
        }
    }

    public void CollapseAll()
    {
        View.CollapseAll();
    }

    public bool HasChildren(AnalyzerNode node)
    {
        return node switch
        {
            AnalyzerSymbolNode symbol => Analysis.EnsureChildren(symbol).Count > 0,
            AnalyzerKindNode kind => !kind.IsDone || kind.Found.Count > 0,
            AnalyzerMessageNode or AnalyzerPendingNode or AnalyzerFailedNode => false,
            _ => throw new UnreachableException(),
        };
    }

    public TreeChildren<AnalyzerNode> GetChildren(AnalyzerNode node)
    {
        return TreeChildren<AnalyzerNode>.Of(ListChildren(node));
    }

    /// <summary>The rows under <paramref name="node"/>; listing an analysis's asks once for it to run, unless it
    /// already started.</summary>
    public IReadOnlyList<AnalyzerNode> ListChildren(AnalyzerNode node)
    {
        switch (node)
        {
            case AnalyzerSymbolNode symbol:
                return Analysis.EnsureChildren(symbol);
            case AnalyzerKindNode kind:
                AskToRun(kind);
                return ListResults(kind);
            case AnalyzerMessageNode or AnalyzerPendingNode or AnalyzerFailedNode:
                return [];
            default:
                throw new UnreachableException();
        }
    }

    /// <summary>Asks once for <paramref name="kind"/> to run, unless it already started.</summary>
    private void AskToRun(AnalyzerKindNode kind)
    {
        if (kind.HasStarted)
        {
            return;
        }

        if (!_asked.Add(kind))
        {
            return;
        }

        AppCommands.Enqueue(AppCommandFactory.Create(kind, static (a, k) => Commands.StartAnalysis(a, k.StartRun())));
    }

    private IReadOnlyList<AnalyzerNode> ListResults(AnalyzerKindNode kind)
    {
        if (kind.IsDone)
        {
            return kind.Found;
        }

        var results = new List<AnalyzerNode>(kind.Found);
        results.Add(Loading);
        return results;
    }

    public TreeRow Row(AnalyzerNode node)
    {
        return node switch
        {
            AnalyzerSymbolNode symbol => CreateSymbolRow(symbol.Symbol),
            AnalyzerKindNode kind => CreateKindRow(kind),
            AnalyzerMessageNode message => CreateMessageRow(message),
            AnalyzerPendingNode => CreateLoadingRow(),
            AnalyzerFailedNode failed => CreateFailedRow(failed),
            _ => throw new UnreachableException(),
        };
    }

    /// <summary>What tells a row apart from its siblings, for tests to find it by: its label, but an analysis
    /// by its header alone, since the count after it changes once it is done.</summary>
    public string KeyOf(AnalyzerNode node)
    {
        return node switch
        {
            AnalyzerSymbolNode symbol => symbol.Symbol.Label,
            AnalyzerKindNode kind => AnalyzerText.Header(_strings, kind.Kind),
            AnalyzerMessageNode message => GetMessageText(message),
            AnalyzerPendingNode => _strings.AnalyzerLoading,
            AnalyzerFailedNode failed => GetFailedText(failed),
            _ => throw new UnreachableException(),
        };
    }

    private TreeRow CreateSymbolRow(AnalysisSymbol symbol)
    {
        var icon = NodeIcons.Icon(NodeIcons.Of(symbol.Shape), symbol.Accessibility, _iconColors, _icons);
        return new TreeRow(symbol.Label, icon, Tooltip: symbol.Location, Secondary: symbol.Location);
    }

    private TreeRow CreateKindRow(AnalyzerKindNode kind)
    {
        var icon = new UiIcon(_icons.AnalyzerAnalysis, _theme.TextMuted);
        return new TreeRow(GetKindLabel(kind), icon);
    }

    private string GetKindLabel(AnalyzerKindNode kind)
    {
        var header = AnalyzerText.Header(_strings, kind.Kind);
        if (!kind.IsDone)
        {
            return header;
        }

        return $"{header} ({kind.Found.Count})";
    }

    private TreeRow CreateMessageRow(AnalyzerMessageNode message)
    {
        var unreadable = message.Unreadable;
        if (unreadable is not null)
        {
            var warning = new UiIcon(_icons.StatusError, _theme.Warning);
            return new TreeRow(_strings.AnalyzerUnreadable(unreadable.Name, unreadable.Why), warning, _theme.Warning);
        }

        if (message.Error is null)
        {
            return CreateLoadingRow();
        }

        var icon = new UiIcon(_icons.StatusError, _theme.Danger);
        return new TreeRow(message.Error, icon, _theme.Danger);
    }

    /// <summary>What the row of <paramref name="message"/> says.</summary>
    private string GetMessageText(AnalyzerMessageNode message)
    {
        var unreadable = message.Unreadable;
        if (unreadable is not null)
        {
            return _strings.AnalyzerUnreadable(unreadable.Name, unreadable.Why);
        }

        return message.Error ?? _strings.AnalyzerLoading;
    }

    /// <summary>The row of a restored analysis that failed: why, as a warning, in its place.</summary>
    private TreeRow CreateFailedRow(AnalyzerFailedNode failed)
    {
        var warning = new UiIcon(_icons.StatusError, _theme.Warning);
        return new TreeRow(GetFailedText(failed), warning, _theme.Warning);
    }

    /// <summary>What the row of <paramref name="failed"/> says: that its module couldn't be read, that its symbol is no
    /// longer in its file, or why looking it up failed.</summary>
    private string GetFailedText(AnalyzerFailedNode failed)
    {
        var anchor = failed.SavedAs;
        return failed.Failure.Match(
            unreadable => _strings.AnalyzerUnreadable(unreadable.Name, unreadable.Why),
            _ => _strings.ErrorNoLongerThere($"{anchor.FilePath.FileName} ({anchor.Token:X8})"),
            lookup => _strings.AnalyzerUnreadable(anchor.FilePath.FileName, lookup.Why));
    }

    private TreeRow CreateLoadingRow()
    {
        var icon = new UiIcon(_icons.StatusLoading, _theme.TextMuted);
        return new TreeRow(_strings.AnalyzerLoading, icon, _theme.TextMuted, Spinning: true);
    }
}
