using ILSight.Localization;
using ZGF.Gui.Immediate.Widgets;
using ZGF.Gui.Immediate;
using System.Diagnostics;
using ILSight.Core;
using ILSight.Core.Analysis;
using static ILSight.Navigation;

namespace ILSight;

/// <summary>Analyzing types and members, and analyzing again what was analyzed in the last run.</summary>
public static class Analysis
{
    internal static void NoteFindings(AnalyzerTree tree)
    {
        tree.Findings++;
    }

    /// <summary>Adds <paramref name="symbol"/> as a root, unless a root analyzes it already, and
    /// returns the root that does.</summary>
    public static AnalyzerSymbolNode AddRoot(AnalyzerTree tree, AnalysisSymbol symbol)
    {
        var existing = FindRoot(tree, symbol);
        if (existing != null)
            return existing;
        var root = new AnalyzerSymbolNode { Symbol = symbol };
        tree.Roots.Add(root);
        tree.Revision++;
        return root;
    }

    private static AnalyzerSymbolNode? FindRoot(AnalyzerTree tree, AnalysisSymbol symbol)
    {
        if (symbol.Reference == null)
            return null;
        foreach (var root in tree.Roots)
        {
            if (root is AnalyzerSymbolNode found && Equals(found.Symbol.Reference, symbol.Reference))
                return found;
        }
        return null;
    }

    /// <summary>Adds a root that waits for its symbol to be found, saved as <paramref name="savedAs"/>
    /// meanwhile, or not saved when it is null.</summary>
    public static AnalyzerPendingNode AddPending(AnalyzerTree tree, CodeAnchor? savedAs)
    {
        var root = new AnalyzerPendingNode { SavedAs = savedAs };
        tree.Roots.Add(root);
        if (savedAs != null)
            tree.Revision++;
        return root;
    }

    /// <summary>Adds a waiting root for each of <paramref name="analyzed"/>, in order, saved as it.</summary>
    public static void AddPending(AnalyzerTree tree, IReadOnlyList<CodeAnchor> analyzed)
    {
        foreach (var anchor in analyzed)
            AddPending(tree, anchor);
    }

    /// <summary>The roots restored from the saved analyses that are still waiting for their symbols.</summary>
    public static List<AnalyzerPendingNode> ListRestoring(AnalyzerTree tree)
    {
        return tree.Roots
            .OfType<AnalyzerPendingNode>()
            .Where(static p => p.SavedAs != null)
            .ToList();
    }

    /// <summary>Turns the waiting <paramref name="pending"/> into the analysis of <paramref name="symbol"/>,
    /// in its place; when another root analyzes it already, <paramref name="pending"/> goes. Returns
    /// the root that analyzes <paramref name="symbol"/>, or null once <paramref name="pending"/> was removed.</summary>
    public static AnalyzerSymbolNode? ResolveRoot(AnalyzerTree tree, AnalyzerPendingNode pending, AnalysisSymbol symbol)
    {
        var index = tree.Roots.IndexOf(pending);
        if (index < 0)
            return null;
        var existing = FindRoot(tree, symbol);
        if (existing != null)
        {
            RemoveRoot(tree, pending);
            return existing;
        }
        var root = new AnalyzerSymbolNode { Symbol = symbol };
        tree.Roots[index] = root;
        if (symbol.Definition != pending.SavedAs)
            tree.Revision++;
        return root;
    }

    public static bool HasRoot(AnalyzerTree tree, AnalyzerNode root)
    {
        return tree.Roots.Contains(root);
    }

    public static void RemoveRoot(AnalyzerTree tree, AnalyzerNode root)
    {
        if (!tree.Roots.Remove(root)) return;
        if (SavedAnalysisOf(root) != null)
            tree.Revision++;
        CancelNode(root);
    }

    /// <summary>The analyses the roots save as, in their order.</summary>
    public static List<CodeAnchor> ListSavedAnalyses(AnalyzerTree tree)
    {
        var saved = new List<CodeAnchor>();
        foreach (var root in tree.Roots)
        {
            var anchor = SavedAnalysisOf(root);
            if (anchor != null)
                saved.Add(anchor.Value);
        }
        return saved;
    }

