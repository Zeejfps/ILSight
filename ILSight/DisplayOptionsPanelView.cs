using System.Globalization;
using System.Runtime.CompilerServices;
using ZGF.Desktop;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.CommandArea;
using ILSight.SettingArea;

namespace ILSight;

/// <summary>How the app is drawn, as settings cards: the interface's theme, icon theme, scale, language and title bar, then the
/// code's font size and switches for line numbers, the current line's highlight, matching brackets and the omnibar,
/// then how the tree writes metadata tokens, whether it lists empty metadata tables, whether it nests namespaces and whether what it picks shows in a preview tab. Each option is a row of its card, named on the left, with its control at the right. Every change
/// shows at once; an option changed from its default has a bold name. A search over them shows only the
/// rows it matches, and the cards that have any, with the words it found marked in their names and descriptions.</summary>
internal static class DisplayOptionsPanelView
{
    private const string PaneId = "display-options";
    private const string FontSizeFormat = "0.#";
    private const float FormMaxWidth = 560f;
    private const string RowSuffix = "-row";
    private const float FontPickerMaxWidth = 220f;
    private const float PickerMinWidth = 64f;
    private const float TitleMinWidth = 48f;
    private const int TitlePriority = 1;
    private const float WholeWidth = 1f;
    private const string PreviewText = "The quick brown fox jumps over the lazy dog";

    private static readonly ConditionalWeakTable<Strings, string[]> UiScaleLabels = new();
    private static readonly ConditionalWeakTable<Strings, string[]> TokenFormatLabels = new();
    private static readonly TokenFormat[] TokenFormats = Enum.GetValues<TokenFormat>();
    private static readonly string[] LocaleLabels = ListLocaleLabels();

    public static void Render(Ui<UiState> ui, AppState appState)
    {
        var theme = ui.Theme;
        using var panel = ui.Column(new Box { Width = Sizing.Grow(), Height = Sizing.Grow(), Gap = 0f });
        using (ui.PaneToolbar())
        {
            OptionSearch.Field(ui, ref ui.State.DisplayOptionsQuery);
        }

        ui.Separator();
        var words = OptionSearch.SplitWords(ui.State.DisplayOptionsQuery);
        using var pane = ui.Scroll(PaneId, new Box { Width = Sizing.Grow(), Height = Sizing.Grow(), Gap = 0f });
        using var form = ui.Column(new Box
        {
            Width = Sizing.Grow(max: FormMaxWidth),
            Gap = theme.GroupGap,
            Padding = theme.PanelPadding,
        });
        ListInstalledFontsOnce(ui);
        var shown = InterfaceSection(ui, appState, words);
        shown |= CodeSection(ui, words);
        shown |= TreeSection(ui, words);
        if (words.Length == 0)
        {
            ResetRow(ui);
        }
        else if (!shown)
        {
            OptionSearch.NoMatch(ui, ref ui.State.DisplayOptionsQuery);
        }
    }

    private static bool Shows(Strings t, string[] words, string heading, string title, string? description = null)
    {
        if (words.Length == 0)
        {
            return true;
        }

        return OptionSearch.IsMatch(t.Culture, words, title, description, heading);
    }

    private static void NextRow(Ui<UiState> ui, ref int rows)
    {
        if (rows > 0)
        {
            ui.Separator();
        }

        rows++;
    }

    private static void ListInstalledFontsOnce(Ui<UiState> ui)
    {
        if (!ui.State.CodeFonts.TryTakeListingRequest())
        {
            return;
        }

        ListInstalledFonts(ui);
    }

    // Its own method, so that the closure is made only the frame the listing is asked for.
    private static void ListInstalledFonts(Ui<UiState> ui)
    {
        var screen = ui.State;
        ui.ListSystemFonts(families => screen.CodeFonts.KeepFonts(families));
    }

