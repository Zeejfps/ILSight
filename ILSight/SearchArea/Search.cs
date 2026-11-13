using ILSight.Core;

namespace ILSight.SearchArea;

/// <summary>The search box: the query typed in it, what it looks for, and whether it should take focus, which the
/// widgets set; and the <see cref="Results"/> the query finds, which keep their own rules. While the panel is open, the
/// results are told the query each frame, and search once typing pauses.</summary>
public sealed class Search
{
    private const string NamespaceScope = "innamespace";
    private const string AssemblyScope = "inassembly";

    /// <summary>The query as typed, which the search box's field is bound to.</summary>
    public string Query = "";

    private bool _isFocusPending;

    /// <summary>What the query looks for, unless prefixes in it say otherwise.</summary>
    public SearchKinds Kinds { get; private set; } = SearchKinds.TypesAndMembers;

    /// <summary>What the query finds.</summary>
    public SearchResults Results { get; } = new();

    /// <summary>Whether the results are the hits of the query as typed, rather than of one typed before it settled;
    /// more may still arrive.</summary>
    public bool ShowsQuery => Results.LastSearched?.Query == Query;

    /// <summary>Starts a search when the query settled, or searches assemblies opened since, as
    /// <see cref="SearchResults.Update"/> does. Runs every frame.</summary>
    public double Update(AssemblyList assemblies, double now, bool shown)
    {
        return Results.Update(Query, Kinds, assemblies, now, shown);
    }

    /// <summary>Has the query look for <paramref name="kinds"/> from now on.</summary>
    public void LookFor(SearchKinds kinds)
    {
        Kinds = kinds;
    }

    /// <summary>Empties the query, and drops a focus asked for that the box has not taken yet.</summary>
    public void Clear()
    {
        Query = "";
        _isFocusPending = false;
    }

    /// <summary>Asks for the box to take focus when it is next drawn.</summary>
    public void AskForFocus()
    {
        _isFocusPending = true;
    }

    /// <summary>True once after a focus was asked for, unless cleared since: the box should take focus.</summary>
    public bool TryTakeFocus()
    {
        var focus = _isFocusPending;
        _isFocusPending = false;
        return focus;
    }

    /// <summary>Keeps the query to <paramref name="node"/>'s assembly, or to the namespace it is.</summary>
    public void ScopeTo(BrowseNode node)
    {
        Query = CreateScopedQuery(node);
    }

    private string CreateScopedQuery(BrowseNode node)
    {
        if (node.Subject is NodeSubject.Namespace)
        {
            return SearchQueries.AddScope(Query, NamespaceScope, node.FullName);
        }

        return SearchQueries.AddScope(Query, AssemblyScope, node.Assembly.Name);
    }
}
