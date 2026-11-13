using System.Diagnostics;

namespace ILSight;

/// <summary>What asking the analyzer to analyze a symbol came to (<see cref="AnalyzerTree.AddRoot"/>): the root
/// analyzing it; a root waiting while its assembly, which the list neither held nor had on its way, is listed, for the
/// edge to start; or refused, with why.</summary>
[MustUse]
public abstract record AnalyzerAdd
{
    private AnalyzerAdd()
    {
    }

    /// <summary>The root analyzing the symbol: added, or the one analyzing it already.</summary>
    public sealed record Analyzing : AnalyzerAdd
    {
        public required AnalyzerSymbolNode Root { get; init; }
    }

    /// <summary>A root waiting, shown as loading, while the symbol's assembly is listed as following a reference into
    /// it lists it, the analysis landing then: for the edge to start (<see cref="AnalyzeReferenceHandler"/>).</summary>
    public sealed record Listing : AnalyzerAdd
    {
        public required PendingAnalysis Pending { get; init; }
    }

    /// <summary>Nothing was added: the symbol's assembly isn't on the list and can't be listed, as
    /// <see cref="Why"/> tells.</summary>
    public sealed record Refused : AnalyzerAdd
    {
        public required Refusal Why { get; init; }
    }

    /// <summary>What the handler of whichever case this is makes of it.</summary>
    public TResult Match<TResult>(Func<Analyzing, TResult> analyzing, Func<Listing, TResult> listing, Func<Refused, TResult> refused)
    {
        return this switch
        {
            Analyzing c => analyzing(c),
            Listing c => listing(c),
            Refused c => refused(c),
            _ => throw new UnreachableException($"AnalyzerAdd.Match does not handle {GetType().Name}."),
        };
    }

    /// <summary>Does what the handler of whichever case this is does with it.</summary>
    public void Switch(Action<Analyzing> analyzing, Action<Listing> listing, Action<Refused> refused)
    {
        switch (this)
        {
            case Analyzing c:
                analyzing(c);
                return;
            case Listing c:
                listing(c);
                return;
            case Refused c:
                refused(c);
                return;
            default:
                throw new UnreachableException($"AnalyzerAdd.Switch does not handle {GetType().Name}.");
        }
    }
}