    // Only the scale and the title bar are options a reset puts back, so only their names turn bold.
    private static bool InterfaceSection(Ui<UiState> ui, AppState appState, string[] words)
    {
        var t = ui.State.Strings;
        var heading = t.DisplayOptionsInterface;
        var theme = Shows(t, words, heading, t.DisplayOptionsTheme);
        var iconTheme = Shows(t, words, heading, t.DisplayOptionsIconTheme);
        var scale = Shows(t, words, heading, t.DisplayOptionsUiScale, t.DisplayOptionsUiScaleDescription);
        var language = Shows(t, words, heading, t.DisplayOptionsLanguage);
        var titleBar = ui.CanDrawTitleBar && Shows(t, words, heading, t.DisplayOptionsSystemTitleBar);
        if (!theme && !iconTheme && !scale && !language && !titleBar)
        {
            return false;
        }

        using var section = Section(ui, ui.Memo("interface", t, static s => Heading(s.DisplayOptionsInterface)));
        using var card = Card(ui);
        var rows = 0;
        if (theme)
        {
            NextRow(ui, ref rows);
            using (OptionRow(ui, t.DisplayOptionsTheme, null, false))
            {
                ThemePicker(ui, appState.Themes, appState.Theme, appState.SettingsPlace);
            }
        }
        if (iconTheme)
        {
            NextRow(ui, ref rows);
            using (OptionRow(ui, t.DisplayOptionsIconTheme, null, false))
            {
                IconThemePicker(ui, appState.IconThemes, appState.IconTheme, appState.SettingsPlace);
            }
        }
        if (scale)
        {
            NextRow(ui, ref rows);
            var changed = ui.State.Display.UiScale != DisplayOptions.Default.UiScale;
            using (OptionRow(ui, t.DisplayOptionsUiScale, t.DisplayOptionsUiScaleDescription, changed))
            {
                UiScalePicker(ui);
            }
        }
        if (language)
        {
            NextRow(ui, ref rows);
            using (OptionRow(ui, t.DisplayOptionsLanguage, null, false))
            {
                LanguagePicker(ui, appState.Locale);
            }
        }
        if (titleBar)
        {
            NextRow(ui, ref rows);
            SystemTitleBarRow(ui);
        }
        return true;
    }

    private static void SystemTitleBarRow(Ui<UiState> ui)
    {
        var t = ui.State.Strings;
        var display = ui.State.Display;
        var system = display.SystemTitleBar;
        var changed = system != DisplayOptions.Default.SystemTitleBar;
        if (SwitchRow(ui, t.DisplayOptionsSystemTitleBar, ref system, "system-title-bar", changed))
        {
            CodeDisplay.SetDisplayOptions(ui.State, display with { SystemTitleBar = system });
        }
    }

    private static void ThemePicker(Ui<UiState> ui, ThemeCatalog<ThemeFile, ThemeContents, AppTheme> themes, ThemeId<ThemeFile> chosen, SettingsPlace settingsPlace)
    {
        using var controls = ui.Row(new Box { Width = ControlWidth(0f, 0f), Gap = ui.Theme.GapTight, AlignY = Align.Center });
        PickTheme(ui, themes, chosen);
        if (settingsPlace.ThemesFolder is null)
        {
            return;
        }

        CustomizeThemeButton(ui);
        OpenThemesFolderButton(ui);
        ReloadThemesButton(ui);
    }

    private static void PickTheme(Ui<UiState> ui, ThemeCatalog<ThemeFile, ThemeContents, AppTheme> themes, ThemeId<ThemeFile> chosen)
    {
        var labels = themes.ListNames(ui.State.Strings.Culture);
        var current = themes.FindThemeIndex(chosen);
        var picked = PickFrom(ui, "theme", labels, current, themes.Ids, themes.Revision);
        if (picked is not null)
        {
            Commands.Later(ui.State, themes.Ids[picked.Value], static (a, theme) => a.Theme = theme);
        }
    }

