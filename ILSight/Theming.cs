using System.Diagnostics;
using System.Globalization;
using ZGF.Gui.Immediate;
using static ILSight.Preferences;

namespace ILSight;

/// <summary>A folder of the user's themes of one kind as watching keeps it: the watch over it, how it is
/// made, how its themes are read again, and which themes they are.</summary>
internal sealed record WatchedThemeFolder(
    ThemeFolderWatch Watch,
    Func<AppState, string?> Prepare,
    Action<AppState> Reload,
    ThemeKind Kind);

/// <summary>The themes the app can be drawn in: the built-in ones and those the user keeps in the themes
/// folder beside the settings, read again whenever that folder changes. What is wrong in a theme file is
/// reported once, until it is fixed.</summary>
public static class Theming
{
    private const string ThemeFilePattern = "*" + ThemeFile.Extension;
    private const string SchemaFileName = "theme.schema.json";
    private const string GuideFileName = "README.md";
    private const string CustomSuffix = "-custom";
    private const int FirstCustomNumber = 2;

    /// <summary>A theme of these colors, with the UI theme in the app's compact metrics.</summary>
    public static AppTheme CreateTheme(UiTheme ui, CodeViewPalette code, NodeIconPalette icons)
    {
        return new AppTheme(MakeCompact(ui), code, icons);
    }

    private static UiTheme MakeCompact(UiTheme theme)
    {
        return theme.WithDensity(UiDensity.Compact) with
        {
            ScreenPadding = 0f,
            TabMinWidth = 120f,
            TabMaxWidth = 240f,
        };
    }

    /// <summary>Reads the user's themes into the catalog and reports what is wrong in any theme.</summary>
    public static void LoadThemes(AppState appState)
    {
        LoadUserThemes(appState, []);
    }

    /// <summary>Reads the user's themes again, reporting only what is newly wrong.</summary>
    public static void ReloadUserThemes(AppState appState)
    {
        var known = appState.Themes.Errors.ToList();
        LoadUserThemes(appState, known);
    }

    private static void LoadUserThemes(AppState appState, IReadOnlyList<ThemeError> known)
    {
        var files = ReadUserThemes(appState.SettingsPlace.ThemesFolder);
        appState.Themes.LoadUserThemes(files);
        ReportNewErrors(appState, appState.Themes.Errors, known, ThemeKind.Color);
    }

    private static List<ThemeFile> ReadUserThemes(string? folder)
    {
        if (folder == null)
            return new List<ThemeFile>();
        return ListThemeFiles<ThemeFile>(folder, ThemeFilePattern, SchemaFileName)
            .Select(static f => ThemeFile.ReadFile(f.Id, f.Path))
            .ToList();
    }

    /// <summary>The files <paramref name="pattern"/> matches in <paramref name="folder"/> by name, each
    /// with the id its name gives, without the schema <paramref name="schemaFileName"/> and any whose name
    /// is no id; none when the folder is missing or cannot be listed.</summary>
    internal static List<(ThemeId<TFile> Id, string Path)> ListThemeFiles<TFile>(string folder, string pattern, string schemaFileName)
        where TFile : IThemeFile<TFile>
    {
        var files = new List<(ThemeId<TFile> Id, string Path)>();
        foreach (var path in ListFiles(folder, pattern, schemaFileName))
        {
            var id = ThemeId<TFile>.Parse(Path.GetFileNameWithoutExtension(path));
            if (id != null)
                files.Add((id, path));
        }
        return files;
    }

    private static List<string> ListFiles(string folder, string pattern, string schemaFileName)
    {
        try
        {
            return Directory.EnumerateFiles(folder, pattern)
                .Where(p => !IsNamed(p, schemaFileName))
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new List<string>();
        }
    }

