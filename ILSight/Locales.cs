using System.Globalization;

namespace ILSight;

public static class Locales
{
    public static IReadOnlyList<(Locale Locale, string Endonym)> All { get; } =
    [
        (Locale.En, "English"),
        (Locale.De, "Deutsch"),
        (Locale.Es, "Español"),
        (Locale.Fr, "Français"),
        (Locale.PtBr, "Português (Brasil)"),
        (Locale.Ru, "Русский"),
        (Locale.Ja, "日本語"),
        (Locale.Ko, "한국어"),
        (Locale.ZhHans, "简体中文"),
    ];

    /// <summary>Where <paramref name="locale"/> is in <see cref="All"/>; null when it isn't there.</summary>
    public static int? FindIndex(Locale locale)
    {
        for (var i = 0; i < All.Count; i++)
        {
            if (All[i].Locale == locale)
            {
                return i;
            }
        }
        return null;
    }

    /// <summary>The locale for <paramref name="culture"/>'s language, or English when there is none.</summary>
    public static Locale For(CultureInfo culture)
    {
        return culture.TwoLetterISOLanguageName switch
        {
            "de" => Locale.De,
            "es" => Locale.Es,
            "fr" => Locale.Fr,
            "ja" => Locale.Ja,
            "ko" => Locale.Ko,
            "pt" => Locale.PtBr,
            "ru" => Locale.Ru,
            "zh" => Locale.ZhHans,
            _ => Locale.En,
        };
    }
}