    private static void CustomizeThemeButton(Ui<UiState> ui)
    {
        var t = ui.State.Strings;
        if (!ui.IconButton("customize-theme", ui.State.Icons.OptionsCustomizeTheme, t.DisplayOptionsCustomizeTheme, variant: ButtonVariant.Neutral))
        {
            return;
        }

        Commands.Later(ui.State, (screen: ui.State, t), static (a, s) => CustomizeTheme(s.screen, a, s.t));
    }

    private static void CustomizeTheme(UiState screen, AppState appState, Strings t)
    {
        var themes = appState.Themes;
        var custom = appState.UserThemes.CreateCustomTheme(appState.Theme, parent => t.ThemeCustomName(themes.GetThemeName(parent, t.Culture)));
        if (custom is null)
        {
            return;
        }

        appState.Theme = custom.Id;
        screen.FilesToOpen.Enqueue(custom.Path);
    }

    private static void OpenThemesFolderButton(Ui<UiState> ui)
    {
        var t = ui.State.Strings;
        if (!ui.IconButton("themes-folder", ui.State.Icons.OptionsOpenThemesFolder, t.DisplayOptionsOpenThemesFolder, variant: ButtonVariant.Neutral))
        {
            return;
        }

        Commands.Later(ui.State, ui.State, static (a, screen) => OpenFolder(screen, a.UserThemes.PrepareFolder()));
    }

    private static void ReloadThemesButton(Ui<UiState> ui)
    {
        var t = ui.State.Strings;
        if (ui.IconButton("reload-themes", ui.State.Icons.OptionsReloadThemes, t.DisplayOptionsReloadThemes, variant: ButtonVariant.Neutral))
        {
            Commands.Later(ui.State, static a => a.UserThemes.ReloadThemes());
        }
    }

    private static void IconThemePicker(Ui<UiState> ui, ThemeCatalog<IconThemeFile, IconThemeContents, IconTheme> themes, ThemeId<IconThemeFile> chosen, SettingsPlace settingsPlace)
    {
        using var controls = ui.Row(new Box { Width = ControlWidth(0f, 0f), Gap = ui.Theme.GapTight, AlignY = Align.Center });
        PickIconTheme(ui, themes, chosen);
        if (settingsPlace.IconThemesFolder is null)
        {
            return;
        }

        CustomizeIconThemeButton(ui);
        OpenIconThemesFolderButton(ui);
        ReloadIconThemesButton(ui);
    }

    private static void PickIconTheme(Ui<UiState> ui, ThemeCatalog<IconThemeFile, IconThemeContents, IconTheme> themes, ThemeId<IconThemeFile> chosen)
    {
        var labels = themes.ListNames(ui.State.Strings.Culture);
        var current = themes.FindThemeIndex(chosen);
        var picked = PickFrom(ui, "icon-theme", labels, current, themes.Ids, themes.Revision);
        if (picked is not null)
        {
            Commands.Later(ui.State, themes.Ids[picked.Value], static (a, theme) => a.IconTheme = theme);
        }
    }

    private static void CustomizeIconThemeButton(Ui<UiState> ui)
    {
        var t = ui.State.Strings;
        if (!ui.IconButton("customize-icon-theme", ui.State.Icons.OptionsCustomizeIconTheme, t.DisplayOptionsCustomizeIconTheme, variant: ButtonVariant.Neutral))
        {
            return;
        }

        Commands.Later(ui.State, (screen: ui.State, t), static (a, s) => CustomizeIconTheme(s.screen, a, s.t));
    }

    private static void CustomizeIconTheme(UiState screen, AppState appState, Strings t)
    {
        var themes = appState.IconThemes;
        var custom = appState.UserIconThemes.CreateCustomTheme(appState.IconTheme, parent => t.ThemeCustomName(themes.GetThemeName(parent, t.Culture)));
        if (custom is null)
        {
            return;
        }

        appState.IconTheme = custom.Id;
        screen.FilesToOpen.Enqueue(custom.Path);
    }

