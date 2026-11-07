using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.Localization;
using static ILSight.Preferences;

namespace ILSight;

/// <summary>The decompiler's toggles, grouped by the C# version that brought each feature, newest
/// first, then the rest. Every change shows in the open documents at once. Features newer than the
/// picked version are greyed out but keep their state for when a newer version is picked. A search
/// narrows the groups to the toggles it matches, every group open while it does, and a group's box
/// then sets only the toggles shown.</summary>
internal static class DecompilerOptionsPanelView
{
    internal sealed record Section(string Key, DecompilerToggleGroup Group, CSharpVersion? Version, DecompilerToggle[] Toggles);

    private static readonly Section[] Sections = BuildSections();

    private static readonly CSharpVersion Newest = Sections.Max(static s => s.Version ?? 0);

    public static void Render(Ui<UiState> ui, DecompilerOptions options)
    {
        var t = ui.State.Strings;
        var theme = ui.Theme;
        var folded = ui.State.FoldedOptionGroups;
        using var pane = ui.Column(new Box { Height = Sizing.Grow(), Gap = 0f });
        using (ui.PaneToolbar())
        {
            OptionSearch.Field(ui, ref ui.State.DecompilerOptionsQuery);
            var searching = ui.State.DecompilerOptionsQuery.Trim().Length > 0;
            if (ui.IconButton("collapse", ui.State.Icons.OptionsCollapseAll, t.CodeCollapseAll, !searching && !AllFolded(folded)))
                OptionFolds.Fold(folded, Sections.Select(static s => s.Key));
            if (ui.IconButton("expand", ui.State.Icons.OptionsExpandAll, t.CodeExpandAll, !searching && folded.Count > 0))
                OptionFolds.UnfoldAll(folded);
            ui.Separator(theme.IconSize);
            if (ui.IconButton("reset", ui.State.Icons.OptionsResetDecompiler, t.DecompilerOptionsReset, options.HasChanges))
                Commands.Later(ui.State, static a => ResetDecompilerToggles(a));
        }
        ui.Separator();
        var tooNew = options.LanguageVersion < Newest ? ui.Memo("too-new", (t, options.LanguageVersion), static a => TooNew(a.t, a.LanguageVersion)) : null;
        if (tooNew != null)
        {
            using (ui.Column(new Box { Padding = new Spacing(theme.ShellPaddingX, theme.ToolbarPaddingY) }))
                ui.Text(tooNew, color: theme.TextMuted, wrap: true);
            ui.Separator();
        }

        var words = OptionSearch.SplitWords(ui.State.DecompilerOptionsQuery);
        var sections = words.Length == 0 ? Sections : FindMatchingSections(t, words);
        if (sections.Length == 0)
        {
            OptionSearch.NoMatch(ui, ref ui.State.DecompilerOptionsQuery);
            return;
        }
        // A search shows every group open, and leaves the folds as they were for when it is cleared.
        var shownFolds = words.Length == 0 ? folded : null;
        var rowHeight = theme.RowHeight;
        string? flipped = null;
        using (var list = ui.VirtualScroll("decompiler-options", RowCount(sections, shownFolds), rowHeight))
        {
            for (var i = list.First; i < list.End; i++)
            {
                var (section, toggle) = RowAt(sections, shownFolds, i);
                using (ui.Id(i))
                {
                    if (toggle is null)
                    {
                        if (Header(ui, shownFolds, section, options, rowHeight, tooNew))
                            flipped = section.Key;
                    }
                    else
                    {
                        Toggle(ui, toggle, options, rowHeight);
                    }
                }
            }
        }
        if (flipped != null)
            OptionFolds.Flip(folded, flipped);
    }

    /// <summary>The sections with only the toggles <paramref name="words"/> match, by the toggle's label,
    /// its name in the decompiler or its section's title; a section none match is left out.</summary>
    internal static Section[] FindMatchingSections(Strings t, string[] words)
    {
        var matching = new List<Section>();
        foreach (var section in Sections)
        {
            var title = Title(t, section);
            var toggles = section.Toggles
                .Where(toggle => OptionSearch.IsMatch(t.Culture, words, Label(t, toggle), toggle.Name, title))
                .ToArray();
            if (toggles.Length > 0)
                matching.Add(section with { Toggles = toggles });
        }
        return matching.ToArray();
    }

    private static string Label(Strings t, DecompilerToggle toggle) => DecompilerOptionLabels.Label(t, toggle.Name) ?? toggle.Name;

