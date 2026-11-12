using ILSight.Core;

namespace ILSight;

/// <summary>Something navigated to: a node's place, or a diagram.</summary>
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
}
