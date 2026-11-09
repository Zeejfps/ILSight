using ZGF.Gui.Immediate;
using System.Globalization;
using ILSight.Core;
using ILSight.Localization;
using ZGF.KeyboardModule;
using static ILSight.ScreenLayout;

namespace ILSight.App.Tests;

public sealed class OptionSearchTests
{
    private static readonly Strings En = Strings.For(Locale.En);

    /// <summary>Whether a line of the last frame reads <paramref name="text"/>: the texts drawn at one height
    /// joined from left to right, as a marked label is drawn in pieces.</summary>
    private static bool DrewLine(AppDriver app, string text) =>
        app.Frames.TextRects()
            .GroupBy(t => MathF.Round(t.Rect.Y))
            .Any(line => string.Concat(line.OrderBy(t => t.Rect.X).Select(t => t.Text)).Contains(text, StringComparison.Ordinal));

    /// <summary>The rects of the last frame filled with the code's find-match color, as marks are.</summary>
    private static List<UiRect> Marks(AppDriver app) =>
        app.Frames.Canvas.Rects.Where(r => r.Inputs.Style.BackgroundColor == BuiltInThemes.Dark.Code.FindMatch)
            .Select(r => app.Frames.ToUi(r.Inputs.Position)).ToList();

    private static List<(int Start, int End)> Hits(string query, string text) =>
        OptionSearch.FindHits(CultureInfo.InvariantCulture, query, text);

    [Fact]
    public void The_hits_of_a_search_are_every_place_each_word_is_found_ignoring_case_and_accents()
    {
        Assert.Equal([(0, 4), (10, 14)], Hits("line", "Line, the line"));
        Assert.Equal([(0, 4), (10, 15)], Hits("CODE opcoe", "Codes and opções"));
        Assert.Empty(Hits("tree", "Line numbers"));
        Assert.Empty(Hits("   ", "Line numbers"));
    }

    [Fact]
    public void Hits_that_overlap_or_touch_are_joined()
    {
        Assert.Equal([(0, 4), (5, 9)], Hits("line ine numb", "Line numbers"));
        Assert.Equal([(0, 4)], Hits("li ne", "Line numbers"));
    }

    [Fact]
    public void Marked_runs_cover_the_text_with_only_the_hits_marked()
    {
        var style = new ZGF.Gui.TextStyle();

        var runs = OptionSearch.CreateMarkedRuns("Show line numbers", [(5, 9)], style);

        Assert.Equal(["Show ", "line", " numbers"], runs.Select(r => r.Text));
        Assert.Equal([false, true, false], runs.Select(r => r.IsMarked));
    }

    [Fact]
    public void A_display_option_marks_the_words_the_search_found_in_its_name()
    {
        var app = AppDriver.Opened(width: 1600f);
        OpenDisplayOptions(app.Ui);
        app.Run();
        Assert.Empty(Marks(app));

        app.Ui.DisplayOptionsQuery = "numbers";
        app.Run();

        var mark = Assert.Single(Marks(app));
        var line = app.TextRect("Line ");
        Assert.True(mark.X >= line.X + line.Width - 1f && MathF.Abs(mark.Y - line.Y) < line.Height, $"mark at {mark}, 'Line ' at {line}");
        Assert.True(app.Frames.Drew("numbers"));
    }

    [Fact]
    public void A_decompiler_options_group_marks_the_words_the_search_found_in_its_title()
    {
        var app = AppDriver.Opened(width: 1600f);
        OpenDecompilerOptions(app.Ui);
        app.Ui.DecompilerOptionsQuery = "F# code";
        app.Run();

        var title = app.TextRect("F#");
        Assert.Contains(Marks(app), m => MathF.Abs(m.Y - title.Y) < title.Height && m.X <= title.X && m.X + m.Width >= title.X + title.Width - 1f);
    }

    [Fact]
    public void A_decompiler_option_marks_the_words_the_search_found_in_its_label()
    {
        var app = AppDriver.Opened(width: 1600f);
        OpenDecompilerOptions(app.Ui);
        app.Ui.DecompilerOptionsQuery = "switch";
        app.Run();

        Assert.NotEmpty(Marks(app));
        Assert.True(DrewLine(app, "switch expressions"));
    }

    private static bool IsMatch(string query, string first, string? second = null, string? third = null) =>
        OptionSearch.IsMatch(CultureInfo.InvariantCulture, OptionSearch.SplitWords(query), first, second, third);

    [Fact]
    public void Every_word_has_to_be_somewhere_in_the_name_the_description_or_the_heading()
    {
        Assert.True(IsMatch("dead code", "Remove dead code"));
        Assert.True(IsMatch("code tree", "Line numbers", null, "Code tree"));
        Assert.True(IsMatch("scale bigger", "UI scale", "Makes everything bigger"));
        Assert.False(IsMatch("dead stores", "Remove dead code"));
    }

    [Fact]
    public void A_search_ignores_case_and_accents_and_a_blank_one_matches_everything()
    {
        Assert.True(IsMatch("LINE", "Line numbers"));
        Assert.True(IsMatch("opcoes", "Opções de exibição"));
        Assert.True(IsMatch("   ", "Anything"));
    }

