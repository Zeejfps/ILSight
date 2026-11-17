using System.Globalization;

namespace ILSight.Core;

/// <summary>What a <see cref="SearchKinds.Literals"/> search looks for: the number the query's
/// single term is, or else text the query matches.</summary>
public union SearchLiteral(SearchLiteral.Integer, SearchLiteral.Single, SearchLiteral.Double, SearchLiteral.Text)
{
    /// <summary>An integer, decimal or <c>0x</c> hex: matches integer constants and loads of any width.</summary>
    public sealed record Integer
    {
        public required long Value { get; init; }
    }

    /// <summary>A number with an <c>f</c> suffix: matches <see cref="float"/> constants and loads.</summary>
    public sealed record Single
    {
        public required float Value { get; init; }
    }

    /// <summary>A number with a point or an exponent: matches <see cref="double"/> constants and loads.</summary>
    public sealed record Double
    {
        public required double Value { get; init; }
    }

    /// <summary>No number: matches strings, and constants written as text, that the query matches.</summary>
    public sealed record Text;

    /// <summary>The literal <paramref name="query"/> looks for: a number when its single term is
    /// one, an <see cref="Integer"/> for integers (decimal or <c>0x</c> hex), a <see cref="Single"/>
    /// with an <c>f</c> suffix, a <see cref="Double"/> otherwise; <see cref="Text"/> when it is no
    /// number, has several terms, or a regular expression.</summary>
    public static SearchLiteral FromQuery(SearchQuery query)
    {
        var term = SearchQueries.FindSingleTerm(query);
        if (term is null)
        {
            return new Text();
        }

        return ReadNumber(term);
    }

    // The number term is, else Text: each reader answers Text for a term that is no number of its kind.
    private static SearchLiteral ReadNumber(string term)
    {
        if (IsHex(term))
        {
            return ReadHex(term);
        }

        var integer = FindInteger(term);
        if (integer is not null)
        {
            return integer;
        }

        if (IsSingle(term))
        {
            return ReadSingle(term[..^1]);
        }

        return ReadDouble(term);
    }

    private static bool IsHex(string term)
    {
        return term.StartsWith("0x", StringComparison.OrdinalIgnoreCase) || term.StartsWith("-0x", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSingle(string term)
    {
        return term.Length > 1 && term[^1] is 'f' or 'F';
    }

    private static SearchLiteral ReadHex(string term)
    {
        var negative = term[0] == '-';
        var digits = term[(negative ? 3 : 2)..].TrimEnd('u', 'U', 'l', 'L');
        if (!ulong.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var hex))
        {
            return new Text();
        }

        return new Integer { Value = negative ? -(long)hex : (long)hex };
    }

    private static Integer? FindInteger(string term)
    {
        var digits = term.TrimEnd('u', 'U', 'l', 'L');
        if (long.TryParse(digits, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var signed))
        {
            return new Integer { Value = signed };
        }

        if (!ulong.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var unsigned))
        {
            return null;
        }

        // Past long.MaxValue, it wraps round as the ulong it is.
        return new Integer { Value = (long)unsigned };
    }

    private static SearchLiteral ReadSingle(string digits)
    {
        if (!float.TryParse(digits, NumberStyles.Float, CultureInfo.InvariantCulture, out var single))
        {
            return new Text();
        }

        return new Single { Value = single };
    }

    private static SearchLiteral ReadDouble(string term)
    {
        var digits = term.Length > 1 && term[^1] is 'd' or 'D' ? term[..^1] : term;
        if (!digits.Contains('.') && !digits.Contains('e') && !digits.Contains('E'))
        {
            return new Text();
        }

        if (!double.TryParse(digits, NumberStyles.Float, CultureInfo.InvariantCulture, out var real))
        {
            return new Text();
        }

        return new Double { Value = real };
    }
}