    private static bool IsNamed(string path, string fileName) => string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase);

    /// <summary>Reports the errors of <paramref name="errors"/> not in <paramref name="known"/>, one report
    /// per file, as problems of themes of <paramref name="kind"/>.</summary>
    internal static void ReportNewErrors(AppState appState, IReadOnlyList<ThemeError> errors, IReadOnlyList<ThemeError> known,
        ThemeKind kind)
    {
        var byFile = errors
            .Where(e => !known.Contains(e))
            .GroupBy(static e => e.File);
        foreach (var fileErrors in byFile)
            appState.ReportError(new AppProblem.ThemeFileProblems(kind, fileErrors.Key, fileErrors.ToList()));
    }

    /// <summary>The theme the app is drawn in: the chosen one as resolved, or the built-in dark one while
    /// the chosen one's file is missing. The choice is kept, so the theme comes back with its file.</summary>
    public static AppTheme ResolveTheme(AppState appState) => appState.Themes.Find(appState.Theme);

    /// <summary>The user's themes folder, made with the theme schema in it when missing, for the screen to
    /// show; null when the settings live in memory only, or after reporting why it cannot be made.</summary>
    public static string? PrepareThemesFolder(AppState appState)
    {
        var folder = appState.SettingsPlace.ThemesFolder;
        if (folder == null)
            return null;
        var error = CreateThemeFolder(folder, SchemaFileName, BuiltInThemes.ReadSchema(), BuiltInThemes.ReadGuide());
        if (error == null)
            return folder;
        appState.ReportError(new AppProblem.ThemesFolderNotMade(ThemeKind.Color, error));
        return null;
    }

    /// <summary>Makes <paramref name="folder"/> with <paramref name="schema"/> in it as
    /// <paramref name="schemaFileName"/>, for theme files to name in <c>$schema</c>, and a README of
    /// <paramref name="guide"/> on writing themes. Returns why it failed, or null.</summary>
    internal static string? CreateThemeFolder(string folder, string schemaFileName, string schema, string guide)
    {
        try
        {
            Directory.CreateDirectory(folder);
            WriteIfChanged(Path.Combine(folder, schemaFileName), schema);
            WriteIfChanged(Path.Combine(folder, GuideFileName), guide);
            return null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return $"{folder}{Environment.NewLine}{e.Message}";
        }
    }

    private static void WriteIfChanged(string path, string text)
    {
        if (IsWritten(path, text))
            return;
        File.WriteAllText(path, text);
    }

    private static bool IsWritten(string path, string text)
    {
        if (!File.Exists(path))
            return false;
        return File.ReadAllText(path) == text;
    }

    /// <summary>Writes a new user theme that extends the theme shown and lists every color it can change,
    /// commented out at the color shown, then reads the themes again and switches to it. Returns its file
    /// for the screen to open; null when the settings live in memory only, or after reporting why the
    /// file cannot be written. <paramref name="nameOf"/> names the theme by the id of the one it is made from.</summary>
    public static string? CreateCustomTheme(AppState appState, Func<ThemeId<ThemeFile>, string> nameOf)
    {
        var folder = PrepareThemesFolder(appState);
        if (folder == null)
            return null;
        var themes = appState.Themes;
        var parent = themes.FindShownId(appState.Theme);
        var id = FindFreeCustomId(parent, candidate => IsTaken(themes, folder, candidate));
        var path = Path.Combine(folder, id.Text + ThemeFile.Extension);
        var text = ThemeDraft.Compose(nameOf(parent), parent, themes.Find(parent));
        if (!TryWriteNewFile(appState, path, text, ThemeKind.Color))
            return null;
        ReloadUserThemes(appState);
        SetTheme(appState, id);
        return path;
    }

    /// <summary><paramref name="parent"/> followed by <c>-custom</c>, numbered from 2 while
    /// <paramref name="isTaken"/> says a theme or a file already has that id.</summary>
    internal static ThemeId<TFile> FindFreeCustomId<TFile>(ThemeId<TFile> parent, Func<ThemeId<TFile>, bool> isTaken)
        where TFile : IThemeFile<TFile>
    {
        var id = ThemeId<TFile>.Of(parent.Text + CustomSuffix);
        var number = FirstCustomNumber;
        while (isTaken(id))
        {
            id = ThemeId<TFile>.Of($"{parent.Text}{CustomSuffix}-{number}");
            number++;
        }
        return id;
    }

    /// <summary>True when a theme of <paramref name="themes"/> has the id <paramref name="id"/>, or a file
    /// in <paramref name="folder"/> is named by it.</summary>
    internal static bool IsTaken<TFile, TContents, TTheme>(ThemeCatalog<TFile, TContents, TTheme> themes, string folder, ThemeId<TFile> id)
        where TFile : class, IThemeFile<TFile, TContents>
        where TContents : class
        where TTheme : class
    {
        if (themes.IndexOf(id) >= 0)
            return true;
        return File.Exists(Path.Combine(folder, id.Text + ThemeFile.Extension));
    }

    /// <summary>Writes <paramref name="text"/>, a theme of <paramref name="kind"/>, to a file that must not
    /// exist yet; false after reporting why it cannot.</summary>
    internal static bool TryWriteNewFile(AppState appState, string path, string text, ThemeKind kind)
    {
        try
        {
            using var file = new StreamWriter(path, new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write });
            file.Write(text);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            appState.ReportError(new AppProblem.CustomThemeNotWritten(kind, path, e.Message));
            return false;
        }
    }

    /// <summary>Reads the user's themes again whenever their folder changes, until
    /// <see cref="StopWatchingUserThemes"/>, so the chosen theme redraws as its file is saved.</summary>
    public static void WatchUserThemes(AppState appState)
    {
        var folder = new WatchedThemeFolder(appState.ThemesWatch, PrepareThemesFolder, ReloadUserThemes, ThemeKind.Color);
        WatchThemeFolder(appState, folder);
    }

    /// <summary>Reads the themes of <paramref name="folder"/> again whenever it changes, until its watch
    /// stops. Makes the folder when it is missing, and again when it is removed while watched. When the
    /// folder cannot be made or watched, reports why and leaves the themes to be read again by hand.</summary>
    internal static void WatchThemeFolder(AppState appState, WatchedThemeFolder folder) =>
        Background.Start(WatchThemeFolderAsync(appState, folder), appState.ReportError);

    private static async Task WatchThemeFolderAsync(AppState appState, WatchedThemeFolder folder)
    {
        if (!StartWatching(appState, folder))
            return;
        var restartedSinceChange = false;
        while (true)
        {
            var outcome = await Background.Wait(folder.Watch.NextChange());
            if (outcome is not Outcome<ThemeFolderEvent>.Ok { Value: var news })
                return;
            if (news.Kind == ThemeFolderEventKind.Stopped)
                return;
            var isLost = news.Kind == ThemeFolderEventKind.Lost;
            if (isLost && !RestartWatching(appState, folder, news.Reason, restartedSinceChange))
                return;
            restartedSinceChange = isLost;
            folder.Reload(appState);
        }
    }

    /// <summary>Makes the folder and watches it; false when there is none to watch, or after reporting
    /// why it cannot be made or watched.</summary>
    private static bool StartWatching(AppState appState, WatchedThemeFolder folder)
    {
        var path = folder.Prepare(appState);
        if (path == null)
            return false;
        var error = folder.Watch.Start(path);
        if (error == null)
            return true;
        ReportUnwatched(appState, folder, error);
        return false;
    }

    /// <summary>Watches the folder again after the watch was lost, as when the folder was removed. Once
    /// it was restarted with no change seen since, it gives up rather than lose it again and again, and
    /// reports <paramref name="reason"/>.</summary>
    private static bool RestartWatching(AppState appState, WatchedThemeFolder folder, string reason, bool restartedSinceChange)
    {
        if (!restartedSinceChange)
            return StartWatching(appState, folder);
        ReportUnwatched(appState, folder, reason);
        return false;
    }

    private static void ReportUnwatched(AppState appState, WatchedThemeFolder folder, string reason)
    {
        appState.ReportError(new AppProblem.ThemesNotWatched(folder.Kind, reason));
    }

    public static void StopWatchingUserThemes(AppState appState)
    {
        appState.ThemesWatch.Stop();
    }

    private const string UiSection = "ui";
    private const string CodeSection = "code";
    private const string IconsSection = "icons";
    private const string KeySeparator = ", ";

    /// <summary>Takes the code and icon colors of <paramref name="contents"/>, under all that is added later.</summary>
    internal static void AddCodeAndIcons(MergedTheme merged, ThemeContents contents)
    {
        StackValues(merged.Code, contents.Code);
        StackValues(merged.Icons, contents.Icons);
    }

    /// <summary>Takes everything <paramref name="contents"/> sets, over what was added before.</summary>
    internal static void AddContents(MergedTheme merged, ThemeContents contents)
    {
        foreach (var (name, value) in contents.Palette)
            merged.Palette[name] = value;
        StackValues(merged.Ui, contents.Ui);
        StackValues(merged.Code, contents.Code);
        StackValues(merged.Icons, contents.Icons);
    }

    private static void StackValues(Dictionary<string, List<ThemeValue>> merged, IReadOnlyDictionary<string, ThemeValue> added)
    {
        foreach (var (key, value) in added)
            EnsureLayers(merged, key).Add(value);
    }

    private static List<ThemeValue> EnsureLayers(Dictionary<string, List<ThemeValue>> merged, string key)
    {
        if (merged.TryGetValue(key, out var layers))
            return layers;
        layers = new List<ThemeValue>();
        merged[key] = layers;
        return layers;
    }

    /// <summary>The theme the merged colors make, or null when a required color is missing; adds what
    /// is wrong to <paramref name="errors"/>, a missing color as an error of <paramref name="file"/>.</summary>
    internal static AppTheme? BuildTheme(MergedTheme merged, string file, ICollection<ThemeError> errors)
    {
        var ui = LookUpColors(merged, UiSection, merged.Ui, errors);
        var code = LookUpColors(merged, CodeSection, merged.Code, errors);
        var icons = LookUpColors(merged, IconsSection, merged.Icons, errors);
        var missing = new List<string>();
        AddMissing(missing, UiSection, UiTheme.ColorKeys, ui);
        AddMissing(missing, CodeSection, CodeViewPalette.ColorKeys, code);
        AddMissing(missing, IconsSection, NodeIconPalette.ColorKeys, icons);
        if (missing.Count > 0)
        {
            errors.Add(new ThemeError(file, 0, ThemeProblem.MissingColors, Detail: string.Join(KeySeparator, missing)));
            return null;
        }
        return Theming.CreateTheme(UiTheme.FromColors(ui), CodeViewPalette.FromColors(code), NodeIconPalette.FromColors(icons));
    }

    private static Dictionary<string, uint> LookUpColors(MergedTheme merged, string section, Dictionary<string, List<ThemeValue>> values, ICollection<ThemeError> errors)
    {
        var colors = new Dictionary<string, uint>();
        foreach (var (key, layers) in values)
            LookUpTopColor(merged, section, key, layers, colors, errors);
        return colors;
    }

    /// <summary>Takes the color of the topmost of <paramref name="layers"/> that has one, reporting each
    /// palette name above it that the palette does not have.</summary>
    private static void LookUpTopColor(MergedTheme merged, string section, string key, List<ThemeValue> layers, Dictionary<string, uint> colors, ICollection<ThemeError> errors)
    {
        for (var i = layers.Count - 1; i >= 0; i--)
        {
            var value = layers[i];
            if (TryLookUp(merged, value, out var color))
            {
                colors[key] = color;
                return;
            }
            errors.Add(new ThemeError(value.File, value.Line, ThemeProblem.UnknownPaletteName, section + "." + key, value.Text));
        }
    }

    private static bool TryLookUp(MergedTheme merged, ThemeValue value, out uint color)
    {
        if (!value.IsPaletteName)
            return UiColor.TryParse(value.Text, out color);
        if (merged.Palette.TryGetValue(value.Text, out var named))
            return UiColor.TryParse(named.Text, out color);
        color = 0;
        return false;
    }

    private static void AddMissing(List<string> missing, string section, IReadOnlyList<ThemeColorKey> keys, Dictionary<string, uint> colors)
    {
        var absent = keys
            .Where(static k => k.IsRequired)
            .Where(k => !colors.ContainsKey(k.Key))
            .Select(k => section + "." + k.Key);
        missing.AddRange(absent);
    }

    private const string IconsPrefix = "icons.";
    private const string CodepointFormat = "X4";

    /// <summary>Takes the fonts of <paramref name="contents"/>, under all that is added later.</summary>
    internal static void AddFonts(MergedIconTheme merged, IconThemeContents contents)
    {
        foreach (var (id, font) in contents.Fonts)
            merged.Fonts[id] = font;
    }

    /// <summary>Takes everything <paramref name="contents"/> sets, over what was added before.</summary>
    internal static void AddContents(MergedIconTheme merged, IconThemeContents contents)
    {
        AddFonts(merged, contents);
        foreach (var (key, value) in contents.Icons)
            EnsureLayers(merged, key).Add(value);
    }

    private static List<IconValue> EnsureLayers(MergedIconTheme merged, string key)
    {
        if (merged.Icons.TryGetValue(key, out var layers))
            return layers;
        layers = new List<IconValue>();
        merged.Icons[key] = layers;
        return layers;
    }

    /// <summary>The theme the merged icons make, a key none of them draws taken from
    /// <paramref name="fallback"/>; adds what is wrong to <paramref name="errors"/>.</summary>
    internal static IconTheme BuildTheme(MergedIconTheme merged, IconTheme? fallback, ICollection<ThemeError> errors)
    {
        var glyphs = new Dictionary<string, IconGlyph>();
        var values = new Dictionary<string, IconValue>();
        foreach (var (key, layers) in merged.Icons)
            TakeTopIcon(merged, key, layers, glyphs, values, errors);
        if (fallback == null)
            return CreateIconTheme(new IconSet(glyphs), ListFaces(merged, null), values);
        foreach (var (key, value) in fallback.Values)
            values.TryAdd(key, value);
        return CreateIconTheme(new IconSet(glyphs, fallback.Set), ListFaces(merged, fallback), values);
    }

    private static IconTheme CreateIconTheme(IconSet set, IReadOnlyList<FontFace> fonts, Dictionary<string, IconValue> values)
    {
        return new IconTheme { Icons = new AppIcons(set), Set = set, Fonts = fonts, Values = values };
    }

    /// <summary>Takes the topmost of <paramref name="layers"/> that can be drawn, reporting each one above
    /// it whose font no theme declares.</summary>
    private static void TakeTopIcon(MergedIconTheme merged, string key, List<IconValue> layers, Dictionary<string, IconGlyph> glyphs, Dictionary<string, IconValue> values,
        ICollection<ThemeError> errors)
    {
        for (var i = layers.Count - 1; i >= 0; i--)
        {
            var value = layers[i];
            var glyph = MakeGlyph(merged, key, value, errors);
            if (glyph == null)
                continue;
            glyphs[key] = glyph.Value;
            values[key] = value;
            return;
        }
    }

    /// <summary>What <paramref name="value"/> draws; null when its font is broken, or after reporting that
    /// no theme declares its font or that the font has no glyph for it.</summary>
    private static IconGlyph? MakeGlyph(MergedIconTheme merged, string key, IconValue value, ICollection<ThemeError> errors)
    {
        return value switch
    {
        IconValue.Picture picture => IconGlyph.FromSvg(picture.Svg),
        IconValue.Glyph glyph => MakeFontGlyph(merged, key, glyph, errors),
        _ => throw new UnreachableException(),
    };
    }

    private static IconGlyph? MakeFontGlyph(MergedIconTheme merged, string key, IconValue.Glyph glyph, ICollection<ThemeError> errors)
    {
        if (!merged.Fonts.TryGetValue(glyph.Font, out var font))
        {
            errors.Add(new ThemeError(glyph.File, glyph.Line, ThemeProblem.UnknownFont, IconsPrefix + key, glyph.Font));
            return null;
        }
        if (font is not IconFont.Loaded loaded)
            return null;
        if (!loaded.Characters.Contains(glyph.Codepoint))
        {
            var codepoint = glyph.Codepoint.ToString(CodepointFormat, CultureInfo.InvariantCulture);
            errors.Add(new ThemeError(glyph.File, glyph.Line, ThemeProblem.MissingGlyph, IconsPrefix + key, codepoint));
            return null;
        }
        var text = char.ConvertFromUtf32(glyph.Codepoint);
        return IconGlyph.FromFont(text, loaded.Face.Family);
    }

    /// <summary>The faces of the merged fonts that could be read, then those of <paramref name="fallback"/>
    /// in families not drawn by them.</summary>
    private static List<FontFace> ListFaces(MergedIconTheme merged, IconTheme? fallback)
    {
        var faces = merged.Fonts.Values
            .OfType<IconFont.Loaded>()
            .Select(static f => f.Face)
            .ToList();
        if (fallback == null)
            return faces;
        var families = faces
            .Select(static f => f.Family)
            .ToHashSet();
        var inherited = fallback.Fonts
            .Where(f => !families.Contains(f.Family));
        faces.AddRange(inherited);
        return faces;
    }
}
