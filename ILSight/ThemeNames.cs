using System.Diagnostics;
using System.Globalization;

namespace ILSight;

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
        {
            return named;
        }

        if (name.ByTag.TryGetValue(FallbackTag, out var english))
        {
            return english;
        }

        foreach (var first in name.ByTag.Values)
        {
            return first;
        }

        return name.Fallback;
    }
}