    private static void OpenIconThemesFolderButton(Ui<UiState> ui)
    {
        var t = ui.State.Strings;
        if (!ui.IconButton("icon-themes-folder", ui.State.Icons.OptionsOpenIconThemesFolder, t.DisplayOptionsOpenIconThemesFolder, variant: ButtonVariant.Neutral))
        {
            return;
        }

        Commands.Later(ui.State, ui.State, static (a, screen) => OpenFolder(screen, a.UserIconThemes.PrepareFolder()));
    }

    private static void OpenFolder(UiState screen, string? folder)
    {
        if (folder is not null)
        {
            screen.FoldersToOpen.Enqueue(folder);
        }
    }

    private static void ReloadIconThemesButton(Ui<UiState> ui)
    {
        var t = ui.State.Strings;
        if (ui.IconButton("reload-icon-themes", ui.State.Icons.OptionsReloadIconThemes, t.DisplayOptionsReloadIconThemes, variant: ButtonVariant.Neutral))
        {
            Commands.Later(ui.State, static a => a.UserIconThemes.ReloadThemes());
        }
    }

    private static void UiScalePicker(Ui<UiState> ui)
    {
        var labels = UiScaleLabels.GetValue(ui.State.Strings, ListUiScaleLabels);
        var scales = DisplayOptions.UiScales;
        var current = FindScaleIndex(scales, ui.State.Display.UiScale);
        var picked = PickFrom(ui, "ui-scale", labels, current);
        if (picked is not null)
        {
            CodeDisplay.SetUiScale(ui.State, scales[picked.Value]);
        }
    }

    private static int? FindScaleIndex(IReadOnlyList<float> scales, float scale)
    {
        for (var i = 0; i < scales.Count; i++)
        {
            if (scales[i] == scale)
            {
                return i;
            }
        }
        return null;
    }

    private static string[] ListUiScaleLabels(Strings t)
    {
        return DisplayOptions.UiScales
            .Select(s => UiScaleLabel(t, s))
            .ToArray();
    }

    private static string UiScaleLabel(Strings t, float scale)
    {
        var percent = (int)MathF.Round(scale * 100f);
        return t.DisplayOptionsUiScalePercent(percent);
    }

    private static void LanguagePicker(Ui<UiState> ui, Locale chosen)
    {
        var picked = PickFrom(ui, "language", LocaleLabels, Locales.FindIndex(chosen));
        if (picked is not null)
        {
            Commands.Later(ui.State, Locales.All[picked.Value].Locale, static (a, locale) => a.Locale = locale);
        }
    }

    private static string[] ListLocaleLabels()
    {
        return Locales.All
            .Select(l => l.Endonym)
            .ToArray();
    }

    private static int? PickFrom(Ui<UiState> ui, string id, string[] labels, int? current)
    {
        return PickFrom<string>(ui, id, labels, current, null, 0);
    }

    private static int? PickFrom<TItem>(Ui<UiState> ui, string id, string[] labels, int? current, IReadOnlyList<TItem>? itemIds,
        int revision)
        where TItem : notnull
    {
        var width = ControlWidth(PickerMinWidth, ui.SelectButtonWidth(labels));
        var shown = current ?? 0;
        using var menu = ui.SelectButton(id, labels[shown], width, revision: revision);
        if (!menu.IsOpen)
        {
            return null;
        }

        int? picked = null;
        for (var i = 0; i < labels.Length; i++)
        {
            var itemId = ItemIdAt(itemIds, i);
            if (ui.MenuRadioItem(labels[i], i == current, id: itemId))
            {
                picked = i;
            }
        }
        return picked;
    }

    private static string? ItemIdAt<TItem>(IReadOnlyList<TItem>? itemIds, int index)
        where TItem : notnull
    {
        if (itemIds is null)
        {
            return null;
        }

        return itemIds[index].ToString();
    }

