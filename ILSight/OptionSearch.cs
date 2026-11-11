using System.Globalization;
using ZGF.Gui;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ZGF.KeyboardModule;

namespace ILSight;

/// <summary>A label as a search last marked it: its runs for <see cref="Text"/> in <see cref="Style"/> while the
/// search was <see cref="Query"/>, null when no word of it was in the text. Kept per label between frames, so the
/// runs are the same list while nothing changes, as rich text wants.</summary>
internal sealed class MarkedLabel
{
    public bool HasValue;
    public string Text = "";
    public string Query = "";
    public TextStyle? Style;
    public IReadOnlyList<RichTextRun>? Runs;
}

/// <summary>The search over the decompiler and display options: the field at the top of each panel, which
/// one, and what it matches. An option matches when every word of the search is in its name, its
/// description or its section's heading, whatever the case and accents; where they are in its name and
/// description is marked.</summary>
internal static class OptionSearch
{
    private const string FieldId = "option-search";
    private const int MarkSalt = 0x4D41524B;
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
            {
                continue;
            }

            if (second is not null && Contains(compare, second, word))
            {
                continue;
            }

            if (third is not null && Contains(compare, third, word))
            {
                continue;
            }

            return false;
        }
        return true;
    }

    private static bool Contains(CompareInfo compare, string text, string word)
    {
        return compare.IndexOf(text, word, Loose) >= 0;
    }

    /// <summary>Where the words of <paramref name="query"/> are in <paramref name="text"/>, compared as
    /// <see cref="IsMatch"/> compares, in order, overlapping ones joined: the start and end of each.</summary>
    public static List<(int Start, int End)> FindHits(CultureInfo culture, string query, string text)
    {
        var compare = culture.CompareInfo;
        var hits = new List<(int Start, int End)>();
        foreach (var word in SplitWords(query))
        {
            var from = 0;
            while (from < text.Length)
            {
                var at = compare.IndexOf(text.AsSpan(from), word, Loose, out var length);
                if (at < 0 || length == 0)
                {
                    break;
                }

                hits.Add((from + at, from + at + length));
                from += at + length;
            }
        }
        hits.Sort();
        var joined = new List<(int Start, int End)>(hits.Count);
        foreach (var hit in hits)
        {
            if (joined.Count > 0 && hit.Start <= joined[^1].End)
            {
                joined[^1] = (joined[^1].Start, Math.Max(joined[^1].End, hit.End));
            }
            else
            {
                joined.Add(hit);
            }
        }
        return joined;
    }

    /// <summary><paramref name="text"/> as runs in <paramref name="style"/>, those of <paramref name="hits"/> marked.</summary>
    public static List<RichTextRun> CreateMarkedRuns(string text, List<(int Start, int End)> hits, TextStyle style)
    {
        var runs = new List<RichTextRun>(hits.Count * 2 + 1);
        var at = 0;
        foreach (var (start, end) in hits)
        {
            if (start > at)
            {
                runs.Add(new RichTextRun(text[at..start], style));
            }

            runs.Add(new RichTextRun(text[start..end], style, IsMarked: true));
            at = end;
        }
        if (at < text.Length)
        {
            runs.Add(new RichTextRun(text[at..], style));
        }

        return runs;
    }

    /// <summary><paramref name="text"/> as runs in <paramref name="style"/> with the hits of <paramref name="query"/>
    /// marked, kept in <paramref name="label"/> while they stay the same; null when it has none, as without a search.</summary>
    public static IReadOnlyList<RichTextRun>? EnsureMarkedRuns(MarkedLabel label, CultureInfo culture, string query, string text, TextStyle style)
    {
        if (label.HasValue && ReferenceEquals(label.Style, style) && string.Equals(label.Text, text, StringComparison.Ordinal)
            && string.Equals(label.Query, query, StringComparison.Ordinal))
        {
            return label.Runs;
        }

        var hits = FindHits(culture, query, text);
        label.HasValue = true;
        label.Text = text;
        label.Query = query;
        label.Style = style;
        label.Runs = hits.Count == 0 ? null : CreateMarkedRuns(text, hits, style);
        return label.Runs;
    }

    /// <summary>The runs of <paramref name="text"/> with the hits of <paramref name="query"/> marked, as
    /// <see cref="EnsureMarkedRuns"/> keeps them for the label that is <paramref name="part"/> of the element
    /// named <paramref name="text"/> in the scope it is drawn in.</summary>
    public static IReadOnlyList<RichTextRun>? EnsureMarkedRuns(Ui<UiState> ui, string query, string text, TextStyle style, int part = 0)
    {
        if (query.Length == 0)
        {
            return null;
        }

        var label = ui.GetState<MarkedLabel>(HashCode.Combine(ui.GetId(text), part, MarkSalt));
        return EnsureMarkedRuns(label, ui.State.Strings.Culture, query, text, style);
    }

    /// <summary>The background a hit of the search is marked on: that of a find match in the code.</summary>
    public static uint GetMarkBackground(Ui<UiState> ui)
    {
        return ui.State.CodeViewPalette.FindMatch;
    }

    /// <summary>The search field, filling the row it is in. Escape empties it while it has focus, and so
    /// does the cross at its end while it holds text.</summary>
    public static void Field(Ui<UiState> ui, ref string query)
    {
        var t = ui.State.Strings;
        if (query.Length > 0)
        {
            var fieldId = ui.GetId(FieldId);
            if (ui.HasFocus(fieldId) && ui.Input.TakeKey(KeyboardKey.Escape))
            {
                query = "";
            }
        }

        ui.TextField(FieldId, ref query, t.OptionsSearchPlaceholder, Sizing.Grow(), ui.State.Icons.OptionsSearch, clearable: true);
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
            {
                query = "";
            }
        }
    }
}
