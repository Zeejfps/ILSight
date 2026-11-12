using ZGF.Gui.Immediate.Widgets;
using ZGF.Gui.Immediate;
using System.Diagnostics;
using ILSight.Core;
using ILSight.Core.Analysis;
using ILSight.CommandArea;

namespace ILSight;

/// <summary>Analyzing types and members, and analyzing again what was analyzed in the last run.</summary>
public static class Analysis
{
    /// <summary>The children of <paramref name="symbol"/>, listed the first time they are asked for.</summary>
    public static List<AnalyzerNode> EnsureChildren(AnalyzerSymbolNode symbol)
    {
        if (symbol.Children is null)
        {
            symbol.Children = ListChildren(symbol);
        }

        return symbol.Children;
    }

    private static List<AnalyzerNode> ListChildren(AnalyzerSymbolNode symbol)
    {
        var children = new List<AnalyzerNode>();
        foreach (var accessor in symbol.Symbol.Accessors)
        {
            children.Add(new AnalyzerSymbolNode { Symbol = accessor });
        }

        foreach (var kind in symbol.Symbol.Kinds)
        {
            children.Add(new AnalyzerKindNode { Subject = symbol.Symbol, Kind = kind });
        }

        return children;
    }

    /// <summary>Cancels the analyses running under <paramref name="node"/>, leaving children never listed
    /// unlisted.</summary>
    internal static void CancelNode(AnalyzerNode node)
    {
        switch (node)
        {
            case AnalyzerSymbolNode symbol:
                CancelAll(symbol.Children);
                break;
            case AnalyzerKindNode kind:
                CancelRun(kind);
                CancelAll(kind.Found);
                break;
            case AnalyzerMessageNode or AnalyzerPendingNode:
                break;
            default:
                throw new UnreachableException();
        }
    }

    private static void CancelAll(List<AnalyzerNode>? nodes)
    {
        if (nodes is null)
        {
            return;
        }

        foreach (var node in nodes)
        {
            CancelNode(node);
        }
    }

    private static void CancelRun(AnalyzerKindNode kind)
    {
        if (kind.Run is not AnalyzerRun.Running running)
        {
            return;
        }

        kind.Run = new AnalyzerRun.Stopped();
        running.Work.Cancel();
    }

    /// <summary>Marks the analysis <paramref name="kind"/> stands for as running, unless it already started. Returns
    /// the run, for <see cref="RunAnalysisHandler"/> to run over the open assemblies; null when it started
    /// before.</summary>
    public static AnalysisRun? StartRun(AnalyzerKindNode kind)
    {
        if (kind.HasStarted)
        {
            return null;
        }

        var work = new CancellationTokenSource();
        kind.Run = new AnalyzerRun.Running(work);
        return new AnalysisRun { Kind = kind, Work = work };
    }

    /// <summary>Adds <paramref name="found"/>, what <paramref name="run"/> found so far, to its analysis, and notes in
    /// <paramref name="analyzer"/> that it found more; nothing once the run was stopped.</summary>
    public static void AddFound(AnalyzerTree analyzer, AnalysisRun run, IReadOnlyList<AnalysisFind> found)
    {
        var kind = run.Kind;
        if (kind.Work != run.Work)
        {
            return;
        }

        kind.Found.AddRange(CreateNodes(found));
        analyzer.NoteFindings();
    }

    /// <summary>Ends the analysis <paramref name="run"/> filled as done, with a row telling <paramref name="error"/>
    /// when it failed, and notes it in <paramref name="analyzer"/>; nothing once the run was stopped.</summary>
    public static void FinishRun(AnalyzerTree analyzer, AnalysisRun run, string? error)
    {
        var kind = run.Kind;
        if (kind.Work != run.Work)
        {
            return;
        }

        if (error is not null)
        {
            kind.Found.Add(new AnalyzerMessageNode { Error = error });
        }

        kind.Run = new AnalyzerRun.Done();
        run.Work.Dispose();
        analyzer.NoteFindings();
    }

    private static List<AnalyzerNode> CreateNodes(IReadOnlyList<AnalysisFind> found)
    {
        return found
            .Select(static f => f.Match<AnalyzerNode>(static s => new AnalyzerSymbolNode { Symbol = s.Symbol }, static u => new AnalyzerMessageNode { Unreadable = u }))
            .ToList();
    }

