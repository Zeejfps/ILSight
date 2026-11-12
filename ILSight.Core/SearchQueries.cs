using System.Text;
using System.Text.RegularExpressions;

namespace ILSight.Core;

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