    public static void ClearRoots(AnalyzerTree tree)
    {
        foreach (var root in tree.Roots)
            CancelNode(root);
        tree.Roots.Clear();
        tree.Revision++;
    }

    /// <summary>The children of <paramref name="symbol"/>, listed the first time they are asked for.</summary>
    public static List<AnalyzerNode> EnsureChildren(AnalyzerSymbolNode symbol)
    {
        if (symbol.Children == null)
            symbol.Children = ListChildren(symbol);
        return symbol.Children;
    }

    private static List<AnalyzerNode> ListChildren(AnalyzerSymbolNode symbol)
    {
        var children = new List<AnalyzerNode>();
        foreach (var accessor in Analyzers.AccessorsOf(symbol.Symbol))
            children.Add(new AnalyzerSymbolNode { Symbol = accessor });
        foreach (var kind in Analyzers.KindsFor(symbol.Symbol))
            children.Add(new AnalyzerKindNode { Subject = symbol.Symbol, Kind = kind });
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
        if (nodes == null)
            return;
        foreach (var node in nodes)
            CancelNode(node);
    }

    private static void CancelRun(AnalyzerKindNode kind)
    {
        var work = kind.Work;
        if (work == null)
            return;
        kind.Work = null;
        work.Cancel();
    }

    /// <summary>Runs the analysis over <paramref name="assemblies"/> unless it already started,
    /// calling <paramref name="changed"/> each time results come in. When it fails, it ends with a row
    /// telling why and hands the exception to <paramref name="failed"/>.</summary>
    internal static void StartAnalysisOnce(AnalyzerKindNode kind, AssemblyList assemblies, Action changed, Action<Exception> failed, Action<AppProblem> reportError)
    {
        if (kind.HasStarted)
            return;
        kind.HasStarted = true;
        Background.Start(RunKindAsync(kind, assemblies, changed, failed), reportError);
    }

    private static async Task RunKindAsync(AnalyzerKindNode kind, AssemblyList assemblies, Action changed, Action<Exception> failed)
    {
        var work = new CancellationTokenSource();
        kind.Work = work;
        var token = work.Token;
        try
        {
            await foreach (var batch in Analyzers.RunAsync(assemblies, kind.Subject, kind.Kind, token))
                Deliver(kind, work, CreateNodes(batch), changed);
            Finish(kind, work, [], changed);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception e)
        {
            Finish(kind, work, [new AnalyzerMessageNode { Error = e.Message }], changed);
            failed(e);
        }
    }

    private static List<AnalyzerNode> CreateNodes(IReadOnlyList<AnalysisSymbol> symbols)
    {
        return symbols
            .Select(static s => (AnalyzerNode)new AnalyzerSymbolNode { Symbol = s })
            .ToList();
    }

    private static void Deliver(AnalyzerKindNode kind, CancellationTokenSource work, List<AnalyzerNode> found, Action changed)
    {
        if (kind.Work != work)
            return;
        kind.Found.AddRange(found);
        changed();
    }

    private static void Finish(AnalyzerKindNode kind, CancellationTokenSource work, List<AnalyzerNode> found, Action changed)
    {
        if (kind.Work != work)
            return;
        kind.Found.AddRange(found);
        kind.IsDone = true;
        kind.Work = null;
        work.Dispose();
        changed();
    }

    private static CodeAnchor? SavedAnalysisOf(AnalyzerNode root)
    {
        return root switch
        {
            AnalyzerSymbolNode symbol => symbol.Symbol.Definition,
            AnalyzerPendingNode pending => pending.SavedAs,
            AnalyzerKindNode or AnalyzerMessageNode => null,
            _ => throw new UnreachableException(),
        };
    }

    /// <summary>Analyzes the type or member <paramref name="node"/> stands for in the analyzer. Returns
    /// the root analyzing it, or null when it cannot be analyzed.</summary>
    public static AnalyzerSymbolNode? AddToAnalyzer(AppState appState, BrowseNode node)
    {
        var symbol = Analyzers.Of(node);
        if (symbol == null)
            return null;
        return AddToAnalyzer(appState, symbol);
    }

