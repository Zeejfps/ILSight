using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ILSight.Core;

/// <summary>What a search looks for; any combination.</summary>
[Flags]
public enum SearchKinds
{
    None = 0,
    Types = 1 << 0,
    Methods = 1 << 1,
    Fields = 1 << 2,
    Properties = 1 << 3,
    Events = 1 << 4,
    /// <summary>Constants, and string and number literals in method bodies.</summary>
    Literals = 1 << 5,
    /// <summary>The definition with a metadata token, in hex.</summary>
    Tokens = 1 << 6,
    Resources = 1 << 7,
    Assemblies = 1 << 8,
    Namespaces = 1 << 9,
    Members = Methods | Fields | Properties | Events,
    TypesAndMembers = Types | Members,
    All = TypesAndMembers | Literals | Tokens | Resources | Assemblies | Namespaces,
}

/// <summary>Which of an assembly's names an <see cref="SearchKinds.Assemblies"/> search matches.</summary>
public enum AssemblyNameKind
{
    NameOrFileName,
    FilePath,
    FullName,
}

/// <summary>
/// A parsed search box query, with ILSpy's syntax. Whitespace separates terms; double quotes keep
/// one together. A term matches a name containing it, ignoring case; every term must match.
/// <list type="bullet">
/// <item><c>-term</c> must not be contained, <c>=term</c> must be the whole name, <c>~term</c>
/// matches letters in order with gaps, <c>+term</c> is a plain term.</item>
/// <item><c>/regex/</c> matches a regular expression instead, ignoring case. The first one
/// counts; when it is invalid nothing is found, and <see cref="Matcher"/> says why.</item>
/// <item>A term with a dot matches qualified names, e.g. <c>Generic.List</c>.</item>
/// <item>A term with <c>&lt;</c> matches names with their type parameters, C#-style, e.g.
/// <c>List&lt;T&gt;</c>.</item>
/// <item>Kind prefixes replace the <see cref="Kinds"/> picked, and add up when there are several:
/// <c>tm:</c> <c>t:</c> <c>m:</c> <c>md:</c> <c>f:</c> <c>p:</c> <c>e:</c> <c>c:</c> (literals)
/// <c>r:</c> <c>n:</c>, <c>a:</c> / <c>af:</c> / <c>an:</c> (assembly by name, file path, full
/// name), <c>@</c> (metadata token).</item>
/// <item><c>inassembly:</c> and <c>innamespace:</c> keep results to assemblies or namespaces
/// whose name contains the value.</item>
/// </list>
/// </summary>
public sealed class SearchQuery
{
    public required SearchKinds Kinds { get; set; }

    public AssemblyNameKind AssemblyName { get; internal set; }

    /// <summary>How names are matched: by the terms, or by the regular expression, or not at all
    /// when the expression is invalid.</summary>
    public SearchMatcher Matcher { get; internal set; } = new SearchMatcher.Terms { Values = [] };

    /// <summary>Whether names are matched qualified, namespace and declaring types included.</summary>
    public bool FullName { get; internal set; }

    /// <summary>Whether a term names generic arity, so names keep their <c>`1</c> suffix.</summary>
    public bool KeepArity { get; internal set; }

    /// <summary>Whether a term names type parameters, so generic names are written with them, e.g.
    /// <c>Dictionary&lt;TKey, TValue&gt;</c>.</summary>
    public bool Generics { get; internal set; }

    public string? InAssembly { get; internal set; }

    public string? InNamespace { get; internal set; }

    /// <summary>Whether the query can find anything: it has terms, or a valid regular expression.</summary>
    public bool CanFind => Matcher.Match(static terms => terms.Values.Count > 0, static _ => true, static _ => false);
}

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

    /// <summary>Whether <paramref name="term"/> has a <see cref="float"/>'s <c>f</c> suffix.</summary>
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

    /// <summary>A decimal integer, signed or not, with any <c>u</c> and <c>l</c> suffixes; one past
    /// <see cref="long.MaxValue"/> wraps round as the <see cref="ulong"/> it is.</summary>
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

    /// <summary>A number with a point or an exponent, and maybe a <c>d</c> suffix.</summary>
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

/// <summary>Reading a search box's text into a <see cref="SearchQuery"/>, and matching names against it.</summary>
public static class SearchQueries
{
    /// <summary>Parses <paramref name="text"/>, searching for <paramref name="kinds"/> unless a prefix
    /// says otherwise.</summary>
    public static SearchQuery ParseQuery(string text, SearchKinds kinds = SearchKinds.TypesAndMembers)
    {
        var query = new SearchQuery { Kinds = kinds };
        var prefixed = false;
        var terms = new List<string>();
        SearchMatcher? regex = null;
        foreach (var part in Split(text))
        {
            var (prefix, term) = SplitPrefix(part);
            if (term.Length >= 2 && term[0] == '"' && term[^1] == '"')
            {
                term = term[1..^1];
            }

            switch (prefix)
            {
                case "inassembly":
                    query.InAssembly ??= term;
                    continue;
                case "innamespace":
                    query.InNamespace ??= term;
                    continue;
                case not null:
                    query.Kinds = (prefixed ? query.Kinds : SearchKinds.None) | GetKinds(prefix);
                    prefixed = true;
                    query.AssemblyName = prefix switch
                    {
                        "af" => AssemblyNameKind.FilePath,
                        "an" => AssemblyNameKind.FullName,
                        _ => query.AssemblyName,
                    };
                    break;
            }

            if (regex is null && term.Length > 1 && term[0] == '/')
            {
                var pattern = term.Length > 2 && term[^1] == '/' ? term[1..^1] : term[1..];
                query.FullName |= pattern.Contains("\\.");
                query.Generics |= pattern.Contains('<');
                regex = ParseRegex(pattern);
                continue;
            }
            query.FullName |= term.Contains('.');
            query.KeepArity |= term.Contains('`');
            query.Generics |= term.Contains('<');
            terms.Add(term);
        }
        query.Matcher = regex ?? new SearchMatcher.Terms { Values = terms };
        return query;
    }