    private static bool CodeSection(Ui<UiState> ui, string[] words)
    {
        var t = ui.State.Strings;
        var display = ui.State.Display;
        var defaults = DisplayOptions.Default;
        var heading = t.DisplayOptionsCode;
        var hint = ui.Memo("zoom-hint", t, static s => s.DisplayOptionsZoomHint(Shortcuts.ZoomReset.Label));
        var font = Shows(t, words, heading, t.DisplayOptionsFont);
        var fontSize = Shows(t, words, heading, t.DisplayOptionsFontSize, hint);
        var lineNumbers = Shows(t, words, heading, t.DisplayOptionsLineNumbers);
        var currentLine = Shows(t, words, heading, t.DisplayOptionsCurrentLine);
        var brackets = Shows(t, words, heading, t.DisplayOptionsMatchingBrackets);
        var usages = Shows(t, words, heading, t.DisplayOptionsUsagesAtCaret);
        var omnibar = Shows(t, words, heading, t.DisplayOptionsOmnibar);
        if (!font && !fontSize && !lineNumbers && !currentLine && !brackets && !usages && !omnibar)
        {
            return false;
        }

        using var section = Section(ui, ui.Memo("code", t, static s => Heading(s.DisplayOptionsCode)));
        using var card = Card(ui);
        var rows = 0;
        if (font)
        {
            NextRow(ui, ref rows);
            using (OptionRow(ui, t.DisplayOptionsFont, null, display.Font is not null))
            {
                FontPicker(ui);
            }
        }
        if (fontSize)
        {
            NextRow(ui, ref rows);
            using (OptionRow(ui, t.DisplayOptionsFontSize, hint, display.FontSize is not null))
            {
                FontSizeStepper(ui);
            }
        }
        if (font || fontSize)
        {
            FontPreview(ui);
        }

        if (lineNumbers)
        {
            NextRow(ui, ref rows);
            var on = display.ShowLineNumbers;
            if (SwitchRow(ui, t.DisplayOptionsLineNumbers, ref on, "line-numbers", on != defaults.ShowLineNumbers))
            {
                CodeDisplay.SetDisplayOptions(ui.State, display with { ShowLineNumbers = on });
            }
        }
        if (currentLine)
        {
            NextRow(ui, ref rows);
            var on = display.HighlightCurrentLine;
            if (SwitchRow(ui, t.DisplayOptionsCurrentLine, ref on, "current-line", on != defaults.HighlightCurrentLine))
            {
                CodeDisplay.SetDisplayOptions(ui.State, display with { HighlightCurrentLine = on });
            }
        }
        if (brackets)
        {
            NextRow(ui, ref rows);
            var on = display.HighlightMatchingBrackets;
            if (SwitchRow(ui, t.DisplayOptionsMatchingBrackets, ref on, "matching-brackets", on != defaults.HighlightMatchingBrackets))
            {
                CodeDisplay.SetDisplayOptions(ui.State, display with { HighlightMatchingBrackets = on });
            }
        }
        if (usages)
        {
            NextRow(ui, ref rows);
            var on = display.HighlightUsagesAtCaret;
            if (SwitchRow(ui, t.DisplayOptionsUsagesAtCaret, ref on, "usages-at-caret", on != defaults.HighlightUsagesAtCaret))
            {
                CodeDisplay.SetDisplayOptions(ui.State, display with { HighlightUsagesAtCaret = on });
            }
        }
        if (omnibar)
        {
            NextRow(ui, ref rows);
            var on = display.ShowOmnibar;
            if (SwitchRow(ui, t.DisplayOptionsOmnibar, ref on, "omnibar", on != defaults.ShowOmnibar))
            {
                CodeDisplay.SetDisplayOptions(ui.State, display with { ShowOmnibar = on });
            }
        }
        return true;
    }

