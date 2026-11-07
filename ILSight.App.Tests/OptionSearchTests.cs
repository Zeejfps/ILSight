using System.Globalization;
using ILSight.Core;
using ILSight.Localization;
using ZGF.KeyboardModule;
using static ILSight.ScreenLayout;

namespace ILSight.App.Tests;

public sealed class OptionSearchTests
{
    private static readonly Strings En = Strings.For(Locale.En);

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

        app.Click("C# 8.0 / VS 2019");
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

        Assert.True(app.Frames.Drew("Line numbers"));
        Assert.True(app.Frames.Drew("Highlight current line"));
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

        app.Click("Clear search").Frame(2);

        Assert.Equal("", app.Ui.DisplayOptionsQuery);
        Assert.True(app.Frames.Drew("UI scale"));
    }

    [Fact]
    public void Escape_in_the_search_field_empties_it()
    {
        var app = AppDriver.Opened(width: 1600f);
        OpenDisplayOptions(app.Ui);
        app.Run();
        app.Click("Search options").Frame();
        app.Frames.Type("line").Frame();
        Assert.Equal("line", app.Ui.DisplayOptionsQuery);

        app.Frames.Key(KeyboardKey.Escape).Frame(2);

        Assert.Equal("", app.Ui.DisplayOptionsQuery);
        Assert.True(app.Frames.Drew("UI scale"));
    }
}
