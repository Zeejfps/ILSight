using System.Diagnostics;

namespace ILSight.Core;

/// <summary>How a <see cref="SearchQuery"/> matches names.</summary>
public abstract record SearchMatcher
{
    private SearchMatcher()
    {
    }

    /// <summary>Whether <paramref name="name"/> matches.</summary>
    public abstract bool IsMatch(string name);

    /// <summary>Every term must match; none at all finds nothing.</summary>
    public sealed record Terms : SearchMatcher
    {
        public required IReadOnlyList<string> Values { get; init; }

        public override bool IsMatch(string name)
        {
            return SearchQueries.IsEveryTermIn(Values, name);
        }
    }

    /// <summary>A <c>/regex/</c> term, which the other terms give way to.</summary>
    public sealed record Regex : SearchMatcher
    {
        public required System.Text.RegularExpressions.Regex Expression { get; init; }

        public override bool IsMatch(string name)
        {
            return Expression.IsMatch(name);
        }
    }

    /// <summary>A <c>/regex/</c> term that is no valid regular expression, so nothing is found.
    /// <see cref="Message"/> says why, as .NET's parser said it.</summary>
    public sealed record InvalidRegex : SearchMatcher
    {
        public required string Message { get; init; }

        public override bool IsMatch(string name)
        {
            return false;
        }
    }

    public TResult Match<TResult>(Func<Terms, TResult> terms, Func<Regex, TResult> regex, Func<InvalidRegex, TResult> invalidRegex)
    {
        return this switch
        {
            Terms t => terms(t),
            Regex r => regex(r),
            InvalidRegex i => invalidRegex(i),
            _ => throw new UnreachableException($"SearchMatcher.Match does not handle {GetType().Name}."),
        };
    }
}