    private static void FontPicker(Ui<UiState> ui)
    {
        var t = ui.State.Strings;
        var choices = ui.State.CodeFonts;
        var chosen = ui.State.Display.Font;
        var shown = CodeDisplay.GetFontName(chosen);
        var width = ControlWidth(PickerMinWidth, MeasureFontPicker(ui, shown), FontPickerMaxWidth);
        using var menu = ui.SelectButton("code-font", shown, width);
        if (!menu.IsOpen)
        {
            return;
        }

        var fonts = choices.Fonts;
        for (var i = 0; i < fonts.Count; i++)
        {
            if (ui.MenuRadioItem(choices.Labels[i], choices.Labels[i] == shown))
            {
                CodeDisplay.SetCodeFont(ui.State, fonts[i]);
            }
        }
        if (!choices.IsListed)
        {
            ui.MenuItem(t.DisplayOptionsFontsLoading, enabled: false, id: "fonts-loading");
        }
    }

    private static float MeasureFontPicker(Ui<UiState> ui, string shown)
    {
        var offered = ui.SelectButtonWidth(ui.State.CodeFonts.Labels);
        var chosen = ui.SelectButtonWidth([shown]);
        var widest = MathF.Max(offered, chosen);
        return MathF.Min(widest, FontPickerMaxWidth);
    }

    private static void FontSizeStepper(Ui<UiState> ui)
    {
        var t = ui.State.Strings;
        var theme = ui.Theme;
        var display = ui.State.Display;
        var smaller = ui.Memo("smaller", t, static s => s.DisplayOptionsSmaller(Shortcuts.ZoomOut.Label));
        if (ui.IconButton("smaller", ui.State.Icons.OptionsFontSmaller, smaller, CodeDisplay.CanZoomOut(display, theme), ButtonVariant.Neutral))
        {
            CodeDisplay.ZoomCode(ui.State, -1);
        }

        var size = CodeDisplay.GetFontSize(display, theme);
        var text = ui.Memo("size", size, static s => FormatFontSize(s));
        ui.Text(text, new Box { Width = Sizing.Fixed(MeasureFontSizeSlot(ui)) }, align: Align.Center);
        var larger = ui.Memo("larger", t, static s => s.DisplayOptionsLarger(Shortcuts.ZoomIn.Label));
        if (ui.IconButton("larger", ui.State.Icons.OptionsFontLarger, larger, CodeDisplay.CanZoomIn(display, theme), ButtonVariant.Neutral))
        {
            CodeDisplay.ZoomCode(ui.State, 1);
        }
    }

    // Under the font size, so every change to the font or its size shows as it is made.
    private static void FontPreview(Ui<UiState> ui)
    {
        var theme = ui.Theme;
        var size = CodeDisplay.GetFontSize(ui.State.Display, theme);
        using var row = ui.Column(new Box
        {
            Width = Sizing.Grow(),
            Padding = new Spacing(theme.PanelPadding, 0f, theme.PanelPadding, theme.Gap),
        });
        using var sample = ui.Column(new Box
        {
            Width = Sizing.Grow(),
            Padding = new Spacing(theme.ControlPaddingX, theme.Gap),
            Background = theme.EditorBackground,
            BorderColor = theme.Separator,
            BorderWidth = theme.BorderWidth,
            Radius = theme.CornerRadius,
        });
        var color = ui.State.CodeViewPalette.Plain;
        ui.Text(PreviewText, new Box { Width = Sizing.Grow() }, color: color, size: size, family: MonoFonts.Code, wrap: true);
    }

    private static string FormatFontSize(float size)
    {
        return size.ToString(FontSizeFormat, CultureInfo.CurrentCulture);
    }

    // The largest size's width, so the buttons beside it stay put as the size changes.
    private static float MeasureFontSizeSlot(Ui<UiState> ui)
    {
        var widest = FormatFontSize(CodeFontSize.Max);
        var width = ui.MeasureText(widest);
        return MathF.Ceiling(width) + ui.Theme.Gap * 2;
    }

