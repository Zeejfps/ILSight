using System.Diagnostics;
using System.Globalization;

namespace ILSight.Core;

/// <summary>What a <see cref="SearchKinds.Literals"/> search looks for: the number the query's
/// single term is, or else text the query matches.</summary>
public abstract record SearchLiteral
{
    private SearchLiteral()
    {
    }

    /// <summary>An integer, decimal or <c>0x</c> hex: matches integer constants and loads of any width.</summary>
    public sealed record Integer : SearchLiteral
    {
        public required long Value { get; init; }
    }

    /// <summary>A number with an <c>f</c> suffix: matches <see cref="float"/> constants and loads.</summary>
    public sealed record Single : SearchLiteral
    {
        public required float Value { get; init; }
    }

    /// <summary>A number with a point or an exponent: matches <see cref="double"/> constants and loads.</summary>
    public sealed record Double : SearchLiteral
    {
        public required double Value { get; init; }
    }

    /// <summary>No number: matches strings, and constants written as text, that the query matches.</summary>
    public sealed record Text : SearchLiteral;

    public TResult Match<TResult>(Func<Integer, TResult> integer, Func<Single, TResult> single, Func<Double, TResult> @double, Func<Text, TResult> text)
    {
        return this switch
        {
            Integer i => integer(i),
            Single s => single(s),
            Double d => @double(d),
            Text t => text(t),
            _ => throw new UnreachableException($"SearchLiteral.Match does not handle {GetType().Name}."),
        };
    }

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

        var number = FindNumber(term);
        if (number is null)
        {
            return new Text();
        }

        return number;
    }

    private static SearchLiteral? FindNumber(string term)
    {
        if (IsHex(term))
        {
            return FindHex(term);
        }

        var integer = FindInteger(term);
        if (integer is not null)
        {
            return integer;
        }

        if (IsSingle(term))
        {
            return FindSingle(term[..^1]);
        }

        return FindDouble(term);
    }

    private static bool IsHex(string term)
    {
        return term.StartsWith("0x", StringComparison.OrdinalIgnoreCase) || term.StartsWith("-0x", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSingle(string term)
    {
        return term.Length > 1 && term[^1] is 'f' or 'F';
    }

    private static Integer? FindHex(string term)
    {
        var negative = term[0] == '-';
        var digits = term[(negative ? 3 : 2)..].TrimEnd('u', 'U', 'l', 'L');
        if (!ulong.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var hex))
        {
            return null;
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

    private static Single? FindSingle(string digits)
    {
        if (!float.TryParse(digits, NumberStyles.Float, CultureInfo.InvariantCulture, out var single))
        {
            return null;
        }

        return new Single { Value = single };
    }

    private static Double? FindDouble(string term)
    {
        var digits = term.Length > 1 && term[^1] is 'd' or 'D' ? term[..^1] : term;
        if (!digits.Contains('.') && !digits.Contains('e') && !digits.Contains('E'))
        {
            return null;
        }

        if (!double.TryParse(digits, NumberStyles.Float, CultureInfo.InvariantCulture, out var real))
        {
            return null;
        }

        return new Double { Value = real };
    }
}
