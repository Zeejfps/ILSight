using System.Diagnostics;
using ILSight.Core;
using ILSight.Core.Comparison;
using ILSight.AssemblyArea;
using ILSight.ExportArea;

namespace ILSight;

/// <summary>Something the user asked a tab to show, held as a value naming its place, never the document itself, so
/// that going back to it shows it again however its tab went: a node's place, a diagram's types as it was left, a
/// comparison's assemblies and what it took in, or which export's report it was.</summary>
public abstract record HistoryEntry
{
    private HistoryEntry()
    {
    }

    /// <summary>A place navigated to, the icon and accessibility its node showed, the language it was shown
    /// in, where its view stood when it was left, and what of the tab it was left from it was shown as.</summary>
    public sealed record Node : HistoryEntry
    {
        public required NodePath Place { get; init; }

        public required NodeIcon Icon { get; init; }

        public required NodeAccessibility Accessibility { get; init; }

        public required DecompilationLanguage Language { get; init; }

        /// <summary>Where its view stood when it was left; null before it was.</summary>
        public required ViewPosition? Position { get; init; }

        /// <summary>What of the tab it was left from it was shown as, which where that tab's view stood since is
        /// kept by (<see cref="NavigationHistory.KeepPosition"/>); null before it was left.</summary>
        public required TabVersion? ShownAs { get; init; }
    }

    /// <summary>A diagram worked in, under the title of its tab: the id the navigation gave it, which finds the tab
    /// still showing it, and its types and its own assemblies as it was left, which make it again once no tab
    /// does.</summary>
    public sealed record Diagram : HistoryEntry
    {
        public required int Id { get; init; }

        public required string Title { get; init; }

        public required IReadOnlyList<TypeAnchor> Types { get; init; }

        public required IReadOnlyList<AssemblyPath> HomeAssemblies { get; init; }
    }

    /// <summary>A comparison of two assemblies' APIs worked in, under the title of its tab: the id the navigation gave
    /// it, which finds the tab still showing it, and the assemblies it compared and what it took in as it was left,
    /// which compare them again once no tab does.</summary>
    public sealed record Comparison : HistoryEntry
    {
        public required int Id { get; init; }

        public required string Title { get; init; }

        public required AssemblyPath LeftPath { get; init; }

        public required AssemblyPath RightPath { get; init; }

        public required ApiScope Scope { get; init; }
    }

    /// <summary>An export's report, shown in a tab of its own: which export it was, found again among the finished
    /// ones while the exports panel has it.</summary>
    public sealed record Report : HistoryEntry
    {
        public required ExportRun Run { get; init; }
    }

    public TResult Match<TResult>(Func<Node, TResult> node, Func<Diagram, TResult> diagram, Func<Comparison, TResult> comparison,
        Func<Report, TResult> report)
    {
        return this switch
        {
            Node n => node(n),
            Diagram d => diagram(d),
            Comparison c => comparison(c),
            Report r => report(r),
            _ => throw new UnreachableException($"HistoryEntry.Match does not handle {GetType().Name}."),
        };
    }

    /// <summary>What the handler of whichever case this is makes of it and <paramref name="state"/>, which it is
    /// handed, so that a handler that needs the caller's values can be static and allocate nothing.</summary>
    public TResult Match<TState, TResult>(TState state, Func<TState, Node, TResult> node,
        Func<TState, Diagram, TResult> diagram, Func<TState, Comparison, TResult> comparison,
        Func<TState, Report, TResult> report)
    {
        return this switch
        {
            Node c => node(state, c),
            Diagram c => diagram(state, c),
            Comparison c => comparison(state, c),
            Report c => report(state, c),
            _ => throw new UnreachableException($"HistoryEntry.Match does not handle {GetType().Name}."),
        };
    }
}
