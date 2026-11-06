using System.Diagnostics;
using System.Globalization;

namespace ILSight;

/// <summary>What a theme is called: one name, or a name per language with one to fall back on.</summary>
public abstract record ThemeName
{
    private ThemeName()
    {
    }

    public sealed record Plain(string Name) : ThemeName;

    public sealed record PerLanguage : ThemeName
    {
        /// <summary>Takes <paramref name="byTag"/> as case-insensitive, the first of each tag standing.</summary>
        public PerLanguage(IReadOnlyDictionary<string, string> byTag, string fallback)
        {
            var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (tag, name) in byTag)
                tags.TryAdd(tag, name);
            ByTag = tags;
            Fallback = fallback;
        }

        public IReadOnlyDictionary<string, string> ByTag { get; }

        public string Fallback { get; }
    }
}

public static class ThemeNames
{
    private const string FallbackTag = "en";

    /// <summary>What <paramref name="name"/> reads as in <paramref name="culture"/>.</summary>
    public static string GetName(ThemeName name, CultureInfo culture)
    {
        return name switch
        {
            ThemeName.Plain plain => plain.Name,
            ThemeName.PerLanguage perLanguage => GetName(perLanguage, culture),
            _ => throw new UnreachableException(),
        };
    }

    private static string GetName(ThemeName.PerLanguage name, CultureInfo culture)
    {
        if (name.ByTag.TryGetValue(culture.Name, out var named))
            return named;
        if (name.ByTag.TryGetValue(FallbackTag, out var english))
            return english;
        foreach (var first in name.ByTag.Values)
            return first;
        return name.Fallback;
    }
}