    /// <summary>Analyzes the type or member <paramref name="node"/> stands for in the analyzer. Returns
    /// the root analyzing it, or null when it cannot be analyzed.</summary>
    public static AnalyzerSymbolNode? AddToAnalyzer(AppState appState, BrowseNode node)
    {
        var symbol = node.Analysis;
        if (symbol is null)
        {
            return null;
        }

        return AddToAnalyzer(appState, symbol);
    }

    /// <summary>Analyzes <paramref name="symbol"/> in the analyzer. Returns the root analyzing it, the one
    /// already there when it was analyzed before; null when its assembly is no longer on the list.</summary>
    public static AnalyzerSymbolNode? AddToAnalyzer(AppState appState, AnalysisSymbol symbol)
    {
        return appState.Analyzer.AddRoot(symbol);
    }

    /// <summary>Adds a root to <paramref name="analyzer"/> that shows as loading while what
    /// <paramref name="reference"/> points at is found. Returns it with the reference, for
    /// <see cref="AnalyzeReferenceHandler"/> to find and analyze.</summary>
    public static PendingAnalysis BeginAnalyzing(AnalyzerTree analyzer, CodeReference.Navigable reference)
    {
        return new PendingAnalysis { Root = analyzer.AddPending(savedAs: null), Reference = reference };
    }

    /// <summary>The roots of <paramref name="analyzer"/> restored from the saved analyses that still wait for their
    /// symbols, with the generation of <paramref name="assemblies"/> now, for <see cref="RestoreAnalysesHandler"/> to
    /// find; null when none waits.</summary>
    public static AnalysesRestore? BeginRestoring(AnalyzerTree analyzer, AssemblyList assemblies)
    {
        var pending = analyzer.ListRestoring();
        if (pending.Count == 0)
        {
            return null;
        }

        return new AnalysesRestore
        {
            Roots = pending.ConvertAll(static r => r.Root),
            Analyzed = pending.ConvertAll(static r => r.SavedAs),
            Generation = assemblies.Generation,
        };
    }

    /// <summary>Turns <paramref name="root"/> into the analysis of what <paramref name="node"/> stands for, which
    /// <paramref name="viewCommands"/> has the view show; removes it when that cannot be analyzed.</summary>
    public static void AnalyzeFound(AnalyzerTree analyzer, Queue<ViewCommand> viewCommands, AnalyzerPendingNode root, BrowseNode node)
    {
        var symbol = node.Analysis;
        if (symbol is null)
        {
            analyzer.RemoveRoot(root);
            return;
        }
        ResolveAnalysis(analyzer, viewCommands, root, symbol);
    }

    /// <summary>Turns <paramref name="root"/> into the analysis of <paramref name="symbol"/>, and commands
    /// the view to show the root that analyzes it.</summary>
    private static void ResolveAnalysis(AnalyzerTree analyzer, Queue<ViewCommand> viewCommands, AnalyzerPendingNode root, AnalysisSymbol symbol)
    {
        var shown = analyzer.ResolveRoot(root, symbol);
        if (shown is null)
        {
            return;
        }

        viewCommands.Enqueue(new ViewCommand.ShowAnalysis(shown));
    }

    /// <summary>Turns each of <paramref name="restoring"/> into the analysis of the symbol found for it, in order, or
    /// removes it when none was, or its symbol is in a module whose type system the library could not read.</summary>
    public static void AnalyzeAgain(AnalyzerTree analyzer, Queue<ViewCommand> viewCommands, IReadOnlyList<AnalyzerPendingNode> restoring,
        List<Lookup<AnalysisSymbol>> found)
    {
        for (var i = 0; i < restoring.Count; i++)
        {
            AnalyzeAgain(analyzer, viewCommands, restoring[i], found[i]);
        }
    }

    /// <summary>Removes each of <paramref name="restoring"/> from <paramref name="analyzer"/>.</summary>
    public static void RemoveRoots(AnalyzerTree analyzer, IReadOnlyList<AnalyzerPendingNode> restoring)
    {
        foreach (var root in restoring)
        {
            analyzer.RemoveRoot(root);
        }
    }

    private static void AnalyzeAgain(AnalyzerTree analyzer, Queue<ViewCommand> viewCommands, AnalyzerPendingNode root, Lookup<AnalysisSymbol> found)
    {
        if (found is not Lookup<AnalysisSymbol>.Found symbol)
        {
            analyzer.RemoveRoot(root);
            return;
        }
        ResolveAnalysis(analyzer, viewCommands, root, symbol.Value);
    }