    [Fact]
    public void Decompiler_options_match_by_label_by_the_decompilers_name_and_by_their_groups_title()
    {
        var byLabel = DecompilerOptionsPanelView.FindMatchingSections(En, ["switch", "expressions"]);
        var byName = DecompilerOptionsPanelView.FindMatchingSections(En, ["UseDebugSymbols"]);
        var byGroup = DecompilerOptionsPanelView.FindMatchingSections(En, ["8.0"]);

        var section = Assert.Single(byLabel);
        Assert.Equal("SwitchExpressions", Assert.Single(section.Toggles).Name);
        Assert.Equal("UseDebugSymbols", Assert.Single(Assert.Single(byName).Toggles).Name);
        var csharp8 = DecompilerToggle.All.Where(t => t.IntroducedIn == CSharpVersion.CSharp8).ToArray();
        Assert.Equal(csharp8, Assert.Single(byGroup).Toggles);
        Assert.Empty(DecompilerOptionsPanelView.FindMatchingSections(En, ["nothing-is-called-this"]));
    }

    [Fact]
    public void Searching_the_decompiler_options_opens_a_folded_group_and_clearing_folds_it_again()
    {
        var app = AppDriver.Opened(width: 1600f);
        OptionFolds.Fold(app.Ui.FoldedOptionGroups, Enum.GetNames<CSharpVersion>().Append("Other").Append("FSharp"));
        OpenDecompilerOptions(app.Ui);
        app.Run();
        Assert.False(app.Frames.Drew("switch expressions"));

        app.Ui.DecompilerOptionsQuery = "switch expressions";
        app.Run();
        Assert.True(app.Frames.Drew("switch expressions"));
        Assert.True(app.Frames.Drew("C# 8.0 / VS 2019"));
        Assert.False(app.Frames.Drew("Extension members"));

        app.Ui.DecompilerOptionsQuery = "";
        app.Run();
        Assert.False(app.Frames.Drew("switch expressions"));
        Assert.Contains(nameof(CSharpVersion.CSharp8), app.Ui.FoldedOptionGroups.Folded);
    }

    [Fact]
    public void While_searching_a_groups_box_sets_only_the_options_shown()
    {
        var app = AppDriver.Opened(width: 1600f);
        OpenDecompilerOptions(app.Ui);
        app.Ui.DecompilerOptionsQuery = "switch expressions";
        app.Run();

        app.Frames.ClickOn(Selector.WithText("C# 8.0 / VS 2019"));
        app.Settle(1);

        var changed = DecompilerToggle.All.Where(t => DecompilerToggles.IsChanged(app.AppState.DecompilerOptions, t));
        Assert.Equal("SwitchExpressions", Assert.Single(changed).Name);
    }

    [Fact]
    public void Searching_the_display_options_shows_only_the_rows_it_matches_and_hides_the_reset()
    {
        var app = AppDriver.Opened(width: 1600f);
        OpenDisplayOptions(app.Ui);
        app.Ui.DisplayOptionsQuery = "line";
        app.Run();

        Assert.True(DrewLine(app, "Line numbers"));
        Assert.True(DrewLine(app, "Highlight current line"));
        Assert.False(app.Frames.Drew("UI scale"));
        Assert.False(app.Frames.Drew("TREE"));
        Assert.False(app.Frames.Drew("Reset to defaults"));
    }

    [Fact]
    public void A_search_that_matches_nothing_says_so_and_its_button_clears_it()
    {
        var app = AppDriver.Opened(width: 1600f);
        OpenDisplayOptions(app.Ui);
        app.Ui.DisplayOptionsQuery = "nothing-is-called-this";
        app.Run();
        Assert.True(app.Frames.Drew("No options match the search."));

        app.Frames.ClickOn(Selector.WithText("Clear search")).Frame(2);

        Assert.Equal("", app.Ui.DisplayOptionsQuery);
        Assert.True(app.Frames.Drew("UI scale"));
    }

    [Fact]
    public void Escape_in_the_search_field_empties_it()
    {
        var app = AppDriver.Opened(width: 1600f);
        OpenDisplayOptions(app.Ui);
        app.Run();
        app.Frames.ClickOn(Selector.WithText("Search options")).Frame();
        app.Frames.Type("line").Frame();
        Assert.Equal("line", app.Ui.DisplayOptionsQuery);

        app.Frames.Key(KeyboardKey.Escape).Frame(2);

        Assert.Equal("", app.Ui.DisplayOptionsQuery);
        Assert.True(app.Frames.Drew("UI scale"));
    }

    [Fact]
    public void The_search_field_shows_a_cross_only_while_it_holds_text()
    {
        var app = AppDriver.Opened(width: 1600f);
        OpenDisplayOptions(app.Ui);
        app.Run(2);
        var empty = app.Frames.Canvas.Lines.Count;

        // Spaces alone hide no option, so the only lines it adds are the cross's two.
        app.Ui.DisplayOptionsQuery = "  ";
        app.Run(2);

        Assert.Equal(empty + 2, app.Frames.Canvas.Lines.Count);
    }
}