    /// <summary>Analyzes <paramref name="symbol"/> in the analyzer. Returns the root analyzing it, the one
    /// already there when it was analyzed before.</summary>
    public static AnalyzerSymbolNode AddToAnalyzer(AppState appState, AnalysisSymbol symbol)
    {
        return AddRoot(appState.Analyzer, symbol);
    }

    /// <summary>Runs the analysis <paramref name="kind"/> stands for over the open assemblies in the
    /// background, unless it already started. When it fails, why is reported.</summary>
    public static void RunAnalysis(AppState appState, AnalyzerKindNode kind)
    {
        var tree = appState.Analyzer;
        StartAnalysisOnce(kind, appState.Assemblies, () => NoteFindings(tree), error => ReportAnalysisFailure(appState, kind, error),
            problem => Errors.Report(appState, problem));
    }

    private static void ReportAnalysisFailure(AppState appState, AnalyzerKindNode kind, Exception error)
    {
        Errors.Report(appState, new AppProblem.AnalysisFailed(kind.Subject.Label, kind.Kind, error));
    }

    /// <summary>Adds a root that shows as loading, in the analyzer, while what <paramref name="reference"/>
    /// points at is found in the background, opening its assembly if it is not, and turns the root into
    /// its analysis, which the view is commanded to show. The root goes when the reference does not
    /// resolve, which is reported. Once the root was removed, as clearing the list removes them all,
    /// what is found is dropped.</summary>
    public static void AddToAnalyzer(AppState appState, CodeReference.Navigable reference)
    {
        Background.Start(AddToAnalyzerAsync(appState, reference), problem => Errors.Report(appState, problem));
    }

    private static async Task AddToAnalyzerAsync(AppState appState, CodeReference.Navigable reference)
    {
        var root = AddPending(appState.Analyzer, savedAs: null);
        var assemblies = appState.Assemblies;
        var session = appState.Session;
        var found = await Background.Run(() => NodeLookup.Find(assemblies, session, reference));
        if (!HasRoot(appState.Analyzer, root))
            return;
        switch (found)
        {
            case Outcome<BrowseNode>.Ok ok when ok.Value != null:
                AnalyzeFound(appState, root, ok.Value);
                break;
            case Outcome<BrowseNode>.Ok:
                RemoveRoot(appState.Analyzer, root);
                ReportUnresolvedReference(appState, reference, null);
                break;
            case Outcome<BrowseNode>.Failed failed:
                RemoveRoot(appState.Analyzer, root);
                ReportUnresolvedReference(appState, reference, failed.Error);
                break;
            default:
                throw new UnreachableException();
        }
    }

    private static void AnalyzeFound(AppState appState, AnalyzerPendingNode root, BrowseNode node)
    {
        var symbol = Analyzers.Of(node);
        if (symbol == null)
        {
            RemoveRoot(appState.Analyzer, root);
            return;
        }
        ResolveAnalysis(appState, root, symbol);
    }

    /// <summary>Turns <paramref name="root"/> into the analysis of <paramref name="symbol"/>, and commands
    /// the view to show the root that analyzes it.</summary>
    private static void ResolveAnalysis(AppState appState, AnalyzerPendingNode root, AnalysisSymbol symbol)
    {
        var shown = ResolveRoot(appState.Analyzer, root, symbol);
        if (shown == null)
            return;
        appState.ViewCommands.Enqueue(new ViewCommand.ShowAnalysis(shown));
    }

    /// <summary>Finds the symbols of the roots restored from the saved analyses in the background,
    /// opening their assemblies' files without listing them, and turns each root into the analysis of
    /// its symbol, or removes it when its symbol is gone, unless the list was cleared meanwhile.
    /// Reports why the symbols could not be found.</summary>
    public static void RestoreAnalyses(AppState appState)
    {
        Background.Start(RestoreAnalysesAsync(appState), problem => Errors.Report(appState, problem));
    }

