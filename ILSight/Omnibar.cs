using ILSight.Core;

namespace ILSight;

/// <summary>The bar over each document, one for all of them: which document's bar searches instead of
/// showing its path, the best matches it suggests, and the list of a crumb's children open under it.</summary>
public sealed class Omnibar
{
    /// <summary>How many of the best matches the search suggests.</summary>
    public const int MaxSuggestions = 10;

    /// <summary>The search a bar runs, or null while every bar shows its path.</summary>
    public OmnibarSearch? Searching;

    /// <summary>The query typed into the bar and its matches, the closest first.</summary>
    public required Search Search { get; init; }

    /// <summary>The document panel whose bar searches, or null while every bar shows its path.</summary>
    public string? SearchingPanel => Searching?.Panel;

    /// <summary>The children of a crumb, open under it, or null while no list is open.</summary>
    public CrumbMenu? Menu { get; set; }

    public bool IsSearching => Searching is not null;

    /// <summary>How many matches are suggested.</summary>
    public int SuggestionCount => Math.Min(Search.Rows.Count, MaxSuggestions);

    /// <summary>The suggestion selected, or null when none of those suggested is.</summary>
    public SearchResult? SelectedSuggestion
    {
        get
        {
            var index = ItemSelections.FindIndexIn(Search.Selection, Search.Rows);
            if (index < 0)
            {
                return null;
            }

            if (index >= SuggestionCount)
            {
                return null;
            }

            return Search.Rows[index];
        }
    }

    /// <summary>The search one bar runs, and whether its box is yet to take focus.</summary>
    public sealed class OmnibarSearch
    {
        public required string Panel { get; init; }

        public bool IsFocusPending = true;
    }
}
