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
        var root = new AnalyzerSymbolNode(symbol);
        tree.Roots.Add(root);
        tree.Revision++;
        tree.Changes++;
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
        var root = new AnalyzerPendingNode(savedAs);
        tree.Roots.Add(root);
        if (savedAs != null)
            tree.Revision++;
        tree.Changes++;
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
        var root = new AnalyzerSymbolNode(symbol);
        tree.Roots[index] = root;
        if (symbol.Definition != pending.SavedAs)
            tree.Revision++;
        tree.Changes++;
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
        tree.Changes++;
        root.Cancel();
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
            root.Cancel();
        tree.Roots.Clear();
        tree.Revision++;
        tree.Changes++;
    }

    private static CodeAnchor? SavedAnalysisOf(AnalyzerNode root)
    {
        return root switch
        {
            AnalyzerSymbolNode symbol => symbol.Symbol.Definition,
            AnalyzerPendingNode pending => pending.SavedAs,
            _ => null,
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
        kind.StartOnce(appState.Assemblies, () => NoteFindings(tree), error => ReportAnalysisFailure(appState, kind, error),
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
}
