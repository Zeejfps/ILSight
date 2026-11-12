using System.Diagnostics;
using ILSight.Core;
using ILSight.Core.Analysis;

namespace ILSight;

/// <summary>
/// The analyzer's tree: the symbols the user asked to analyze, newest last, each standing as a
/// <see cref="AnalyzerPendingNode"/> until it is found, with the analyses under them. Results stream
/// in from the background and keep the order the analysis finds them in, or ILSpy's sorting when it
/// asks for one. The roots change only through its methods, which keep the counts below, and an analysis is of an
/// assembly the list holds: an add that lands after its assembly was closed, or stopped arriving, is refused.
/// </summary>
public sealed class AnalyzerTree
{
    private readonly TrackedList<AnalyzerNode> _roots = new();

    /// <summary>The open list, read to check each add against.</summary>
    private readonly AssemblyList _assemblies;

    /// <summary>What <see cref="ListSavedAnalyses"/> listed when <see cref="Revision"/> last changed.</summary>
    private List<CodeAnchor> _saved = [];

    /// <param name="assemblies">The open list, which the session holding both closes: read only, to check each add
    /// against.</param>
    public AnalyzerTree(AssemblyList assemblies)
    {
        _assemblies = assemblies;
    }

    /// <summary>The symbols analyzed, newest last.</summary>
    public IReadOnlyList<AnalyzerNode> Roots => _roots;

    /// <summary>Changes whenever what <see cref="ListSavedAnalyses"/> lists changes.</summary>
    public int Revision { get; private set; }

    /// <summary>Changes whenever a root is added, replaced or removed.</summary>
    public int Changes => _roots.Revision;

    /// <summary>Changes whenever an analysis under a root finds more or ends.</summary>
    public int Findings { get; private set; }

