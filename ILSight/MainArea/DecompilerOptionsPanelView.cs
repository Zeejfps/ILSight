using System.Diagnostics;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.CommandArea;
using static ILSight.Preferences;

namespace ILSight.MainArea;

/// <summary>The decompiler's toggles, grouped by the C# version that brought each feature, newest
/// first, then the rest. Every change shows in the open documents at once. Features newer than the
/// picked version are greyed out but keep their state for when a newer version is picked. A search
/// narrows the groups to the toggles it matches, every group open while it does, the words it found
/// marked in their labels and the groups' titles, and a group's box then sets only the toggles shown.</summary>
internal static class DecompilerOptionsPanelView
{
    private static readonly DecompilerOptionSection[] Sections = BuildSections();

    private static readonly CSharpVersion Newest = Sections.Max(static s => FindVersion(s.Group) ?? 0);

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
            {
                OptionFolds.Fold(folded, Sections.Select(static s => s.Key));
            }

            if (ui.IconButton("expand", ui.State.Icons.OptionsExpandAll, t.CodeExpandAll, !searching && folded.Count > 0))
            {
                OptionFolds.UnfoldAll(folded);
            }

            ui.Separator(theme.IconSize);
            if (ui.IconButton("reset", ui.State.Icons.OptionsResetDecompiler, t.DecompilerOptionsReset, options.HasChanges))
            {
                Commands.Later(ui.State, static a => ResetDecompilerToggles(a));
            }
        }
        ui.Separator();
        var tooNew = options.LanguageVersion < Newest ? ui.Memo("too-new", (t, options.LanguageVersion), static a => TooNew(a.t, a.LanguageVersion)) : null;
        if (tooNew is not null)
        {
            using (ui.Column(new Box { Padding = new Spacing(theme.ShellPaddingX, theme.ToolbarPaddingY) }))
            {
                ui.Text(tooNew, color: theme.TextMuted, wrap: true);
            }

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
                        {
                            flipped = section.Key;
                        }
                    }
                    else
                    {
                        Toggle(ui, toggle, options, rowHeight);
                    }
                }
            }
        }
        if (flipped is not null)
        {
            OptionFolds.Flip(folded, flipped);
        }
    }

    /// <summary>The sections with only the toggles <paramref name="words"/> match, by the toggle's label,
    /// its name in the decompiler or its section's title; a section none match is left out.</summary>
    internal static DecompilerOptionSection[] FindMatchingSections(Strings t, string[] words)
    {
        var matching = new List<DecompilerOptionSection>();
        foreach (var section in Sections)
        {
            var title = Title(t, section);
            var toggles = section.Toggles
                .Where(toggle => OptionSearch.IsMatch(t.Culture, words, Label(t, toggle), toggle.Name, title))
                .ToArray();
            if (toggles.Length > 0)
            {
                matching.Add(section with { Toggles = toggles });
            }
        }
        return matching.ToArray();
    }

    private static string Label(Strings t, DecompilerToggle toggle)
    {
        return DecompilerOptionLabels.Label(t, toggle.Name) ?? toggle.Name;
    }

    /// <summary>Draws a section's header: a chevron and a checkbox that sets every toggle shown in it,
    /// showing a square when only some are on. Returns true when the row besides the checkbox was
    /// clicked to fold or unfold, which it can't while <paramref name="folded"/> is null, as during a
    /// search.</summary>
    private static bool Header(Ui<UiState> ui, FoldedGroups? folded, DecompilerOptionSection section, DecompilerOptions options, float rowHeight,
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
            var title = Title(t, section);
            var bold = HasChangedToggle(section, options);
            var runs = OptionSearch.EnsureMarkedRuns(ui, ui.State.DecompilerOptionsQuery, title, ui.StyleText(allowed ? null : theme.TextDisabled, bold: bold));
            var flipped = runs is not null
                ? ui.Checkbox(runs, OptionSearch.GetMarkBackground(ui), ref all, "all", allowed, Sizing.Grow())
                : ui.Checkbox(title, ref all, id: "all", enabled: allowed, width: Sizing.Grow(), bold: bold);
            if (flipped)
            {
                Commands.Later(ui.State, (toggles: section.Toggles, on: all == true), static (a, s) => SetDecompilerToggles(a, s.toggles, s.on));
            }
        }
        if (!allowed && tooNew is not null)
        {
            ui.Tooltip(item.Id, tooNew);
        }

        return folded is not null && item.Clicked;
    }

    /// <summary>Whether <paramref name="section"/> is folded; none is while <paramref name="folded"/> is null.</summary>
    private static bool IsFolded(FoldedGroups? folded, DecompilerOptionSection section)
    {
        if (folded is null)
        {
            return false;
        }

        return OptionFolds.IsFolded(folded, section.Key);
    }

    private static bool IsAllowed(DecompilerOptionSection section, DecompilerOptions options)
    {
        if (section.Group is not DecompilerToggleGroup.Language language)
        {
            return true;
        }

        return language.IntroducedIn <= options.LanguageVersion;
    }

    /// <summary>The C# version the features of <paramref name="group"/> came with, or null for a group no
    /// version holds back.</summary>
    private static CSharpVersion? FindVersion(DecompilerToggleGroup group)
    {
        if (group is not DecompilerToggleGroup.Language language)
        {
            return null;
        }

        return language.IntroducedIn;
    }

    /// <summary>What a section of <paramref name="group"/> is known by where its folding is kept.</summary>
    private static string GetKey(DecompilerToggleGroup group)
    {
        return group switch
        {
            DecompilerToggleGroup.Language language => language.IntroducedIn.ToString(),
            DecompilerToggleGroup.Other => "Other",
            DecompilerToggleGroup.FSharp => "FSharp",
            DecompilerToggleGroup.ProjectExport => "ProjectExport",
            _ => throw new UnreachableException(),
        };
    }

    /// <summary>True when every toggle in <paramref name="section"/> is on, false when none is, null otherwise.</summary>
    private static bool? State(DecompilerOptionSection section, DecompilerOptions options)
    {
        var on = 0;
        foreach (var toggle in section.Toggles)
        {
            if (DecompilerToggles.IsOn(options, toggle))
            {
                on++;
            }
        }
        if (on == section.Toggles.Length)
        {
            return true;
        }

        if (on == 0)
        {
            return false;
        }

        return null;
    }

    /// <summary>Whether any toggle in <paramref name="section"/> is set away from its default.</summary>
    private static bool HasChangedToggle(DecompilerOptionSection section, DecompilerOptions options)
    {
        foreach (var toggle in section.Toggles)
        {
            if (DecompilerToggles.IsChanged(options, toggle))
            {
                return true;
            }
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
            var label = Label(t, toggle);
            var enabled = DecompilerToggles.Allows(options, toggle);
            var bold = DecompilerToggles.IsChanged(options, toggle);
            var style = ui.StyleText(enabled ? null : theme.TextDisabled, bold: bold);
            var runs = OptionSearch.EnsureMarkedRuns(ui, ui.State.DecompilerOptionsQuery, label, style);
            var flipped = runs is not null
                ? ui.Checkbox(runs, OptionSearch.GetMarkBackground(ui), ref on, toggle.Name, enabled, Sizing.Grow())
                : ui.Checkbox(label, ref on, id: toggle.Name, enabled: enabled, width: Sizing.Grow(), bold: bold);
            if (flipped)
            {
                Commands.Later(ui.State, (toggle, on), static (a, s) => SetDecompilerToggle(a, s.toggle, s.on));
            }
        }
    }

    private static string TooNew(Strings t, CSharpVersion version)
    {
        return t.DecompilerOptionsTooNew(CSharpVersions.Label(version));
    }

    private static string Title(Strings t, DecompilerOptionSection section)
    {
        return section.Group switch
        {
            DecompilerToggleGroup.Language language => CSharpVersions.Label(language.IntroducedIn),
            DecompilerToggleGroup.FSharp => t.DecompilerOptionsFsharp,
            DecompilerToggleGroup.Other => t.DecompilerOptionsOther,
            DecompilerToggleGroup.ProjectExport => throw new UnreachableException("The export's toggles have no section here: the export dialog shows them."),
            _ => throw new UnreachableException(),
        };
    }

    private static bool AllFolded(FoldedGroups folded)
    {
        foreach (var section in Sections)
        {
            if (!OptionFolds.IsFolded(folded, section.Key))
            {
                return false;
            }
        }
        return true;
    }

    private static int RowCount(DecompilerOptionSection[] sections, FoldedGroups? folded)
    {
        var count = 0;
        foreach (var section in sections)
        {
            count += 1 + (IsFolded(folded, section) ? 0 : section.Toggles.Length);
        }

        return count;
    }

    /// <summary>The section a row is in, and the toggle it shows, or null for the section's header.</summary>
    private static (DecompilerOptionSection Section, DecompilerToggle? Toggle) RowAt(DecompilerOptionSection[] sections, FoldedGroups? folded, int row)
    {
        foreach (var section in sections)
        {
            if (row == 0)
            {
                return (section, null);
            }

            var shown = IsFolded(folded, section) ? 0 : section.Toggles.Length;
            if (row <= shown)
            {
                return (section, section.Toggles[row - 1]);
            }

            row -= 1 + shown;
        }
        throw new ArgumentOutOfRangeException(nameof(row));
    }

    private static DecompilerOptionSection[] BuildSections()
    {
        var all = DecompilerToggle.All;
        // A group compares by what it holds, so the language toggles fall into one section a version.
        var language = all.Where(static t => t.Group is DecompilerToggleGroup.Language)
            .GroupBy(static t => t.Group)
            .OrderByDescending(static g => FindVersion(g.Key))
            .Select(static g => new DecompilerOptionSection(GetKey(g.Key), g.Key, g.ToArray()));
        var rest = new DecompilerToggleGroup[] { new DecompilerToggleGroup.Other(), new DecompilerToggleGroup.FSharp() }
            .Select(group => new DecompilerOptionSection(GetKey(group), group, all.Where(t => t.Group == group).ToArray()))
            .Where(static s => s.Toggles.Length > 0);
        return language.Concat(rest).ToArray();
    }
}