    /// <summary>Whether <paramref name="name"/> matches every term, or the regular expression.</summary>
    public static bool IsMatch(SearchQuery query, string name)
    {
        return query.Matcher.IsMatch(name);
    }

    /// <summary>Whether <paramref name="name"/> matches every one of <paramref name="terms"/>.</summary>
    internal static bool IsEveryTermIn(IReadOnlyList<string> terms, string name)
    {
        foreach (var term in terms)
        {
            if (term.Length == 0)
            {
                continue;
            }

            var rest = term.AsSpan(1);
            var matches = term[0] switch
            {
                '-' => rest.Length == 0 || !name.AsSpan().Contains(rest, StringComparison.OrdinalIgnoreCase),
                '=' => rest.Length == 0 || name.AsSpan().Equals(rest, StringComparison.OrdinalIgnoreCase),
                '~' => rest.Length == 0 || IsSubsequence(name, rest),
                '+' => name.AsSpan().Contains(rest, StringComparison.OrdinalIgnoreCase),
                _ => name.Contains(term, StringComparison.OrdinalIgnoreCase),
            };
            if (!matches)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>The query's one term, when it has exactly one and no regular expression; else null.</summary>
    public static string? FindSingleTerm(SearchQuery query)
    {
        var matcher = query.Matcher;
        if (matcher is not SearchMatcher.Terms terms || terms.Values.Count != 1)
        {
            return null;
        }

        return terms.Values[0];
    }

    /// <summary><paramref name="text"/> with its <paramref name="prefix"/> term, if any, replaced by
    /// one for <paramref name="value"/>, placed first.</summary>
    public static string AddScope(string text, string prefix, string value)
    {
        var parts = Split(text).Where(p => !p.StartsWith(prefix + ":", StringComparison.OrdinalIgnoreCase));
        var scoped = prefix + ":" + (value.Length == 0 || value.Contains(' ') ? $"\"{value}\"" : value);
        return string.Join(' ', parts.Prepend(scoped)) + " ";
    }

    private static SearchKinds GetKinds(string prefix)
    {
        return prefix switch
        {
            "@" => SearchKinds.Tokens,
            "tm" => SearchKinds.TypesAndMembers,
            "t" => SearchKinds.Types,
            "m" => SearchKinds.Members,
            "md" => SearchKinds.Methods,
            "f" => SearchKinds.Fields,
            "p" => SearchKinds.Properties,
            "e" => SearchKinds.Events,
            "c" => SearchKinds.Literals,
            "r" => SearchKinds.Resources,
            "n" => SearchKinds.Namespaces,
            "a" or "af" or "an" => SearchKinds.Assemblies,
            _ => throw new ArgumentOutOfRangeException(nameof(prefix), prefix, null),
        };
    }

    private static readonly HashSet<string> Prefixes =
        ["tm", "t", "m", "md", "f", "p", "e", "c", "r", "n", "a", "af", "an", "inassembly", "innamespace"];

    private static (string? Prefix, string Term) SplitPrefix(string part)
    {
        if (part.Length > 1 && part[0] == '@')
        {
            return ("@", part[1..]);
        }

        var end = part.IndexOfAny(['"', '/']);
        var colon = part.IndexOf(':', 0, end < 0 ? part.Length : end);
        if (colon <= 0 || colon == part.Length - 1)
        {
            return (null, part);
        }

        var prefix = part[..colon].ToLowerInvariant();
        return Prefixes.Contains(prefix) ? (prefix, part[(colon + 1)..]) : (null, part);
    }

    private static IEnumerable<string> Split(string text)
    {
        var part = new StringBuilder();
        var quoted = false;
        foreach (var c in text)
        {
            if (c == '"')
            {
                quoted = !quoted;
            }
            else if (!quoted && char.IsWhiteSpace(c))
            {
                if (part.Length > 0)
                {
                    yield return part.ToString();
                    part.Clear();
                }
                continue;
            }
            part.Append(c);
        }
        if (part.Length > 0)
        {
            yield return part.ToString();
        }
    }

    private static bool IsSubsequence(string name, ReadOnlySpan<char> letters)
    {
        var next = 0;
        foreach (var c in name)
        {
            var lower = char.ToLowerInvariant(c);
            var wanted = char.ToLowerInvariant(letters[next]);
            if (lower != wanted)
            {
                continue;
            }

            next++;
            if (next == letters.Length)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>A matcher by <paramref name="pattern"/>, or one that says why it is no regular expression.</summary>
    private static SearchMatcher ParseRegex(string pattern)
    {
        try
        {
            var expression = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return new SearchMatcher.Regex { Expression = expression };
        }
        catch (ArgumentException e)
        {
            return new SearchMatcher.InvalidRegex { Message = e.Message };
        }
    }
}