    public static void SetRowStyle(AnalyzerRows rows, UiTheme theme, NodeIconPalette iconColors, AppIcons icons, Strings strings)
    {
        rows.Theme = theme;
        rows.IconColors = iconColors;
        rows.Icons = icons;
        rows.Strings = strings;
    }

    /// <summary>Shows the roots of <paramref name="analyzer"/>, listing the rows again when the roots changed,
    /// dropping a selection whose root is gone, or when an analysis found more.</summary>
    public static void FollowTree(AnalyzerRows rows, AnalyzerTree analyzer)
    {
        rows.Roots = analyzer.Roots;
        rows.Asked.Clear();
        FollowRoots(rows, analyzer);
        FollowFindings(rows, analyzer);
    }

    private static void FollowRoots(AnalyzerRows rows, AnalyzerTree analyzer)
    {
        if (analyzer.Changes == rows.SeenChanges)
        {
            return;
        }

        rows.SeenChanges = analyzer.Changes;
        rows.View.Refresh();
        DropSelectionOfRemovedRoot(rows, analyzer);
    }

    private static void DropSelectionOfRemovedRoot(AnalyzerRows rows, AnalyzerTree analyzer)
    {
        if (!rows.View.TryGetSelectedRoot(out var root))
        {
            return;
        }

        if (analyzer.HasRoot(root))
        {
            return;
        }

        rows.View.ClearSelection();
    }

    private static void FollowFindings(AnalyzerRows rows, AnalyzerTree analyzer)
    {
        if (analyzer.Findings == rows.SeenFindings)
        {
            return;
        }

        rows.SeenFindings = analyzer.Findings;
        rows.View.Refresh();
    }

    /// <summary>Expands <paramref name="root"/> and selects it.</summary>
    public static void ShowRoot(AnalyzerRows rows, AnalyzerSymbolNode root)
    {
        rows.View.SetExpanded(root, true);
        rows.View.Refresh();
        rows.View.Select(root);
    }

    /// <summary>The root the selection sits under, or null when nothing showing is selected.</summary>
    public static AnalyzerNode? FindSelectedRoot(AnalyzerRows rows)
    {
        if (!rows.View.TryGetSelectedRoot(out var root))
        {
            return null;
        }

        return root;
    }

    /// <summary>Expands every root found and each analysis of it, running those not run yet.</summary>
    public static void ExpandAllRows(AnalyzerRows rows)
    {
        foreach (var root in rows.Roots)
        {
            if (root is AnalyzerSymbolNode symbol)
            {
                ExpandWithAnalyses(rows, symbol);
            }
        }
    }

    private static void ExpandWithAnalyses(AnalyzerRows rows, AnalyzerSymbolNode symbol)
    {
        rows.View.SetExpanded(symbol, true);
        foreach (var child in Analysis.EnsureChildren(symbol))
        {
            if (child is AnalyzerSymbolNode accessor)
            {
                ExpandWithAnalyses(rows, accessor);
            }
            else
            {
                rows.View.SetExpanded(child, true);
            }
        }
    }

    public static void CollapseAllRows(AnalyzerRows rows)
    {
        rows.View.CollapseAll();
    }

    public static bool HasChildren(AnalyzerRows rows, AnalyzerNode node)
    {
        return node switch
        {
            AnalyzerSymbolNode symbol => Analysis.EnsureChildren(symbol).Count > 0,
            AnalyzerKindNode kind => !kind.IsDone || kind.Found.Count > 0,
            AnalyzerMessageNode or AnalyzerPendingNode => false,
            _ => throw new UnreachableException(),
        };
    }

    public static IReadOnlyList<AnalyzerNode> ListChildren(AnalyzerRows rows, AnalyzerNode node)
    {
        switch (node)
        {
            case AnalyzerSymbolNode symbol:
                return Analysis.EnsureChildren(symbol);
            case AnalyzerKindNode kind:
                AskToRun(rows, kind);
                return ListResults(rows, kind);
            case AnalyzerMessageNode or AnalyzerPendingNode:
                return [];
            default:
                throw new UnreachableException();
        }
    }

