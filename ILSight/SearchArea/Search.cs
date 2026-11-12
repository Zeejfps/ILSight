using ZGF.Gui.Immediate;
using ILSight.Core;

namespace ILSight.SearchArea;

/// <summary>The search panel's query, streamed results and selection. While the panel is open a
/// query runs once typing pauses; assemblies opened later are searched and their hits added, and
/// hits in closed ones dropped, keeping the selection.</summary>
public sealed class Search
{
    /// <summary>The hits so far, in the order the sort says, of the assemblies in
    /// <see cref="SearchedAssemblies"/> only; <see cref="Searches"/> keeps them so.</summary>
    public readonly List<SearchResult> Rows = new();
    public readonly HashSet<LoadedAssembly> SearchedAssemblies = new(ReferenceEqualityComparer.Instance);
    /// <summary>The runs that may still deliver hits.</summary>
    public SearchRuns Runs { get; } = new();
    public readonly TypedText Typed = new();
    public SearchedQuery? LastSearched;

    /// <summary>Why <see cref="LastSearched"/> finds nothing, when its <c>/regex/</c> is no valid
    /// regular expression; else null.</summary>
    public SearchMatcher.InvalidRegex? InvalidRegex;

    public bool IsFocusPending;

    public string Query = "";

    /// <summary>What the query looks for, unless prefixes in it say otherwise.</summary>
    public SearchKinds Kinds = SearchKinds.TypesAndMembers;

    /// <summary>Whether results may still arrive, or be sorted into the order asked for.</summary>
    public bool IsBusy => Runs.IsRunning || Sorting.IsSorting;

    /// <summary>Whether <see cref="Rows"/> are the hits of the query as typed, rather than of one typed
    /// before it settled; more may still arrive.</summary>
    public bool ShowsQuery => LastSearched?.Query == Query;

    public ItemSelection<SearchResult> Selection { get; } = new();

    public SearchResult? SelectedRow => Selection.Item;

    /// <summary>The order the results are in and the one asked for.</summary>
    public SearchSorting Sorting { get; } = new();
}
