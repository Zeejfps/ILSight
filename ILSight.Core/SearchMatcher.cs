namespace ILSight.Core;

/// <summary>How a <see cref="SearchQuery"/> matches names.</summary>
public union SearchMatcher(SearchMatcher.Terms, SearchMatcher.Regex, SearchMatcher.InvalidRegex)
{
    /// <summary>Every term must match; none at all finds nothing.</summary>
    public sealed record Terms
    {
        public required IReadOnlyList<string> Values { get; init; }
    }

    /// <summary>A <c>/regex/</c> term, which the other terms give way to.</summary>
    public sealed record Regex
    {
        public required System.Text.RegularExpressions.Regex Expression { get; init; }
    }

    /// <summary>A <c>/regex/</c> term that is no valid regular expression, so nothing is found.
    /// <see cref="Message"/> says why, as .NET's parser said it.</summary>
    public sealed record InvalidRegex
    {
        public required string Message { get; init; }
    }

    /// <summary>Whether <paramref name="name"/> matches: every term is in it, the expression matches it, or (an
    /// invalid expression) never.</summary>
    public bool IsMatch(string name)
    {
        return this switch
        {
            Terms terms => SearchQueries.IsEveryTermIn(terms.Values, name),
            Regex regex => regex.Expression.IsMatch(name),
            InvalidRegex => false,
        };
    }
}