    /// <summary>True while a root waits for its symbol to be found.</summary>
    public bool IsFinding
    {
        get
        {
            foreach (var root in _roots)
            {
                if (root is AnalyzerPendingNode)
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>Counts that an analysis under a root found more or ended.</summary>
    public void NoteFindings()
    {
        Findings++;
    }

    /// <summary>Adds <paramref name="symbol"/> as a root, unless a root analyzes it already, and
    /// returns the root that does; null, adding nothing, when the list neither lists nor has on its way the file it is
    /// in.</summary>
    public AnalyzerSymbolNode? AddRoot(AnalysisSymbol symbol)
    {
        if (!IsOnTheList(symbol))
        {
            return null;
        }

        var existing = FindRoot(symbol);
        if (existing is not null)
        {
            return existing;
        }

        var root = new AnalyzerSymbolNode { Symbol = symbol };
        _roots.Add(root);
        FollowSavedAnalyses();
        return root;
    }

    private AnalyzerSymbolNode? FindRoot(AnalysisSymbol symbol)
    {
        if (symbol.Reference == null)
        {
            return null;
        }

        foreach (var root in _roots)
        {
            if (root is AnalyzerSymbolNode found && Equals(found.Symbol.Reference, symbol.Reference))
            {
                return found;
            }
        }
        return null;
    }

    /// <summary>Adds a root that waits for its symbol to be found, saved as <paramref name="savedAs"/>
    /// meanwhile, or not saved when it is null.</summary>
    public AnalyzerPendingNode AddPending(CodeAnchor? savedAs)
    {
        var root = new AnalyzerPendingNode { SavedAs = savedAs };
        _roots.Add(root);
        FollowSavedAnalyses();
        return root;
    }

    /// <summary>Adds a waiting root for each of <paramref name="analyzed"/>, in order, saved as it.</summary>
    public void AddPending(IReadOnlyList<CodeAnchor> analyzed)
    {
        foreach (var anchor in analyzed)
        {
            AddPending(anchor);
        }
    }

    /// <summary>The roots restored from the saved analyses that are still waiting for their symbols, each
    /// with the analysis it was restored from.</summary>
    public List<(AnalyzerPendingNode Root, CodeAnchor SavedAs)> ListRestoring()
    {
        var restoring = new List<(AnalyzerPendingNode Root, CodeAnchor SavedAs)>();
        foreach (var root in _roots)
        {
            if (root is not AnalyzerPendingNode pending)
            {
                continue;
            }

            if (pending.SavedAs is null)
            {
                continue;
            }

            restoring.Add((pending, pending.SavedAs.Value));
        }
        return restoring;
    }

    /// <summary>Turns the waiting <paramref name="pending"/> into the analysis of <paramref name="symbol"/>,
    /// in its place; when another root analyzes it already, <paramref name="pending"/> goes. Returns
    /// the root that analyzes <paramref name="symbol"/>, or null once <paramref name="pending"/> was removed, and when
    /// the list neither lists nor has on its way the file <paramref name="symbol"/> is in (its assembly was closed, or
    /// stopped arriving, while it was looked for): <paramref name="pending"/> goes then.</summary>
    public AnalyzerSymbolNode? ResolveRoot(AnalyzerPendingNode pending, AnalysisSymbol symbol)
    {
        var index = _roots.IndexOf(pending);
        if (index < 0)
        {
            return null;
        }

        if (!IsOnTheList(symbol))
        {
            RemoveRoot(pending);
            return null;
        }

        var existing = FindRoot(symbol);
        if (existing is not null)
        {
            RemoveRoot(pending);
            return existing;
        }
        var root = new AnalyzerSymbolNode { Symbol = symbol };
        _roots[index] = root;
        FollowSavedAnalyses();
        return root;
    }

    public bool HasRoot(AnalyzerNode root)
    {
        return _roots.Contains(root);
    }

    /// <summary>Removes <paramref name="root"/>, cancelling the analyses running under it.</summary>
    public void RemoveRoot(AnalyzerNode root)
    {
        if (!_roots.TryRemove(root))
        {
            return;
        }

        FollowSavedAnalyses();
        Analysis.CancelNode(root);
    }

    /// <summary>Removes the analyses of what is in <paramref name="item"/>, an assembly or package taken off the list,
    /// cancelling what runs under them.</summary>
    public void RemoveAnalysesOf(LoadedAssembly item)
    {
        var of = new List<AnalyzerNode>();
        foreach (var root in _roots)
        {
            if (root is AnalyzerSymbolNode analyzed && analyzed.Symbol.IsIn(item))
            {
                of.Add(root);
            }
        }

        foreach (var root in of)
        {
            RemoveRoot(root);
        }
    }

    /// <summary>Removes the analyses of what is in the file at <paramref name="path"/>, which stopped arriving before
    /// it was listed, cancelling what runs under them.</summary>
    public void RemoveAnalysesOf(AssemblyPath path)
    {
        var of = new List<AnalyzerNode>();
        foreach (var root in _roots)
        {
            if (root is AnalyzerSymbolNode analyzed && analyzed.Symbol.IsIn(path))
            {
                of.Add(root);
            }
        }

        foreach (var root in of)
        {
            RemoveRoot(root);
        }
    }

    /// <summary>Whether the list holds the file <paramref name="symbol"/> is in, landed or on its way.</summary>
    private bool IsOnTheList(AnalysisSymbol symbol)
    {
        var file = symbol.File;
        if (file is null)
        {
            return false;
        }

        return _assemblies.Holds(file);
    }

    /// <summary>Removes every root, cancelling the analyses running under them.</summary>
    public void ClearRoots()
    {
        foreach (var root in _roots)
        {
            Analysis.CancelNode(root);
        }

        _roots.Clear();
        FollowSavedAnalyses();
    }

    /// <summary>The analyses the roots save as, in their order.</summary>
    public List<CodeAnchor> ListSavedAnalyses()
    {
        var saved = new List<CodeAnchor>();
        foreach (var root in _roots)
        {
            var anchor = SavedAnalysisOf(root);
            if (anchor is not null)
            {
                saved.Add(anchor.Value);
            }
        }
        return saved;
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

    /// <summary>Changes <see cref="Revision"/> when the roots changed what <see cref="ListSavedAnalyses"/> lists.</summary>
    private void FollowSavedAnalyses()
    {
        var saved = ListSavedAnalyses();
        if (saved.SequenceEqual(_saved))
        {
            return;
        }

        _saved = saved;
        Revision++;
    }
}