    private static bool TreeSection(Ui<UiState> ui, string[] words)
    {
        var t = ui.State.Strings;
        var display = ui.State.Display;
        var defaults = DisplayOptions.Default;
        var heading = t.DisplayOptionsTree;
        var tokens = Shows(t, words, heading, t.DisplayOptionsTokens, t.DisplayOptionsTokensDescription);
        var emptyTables = Shows(t, words, heading, t.DisplayOptionsEmptyTables);
        var nested = Shows(t, words, heading, t.DisplayOptionsNestedNamespaces);
        var previewTabs = Shows(t, words, heading, t.DisplayOptionsPreviewTabs);
        if (!tokens && !emptyTables && !nested && !previewTabs)
        {
            return false;
        }

        using var section = Section(ui, ui.Memo("tree", t, static s => Heading(s.DisplayOptionsTree)));
        using var card = Card(ui);
        var rows = 0;
        if (tokens)
        {
            NextRow(ui, ref rows);
            using (OptionRow(ui, t.DisplayOptionsTokens, t.DisplayOptionsTokensDescription, display.TreeTokens != defaults.TreeTokens))
            {
                TokenFormatPicker(ui);
            }
        }
        if (emptyTables)
        {
            NextRow(ui, ref rows);
            var on = display.ShowEmptyMetadataTables;
            if (SwitchRow(ui, t.DisplayOptionsEmptyTables, ref on, "empty-tables", on != defaults.ShowEmptyMetadataTables))
            {
                CodeDisplay.SetDisplayOptions(ui.State, display with { ShowEmptyMetadataTables = on });
            }
        }
        if (nested)
        {
            NextRow(ui, ref rows);
            var on = display.NestedNamespaces;
            if (SwitchRow(ui, t.DisplayOptionsNestedNamespaces, ref on, "nested-namespaces", on != defaults.NestedNamespaces))
            {
                CodeDisplay.SetDisplayOptions(ui.State, display with { NestedNamespaces = on });
            }
        }
        if (previewTabs)
        {
            NextRow(ui, ref rows);
            var on = display.PreviewTabs;
            if (SwitchRow(ui, t.DisplayOptionsPreviewTabs, ref on, "preview-tabs", on != defaults.PreviewTabs))
            {
                CodeDisplay.SetDisplayOptions(ui.State, display with { PreviewTabs = on });
            }
        }
        return true;
    }

    private static void TokenFormatPicker(Ui<UiState> ui)
    {
        var labels = TokenFormatLabels.GetValue(ui.State.Strings, ListTokenFormatLabels);
        var index = Array.IndexOf(TokenFormats, ui.State.Display.TreeTokens);
        int? current = index >= 0 ? index : null;
        var picked = PickFrom(ui, "tokens", labels, current);
        if (picked is not null)
        {
            CodeDisplay.SetTreeTokens(ui.State, TokenFormats[picked.Value]);
        }
    }

    private static string[] ListTokenFormatLabels(Strings t)
    {
        return TokenFormats
            .Select(f => TokenFormatLabel(t, f))
            .ToArray();
    }

    private static string TokenFormatLabel(Strings t, TokenFormat format)
    {
        return format switch
        {
            TokenFormat.Hex => t.DisplayOptionsTokensHex,
            TokenFormat.Decimal => t.DisplayOptionsTokensDecimal,
            _ => t.DisplayOptionsTokensHidden,
        };
    }

    private static bool SwitchRow(Ui<UiState> ui, string title, ref bool value, string id, bool changed)
    {
        var press = ui.Interact(id + RowSuffix);
        if (press.Hovered)
        {
            ui.SetCursor(MouseCursor.Hand);
        }

        bool flipped;
        using (OptionRow(ui, press.Id, title, null, changed))
        {
            flipped = ui.Switch(id, ref value);
        }

        if (flipped)
        {
            return true;
        }

        if (!press.Clicked)
        {
            return false;
        }

        value = !value;
        return true;
    }

