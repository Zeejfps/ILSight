using System.Globalization;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ZGF.KeyboardModule;

namespace ILSight;

/// <summary>The search over the decompiler and display options: the field at the top of each panel, which
/// one, and what it matches. An option matches when every word of the search is in its name, its
/// description or its section's heading, whatever the case and accents.</summary>
internal static class OptionSearch
{
    private const string FieldId = "option-search";
    private const CompareOptions Loose = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    /// <summary>The words of <paramref name="query"/>; none when it is blank, which matches everything.</summary>
    public static string[] SplitWords(string query)
    {
        return query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>Whether every one of <paramref name="words"/> is in <paramref name="first"/>,
    /// <paramref name="second"/> or <paramref name="third"/>, compared as <paramref name="culture"/>
    /// compares, ignoring case and accents.</summary>
    public static bool IsMatch(CultureInfo culture, string[] words, string first, string? second = null, string? third = null)
    {
        var compare = culture.CompareInfo;
        foreach (var word in words)
        {
            if (Contains(compare, first, word))
                continue;
            if (second != null && Contains(compare, second, word))
                continue;
            if (third != null && Contains(compare, third, word))
                continue;
            return false;
        }
        return true;
    }

    private static bool Contains(CompareInfo compare, string text, string word)
    {
        return compare.IndexOf(text, word, Loose) >= 0;
    }

    /// <summary>The search field, filling the row it is in. Escape empties it while it has focus.</summary>
    public static void Field(Ui<UiState> ui, ref string query)
    {
        var t = ui.State.Strings;
        if (query.Length > 0 && ui.HasFocus(ui.GetId(FieldId)) && ui.Input.TakeKey(KeyboardKey.Escape))
            query = "";
        ui.TextField(FieldId, ref query, t.OptionsSearchPlaceholder, Sizing.Grow(), ui.State.Icons.OptionsSearch);
    }

    /// <summary>Says that no option matches, with a button that empties the search.</summary>
    public static void NoMatch(Ui<UiState> ui, ref string query)
    {
        var t = ui.State.Strings;
        var theme = ui.Theme;
        using (ui.Column(new Box { Width = Sizing.Grow(), Gap = theme.Gap, Padding = theme.PanelPadding, AlignX = Align.Center }))
        {
            ui.Text(t.OptionsSearchNoMatch, color: theme.TextMuted, wrap: true);
            if (ui.Button(t.OptionsSearchClear, id: "clear-search", variant: ButtonVariant.Ghost))
                query = "";
        }
    }
}