    /// <summary>Asks once for <paramref name="kind"/> to run, unless it already started.</summary>
    private static void AskToRun(AnalyzerRows rows, AnalyzerKindNode kind)
    {
        if (kind.HasStarted)
        {
            return;
        }

        if (!rows.Asked.Add(kind))
        {
            return;
        }

        rows.AppCommands.Enqueue(Commands.Create(kind, static (a, k) => Commands.StartAnalysis(a, Analysis.StartRun(k))));
    }

    private static IReadOnlyList<AnalyzerNode> ListResults(AnalyzerRows rows, AnalyzerKindNode kind)
    {
        if (kind.IsDone)
        {
            return kind.Found;
        }

        var results = new List<AnalyzerNode>(kind.Found);
        results.Add(rows.Loading);
        return results;
    }

    public static TreeRow GetRow(AnalyzerRows rows, AnalyzerNode node)
    {
        return node switch
        {
            AnalyzerSymbolNode symbol => CreateSymbolRow(rows, symbol.Symbol),
            AnalyzerKindNode kind => CreateKindRow(rows, kind),
            AnalyzerMessageNode message => CreateMessageRow(rows, message),
            AnalyzerPendingNode => CreateLoadingRow(rows),
            _ => throw new UnreachableException(),
        };
    }

    /// <summary>What tells a row apart from its siblings, for tests to find it by: its label, but an analysis
    /// by its header alone, since the count after it changes once it is done.</summary>
    public static string GetKey(AnalyzerRows rows, AnalyzerNode node)
    {
        return node switch
        {
            AnalyzerSymbolNode symbol => symbol.Symbol.Label,
            AnalyzerKindNode kind => AnalyzerText.Header(rows.Strings, kind.Kind),
            AnalyzerMessageNode message => GetMessageText(rows, message),
            AnalyzerPendingNode => rows.Strings.AnalyzerLoading,
            _ => throw new UnreachableException(),
        };
    }

    private static TreeRow CreateSymbolRow(AnalyzerRows rows, AnalysisSymbol symbol)
    {
        var icon = NodeIcons.Icon(NodeIcons.Of(symbol.Shape), symbol.Accessibility, rows.IconColors, rows.Icons);
        return new TreeRow(symbol.Label, icon, Tooltip: symbol.Location, Secondary: symbol.Location);
    }

    private static TreeRow CreateKindRow(AnalyzerRows rows, AnalyzerKindNode kind)
    {
        var icon = new UiIcon(rows.Icons.AnalyzerAnalysis, rows.Theme.TextMuted);
        return new TreeRow(GetKindLabel(rows, kind), icon);
    }

    private static string GetKindLabel(AnalyzerRows rows, AnalyzerKindNode kind)
    {
        var header = AnalyzerText.Header(rows.Strings, kind.Kind);
        if (!kind.IsDone)
        {
            return header;
        }

        return $"{header} ({kind.Found.Count})";
    }

    private static TreeRow CreateMessageRow(AnalyzerRows rows, AnalyzerMessageNode message)
    {
        var unreadable = message.Unreadable;
        if (unreadable is not null)
        {
            var warning = new UiIcon(rows.Icons.StatusError, rows.Theme.Warning);
            return new TreeRow(rows.Strings.AnalyzerUnreadable(unreadable.Name, unreadable.Why), warning, rows.Theme.Warning);
        }

        if (message.Error is null)
        {
            return CreateLoadingRow(rows);
        }

        var icon = new UiIcon(rows.Icons.StatusError, rows.Theme.Danger);
        return new TreeRow(message.Error, icon, rows.Theme.Danger);
    }

    /// <summary>What the row of <paramref name="message"/> says.</summary>
    private static string GetMessageText(AnalyzerRows rows, AnalyzerMessageNode message)
    {
        var unreadable = message.Unreadable;
        if (unreadable is not null)
        {
            return rows.Strings.AnalyzerUnreadable(unreadable.Name, unreadable.Why);
        }

        return message.Error ?? rows.Strings.AnalyzerLoading;
    }

    private static TreeRow CreateLoadingRow(AnalyzerRows rows)
    {
        var icon = new UiIcon(rows.Icons.StatusLoading, rows.Theme.TextMuted);
        return new TreeRow(rows.Strings.AnalyzerLoading, icon, rows.Theme.TextMuted, Spinning: true);
    }
}