    /// <summary>Draws a section's header: a chevron and a checkbox that sets every toggle shown in it,
    /// showing a square when only some are on. Returns true when the row besides the checkbox was
    /// clicked to fold or unfold, which it can't while <paramref name="folded"/> is null, as during a
    /// search.</summary>
    private static bool Header(Ui<UiState> ui, FoldedGroups? folded, Section section, DecompilerOptions options, float rowHeight,
        string? tooNew)
    {
        var t = ui.State.Strings;
        var theme = ui.Theme;
        var isFolded = IsFolded(folded, section);
        var allowed = IsAllowed(section, options);
        var item = ui.Interact(section.Key);
        using (ui.Row(item.Id, new Box
        {
            Width = Sizing.Grow(),
            Height = rowHeight,
            Gap = theme.GapTight,
            Padding = new Spacing(theme.ShellPaddingX, 0f),
            AlignY = Align.Center,
            Background = item.Hovered ? theme.RowHover : 0u,
        }))
        {
            var icons = ui.State.Icons;
            var mark = isFolded ? icons.OptionsGroupFolded : icons.OptionsGroupUnfolded;
            ui.Icon(new UiIcon(mark, theme.TextMuted));
            var all = State(section, options);
            if (ui.Checkbox(Title(t, section), ref all, id: "all", enabled: allowed, width: Sizing.Grow(), bold: Changed(section, options)))
                Commands.Later(ui.State, (toggles: section.Toggles, on: all == true), static (a, s) => SetDecompilerToggles(a, s.toggles, s.on));
        }
        if (!allowed && tooNew != null)
            ui.Tooltip(item.Id, tooNew);
        return folded != null && item.Clicked;
    }

    /// <summary>Whether <paramref name="section"/> is folded; none is while <paramref name="folded"/> is null.</summary>
    private static bool IsFolded(FoldedGroups? folded, Section section)
    {
        if (folded == null)
            return false;
        return OptionFolds.IsFolded(folded, section.Key);
    }

    private static bool IsAllowed(Section section, DecompilerOptions options)
    {
        var version = section.Version;
        if (version == null)
            return true;
        return version.Value <= options.LanguageVersion;
    }

    /// <summary>True when every toggle in <paramref name="section"/> is on, false when none is, null otherwise.</summary>
    private static bool? State(Section section, DecompilerOptions options)
    {
        var on = 0;
        foreach (var toggle in section.Toggles)
        {
            if (DecompilerToggles.IsOn(options, toggle))
                on++;
        }
        if (on == section.Toggles.Length)
            return true;
        if (on == 0)
            return false;
        return null;
    }

    /// <summary>Whether any toggle in <paramref name="section"/> is set away from its default.</summary>
    private static bool Changed(Section section, DecompilerOptions options)
    {
        foreach (var toggle in section.Toggles)
        {
            if (DecompilerToggles.IsChanged(options, toggle)) return true;
        }
        return false;
    }

    private static void Toggle(Ui<UiState> ui, DecompilerToggle toggle, DecompilerOptions options, float rowHeight)
    {
        var t = ui.State.Strings;
        var theme = ui.Theme;
        using (ui.Row(new Box
        {
            Width = Sizing.Grow(),
            Height = rowHeight,
            Padding = new Spacing(theme.ShellPaddingX + theme.IconSize + theme.GapTight + theme.TreeIndent, 0f),
            AlignY = Align.Center,
        }))
        {
            var on = DecompilerToggles.IsOn(options, toggle);
            if (ui.Checkbox(Label(t, toggle), ref on, id: toggle.Name, enabled: DecompilerToggles.Allows(options, toggle),
                    width: Sizing.Grow(), bold: DecompilerToggles.IsChanged(options, toggle)))
                Commands.Later(ui.State, (toggle, on), static (a, s) => SetDecompilerToggle(a, s.toggle, s.on));
        }
    }

    private static string TooNew(Strings t, CSharpVersion version) => t.DecompilerOptionsTooNew(CSharpVersions.Label(version));

    private static string Title(Strings t, Section section) => section.Group switch
    {
        DecompilerToggleGroup.Language => CSharpVersions.Label(section.Version!.Value),
        DecompilerToggleGroup.FSharp => t.DecompilerOptionsFsharp,
        _ => t.DecompilerOptionsOther,
    };

    private static bool AllFolded(FoldedGroups folded)
    {
        foreach (var section in Sections)
        {
            if (!OptionFolds.IsFolded(folded, section.Key)) return false;
        }
        return true;
    }

    private static int RowCount(Section[] sections, FoldedGroups? folded)
    {
        var count = 0;
        foreach (var section in sections)
            count += 1 + (IsFolded(folded, section) ? 0 : section.Toggles.Length);
        return count;
    }

    /// <summary>The section a row is in, and the toggle it shows, or null for the section's header.</summary>
    private static (Section Section, DecompilerToggle? Toggle) RowAt(Section[] sections, FoldedGroups? folded, int row)
    {
        foreach (var section in sections)
        {
            if (row == 0) return (section, null);
            var shown = IsFolded(folded, section) ? 0 : section.Toggles.Length;
            if (row <= shown) return (section, section.Toggles[row - 1]);
            row -= 1 + shown;
        }
        throw new ArgumentOutOfRangeException(nameof(row));
    }

    private static Section[] BuildSections()
    {
        var all = DecompilerToggle.All;
        var language = all.Where(static t => t.Group == DecompilerToggleGroup.Language)
            .GroupBy(static t => t.IntroducedIn!.Value)
            .OrderByDescending(static g => g.Key)
            .Select(static g => new Section(g.Key.ToString(), DecompilerToggleGroup.Language, g.Key, g.ToArray()));
        var rest = new[] { DecompilerToggleGroup.Other, DecompilerToggleGroup.FSharp }
            .Select(group => new Section(group.ToString(), group, null, all.Where(t => t.Group == group).ToArray()))
            .Where(static s => s.Toggles.Length > 0);
        return language.Concat(rest).ToArray();
    }
}
