using ZGF.Gui.Immediate;
using ILSight.Core;

namespace ILSight;

/// <summary>The search panel's query, streamed results and selection. While the panel is open a
/// query runs once typing pauses; assemblies opened later are searched and their hits added, and
/// hits in closed ones dropped, keeping the selection.</summary>
public sealed class Search
{
    /// <summary>Where the work it starts in the background is counted until it ends.</summary>
    public required BackgroundWork Work { get; init; }

    /// <summary>Tells the user a search failed.</summary>
    public required Action<AppProblem> ReportError { get; init; }

    /// <summary>Streams a query's hits.</summary>
    public required SearchStream Stream { get; init; }

    /// <summary>The hits so far, in the order the sort says, of the assemblies in
    /// <see cref="SearchedAssemblies"/> only; <see cref="Searches"/> keeps them so.</summary>
    public readonly List<SearchResult> Rows = new();
    public readonly HashSet<LoadedAssembly> SearchedAssemblies = new(ReferenceEqualityComparer.Instance);
    public readonly List<CancellationTokenSource> Runs = new();
    public readonly TypedText Typed = new();
    public Searched? LastSearched;

    /// <summary>Why <see cref="LastSearched"/> finds nothing, when its <c>/regex/</c> is no valid
    /// regular expression; else null.</summary>
    public SearchMatcher.InvalidRegex? InvalidRegex;

    public bool IsFocusPending;

    public string Query = "";

    /// <summary>What the query looks for, unless prefixes in it say otherwise.</summary>
    public SearchKinds Kinds = SearchKinds.TypesAndMembers;

    public bool IsBusy => Runs.Count > 0;

    /// <summary>Whether <see cref="Rows"/> are the hits of the query as typed, rather than of one typed
    /// before it settled; more may still arrive.</summary>
    public bool ShowsQuery => LastSearched?.Query == Query;

    public ItemSelection<SearchResult> Selection { get; } = new();

    public SearchResult? SelectedRow => Selection.Item;

    public SearchSort Sort { get; set; }
    public bool Descending { get; set; }

    /// <summary>The query last searched for, what it looked for, and the assembly list's version it searched.</summary>
    public sealed record Searched(string Query, SearchKinds Kinds, int Version);
}