    private static void ResetRow(Ui<UiState> ui)
    {
        var t = ui.State.Strings;
        var changed = ui.State.Display != DisplayOptions.Default;
        var icon = new UiIcon(ui.State.Icons.OptionsResetDisplay);
        using (ui.Row(new Box { Width = Sizing.Grow(), AlignX = Align.End }))
        {
            if (ui.Button(t.DisplayOptionsReset, enabled: changed, id: "reset", variant: ButtonVariant.Ghost, icon: icon))
            {
                CodeDisplay.AskToResetDisplayOptions(ui.State);
            }
        }
    }

    private static string Heading(string text)
    {
        return text.ToUpperInvariant();
    }

    private static Ui.Scope Section(Ui<UiState> ui, string heading)
    {
        var theme = ui.Theme;
        var section = ui.Column(new Box { Width = Sizing.Grow(), Gap = theme.GapTight });
        ui.Text(heading, color: theme.TextMuted, size: theme.FontSizeSmall, bold: true);
        return section;
    }

    private static Ui.Scope Card(Ui<UiState> ui)
    {
        var theme = ui.Theme;
        return ui.Column(new Box
        {
            Width = Sizing.Grow(),
            Gap = 0f,
            Background = theme.Surface,
            BorderColor = theme.Separator,
            BorderWidth = theme.BorderWidth,
            Radius = theme.PanelRadius,
        });
    }

    private static Ui.Scope OptionRow(Ui<UiState> ui, string title, string? description, bool changed)
    {
        return OptionRow(ui, 0, title, description, changed);
    }

    private static Ui.Scope OptionRow(Ui<UiState> ui, int id, string title, string? description, bool changed)
    {
        var theme = ui.Theme;
        var row = ui.Row(id, new Box
        {
            Width = Sizing.Grow(),
            Height = Sizing.Fit(min: theme.ControlHeight + theme.Gap * 2),
            Gap = theme.GroupGap,
            Padding = new Spacing(theme.PanelPadding, theme.Gap),
            AlignY = Align.Center,
        });
        using (ui.Column(new Box { Width = TitleWidth(ui, title, changed), Gap = theme.GapTight }))
        {
            var query = ui.State.DisplayOptionsQuery;
            var mark = OptionSearch.GetMarkBackground(ui);
            var titleRuns = OptionSearch.EnsureMarkedRuns(ui, query, title, ui.StyleText(bold: changed));
            if (titleRuns is not null)
            {
                using (ui.Box(new Box { Width = Sizing.Grow(), Clip = true }))
                {
                    ui.RichText(titleRuns, wrap: false, markBackground: mark);
                }
            }
            else
            {
                ui.Text(title, new Box { Width = Sizing.Grow() }, bold: changed, overflow: TextOverflow.Ellipsis);
            }
            if (description is not null)
            {
                Description(ui, description, query, mark);
            }
        }
        return row;
    }

    private static void Description(Ui<UiState> ui, string description, string query, uint mark)
    {
        var theme = ui.Theme;
        var box = new Box { Width = Sizing.Percent(WholeWidth) };
        var runs = OptionSearch.EnsureMarkedRuns(ui, query, description, ui.StyleText(theme.TextMuted, theme.FontSizeSmall), part: 1);
        if (runs is not null)
        {
            ui.RichText(runs, box, markBackground: mark);
        }
        else
        {
            ui.Text(description, box, color: theme.TextMuted, size: theme.FontSizeSmall, wrap: true);
        }
    }

    // The description wraps in the title's share and asks for no room of its own. When the row is short the control
    // gives up room first, and the title only after it, cut short down to its own width or TitleMinWidth, whichever is
    // less.
    private static Sizing TitleWidth(Ui<UiState> ui, string title, bool bold)
    {
        var width = ui.MeasureText(title, bold: bold);
        return Sizing.Grow(MathF.Min(width, TitleMinWidth)) with { Priority = TitlePriority };
    }

    // Gives up room down to min before the title gives up any.
    private static Sizing ControlWidth(float min, float size, float max = float.PositiveInfinity)
    {
        return Sizing.Shrink(min, size, max);
    }
}