    private static async Task RestoreAnalysesAsync(AppState appState)
    {
        var restoring = ListRestoring(appState.Analyzer);
        if (restoring.Count == 0)
            return;
        var analyzed = restoring.ConvertAll(static r => r.SavedAs!.Value);
        var assemblies = appState.Assemblies;
        var generation = assemblies.Generation;
        var found = await Background.Run(() => FindAnalyzed(assemblies, analyzed));
        if (WasListCleared(appState, generation))
            return;
        switch (found)
        {
            case Outcome<List<AnalysisSymbol?>>.Ok ok:
                AnalyzeAgain(appState, restoring, ok.Value);
                break;
            case Outcome<List<AnalysisSymbol?>>.Failed failed:
                Errors.Report(appState, new AppProblem.AnalysesNotRestored(failed.Error));
                RemoveRoots(appState, restoring);
                break;
            default:
                throw new UnreachableException();
        }
    }

    private static bool WasListCleared(AppState appState, int generation) => appState.Assemblies.Generation != generation;

    private static List<AnalysisSymbol?> FindAnalyzed(AssemblyList assemblies, List<CodeAnchor> analyzed)
    {
        return analyzed.ConvertAll(a => Analyzers.Find(assemblies, a));
    }

    private static void AnalyzeAgain(AppState appState, List<AnalyzerPendingNode> restoring, List<AnalysisSymbol?> found)
    {
        for (var i = 0; i < restoring.Count; i++)
            AnalyzeAgain(appState, restoring[i], found[i]);
    }

    private static void RemoveRoots(AppState appState, List<AnalyzerPendingNode> restoring)
    {
        foreach (var root in restoring)
            RemoveRoot(appState.Analyzer, root);
    }

    private static void AnalyzeAgain(AppState appState, AnalyzerPendingNode root, AnalysisSymbol? symbol)
    {
        if (symbol == null)
        {
            RemoveRoot(appState.Analyzer, root);
            return;
        }
        ResolveAnalysis(appState, root, symbol);
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
            return;
        rows.SeenChanges = analyzer.Changes;
        rows.View.Refresh();
        DropSelectionOfRemovedRoot(rows, analyzer);
    }

    private static void DropSelectionOfRemovedRoot(AnalyzerRows rows, AnalyzerTree analyzer)
    {
        if (!rows.View.TryGetSelectedRoot(out var root))
            return;
        if (Analysis.HasRoot(analyzer, root))
            return;
        rows.View.ClearSelection();
    }

    private static void FollowFindings(AnalyzerRows rows, AnalyzerTree analyzer)
    {
        if (analyzer.Findings == rows.SeenFindings)
            return;
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
            return null;
        return root;
    }

    /// <summary>Expands every root found and each analysis of it, running those not run yet.</summary>
    public static void ExpandAllRows(AnalyzerRows rows)
    {
        foreach (var root in rows.Roots)
        {
            if (root is AnalyzerSymbolNode symbol)
                ExpandWithAnalyses(rows, symbol);
        }
    }

    private static void ExpandWithAnalyses(AnalyzerRows rows, AnalyzerSymbolNode symbol)
    {
        rows.View.SetExpanded(symbol, true);
        foreach (var child in Analysis.EnsureChildren(symbol))
        {
            if (child is AnalyzerSymbolNode accessor)
                ExpandWithAnalyses(rows, accessor);
            else
                rows.View.SetExpanded(child, true);
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
            return;
        if (!rows.Asked.Add(kind))
            return;
        rows.AppCommands.Enqueue(Commands.Create(kind, static (a, k) => Analysis.RunAnalysis(a, k)));
    }

    private static IReadOnlyList<AnalyzerNode> ListResults(AnalyzerRows rows, AnalyzerKindNode kind)
    {
        if (kind.IsDone)
            return kind.Found;
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
            return header;
        return $"{header} ({kind.Found.Count})";
    }

    private static TreeRow CreateMessageRow(AnalyzerRows rows, AnalyzerMessageNode message)
    {
        if (message.Error == null)
            return CreateLoadingRow(rows);
        var icon = new UiIcon(rows.Icons.StatusError, rows.Theme.Danger);
        return new TreeRow(message.Error, icon, rows.Theme.Danger);
    }

    private static TreeRow CreateLoadingRow(AnalyzerRows rows)
    {
        var icon = new UiIcon(rows.Icons.StatusLoading, rows.Theme.TextMuted);
        return new TreeRow(rows.Strings.AnalyzerLoading, icon, rows.Theme.TextMuted, Spinning: true);
    }
}
