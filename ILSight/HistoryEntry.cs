using ILSight.Core;
using ILSight.AssemblyArea;
using ILSight.ExportArea;

namespace ILSight;

/// <summary>Something the user asked a tab to show: a node's place, a diagram, a comparison or an export's
/// report.</summary>
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

    /// <summary>A diagram worked in, under the title of its tab.</summary>
    public sealed record Diagram(TypeDiagram Shown, string Title) : HistoryEntry;

    /// <summary>A comparison of two assemblies' APIs worked in, under the title of its tab.</summary>
    public sealed record Comparison : HistoryEntry
    {
        public required AssemblyComparison Shown { get; init; }

        public required string Title { get; init; }
    }

    /// <summary>An export's report, shown in a tab of its own.</summary>
    public sealed record Report : HistoryEntry
    {
        public required ExportReport Shown { get; init; }
    }
}
