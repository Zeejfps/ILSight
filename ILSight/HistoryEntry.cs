using ILSight.Core;
using ILSight.Core.ComparisonArea;
using ILSight.AssemblyArea;
using ILSight.ExportArea;

namespace ILSight;

/// <summary>Something the user asked a tab to show, held as a value naming its place, never the document itself, so
/// that going back to it shows it again however its tab went: a node's place, a diagram's types as it was left, a
/// comparison's assemblies and what it took in, or which export's report it was.</summary>
public union HistoryEntry(HistoryEntry.Node, HistoryEntry.Diagram, HistoryEntry.Comparison, HistoryEntry.Report)
{
    /// <summary>A place navigated to, the icon and accessibility its node showed, the language it was shown
    /// in, where its view stood when it was left, and what of the tab it was left from it was shown as.</summary>
    public sealed record Node
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
    public sealed record Diagram
    {
        public required int Id { get; init; }

        public required TabTitle Title { get; init; }

        public required IReadOnlyList<TypeAnchor> Types { get; init; }

        public required IReadOnlyList<AssemblyPath> HomeAssemblies { get; init; }
    }

    /// <summary>A comparison of two assemblies' APIs worked in, under the title of its tab: the id the navigation gave
    /// it, which finds the tab still showing it, and the assemblies it compared and what it took in as it was left,
    /// which compare them again once no tab does.</summary>
    public sealed record Comparison
    {
        public required int Id { get; init; }

        public required TabTitle Title { get; init; }

        public required AssemblyPath LeftPath { get; init; }

        public required AssemblyPath RightPath { get; init; }

        public required ApiScope Scope { get; init; }
    }

    /// <summary>An export's report, shown in a tab of its own: which export it was, found again among the finished
    /// ones while the exports panel has it.</summary>
    public sealed record Report
    {
        public required ExportRun Run { get; init; }
    }
}
