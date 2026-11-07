using System.Text.Json;
using System.ComponentModel;
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
        ThemeCatalogs.LoadUserThemes(appState.Themes, files);
        ReportNewErrors(appState, appState.Themes.Errors, known, ThemeKind.Color);
    }

    private static List<ThemeFile> ReadUserThemes(string? folder)
    {
        if (folder == null)
            return new List<ThemeFile>();
        return ListThemeFiles<ThemeFile>(folder, ThemeFilePattern, SchemaFileName)
            .Select(static f => Theming.ReadThemeFile(f.Id, f.Path))
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
    public static AppTheme ResolveTheme(AppState appState) => ThemeCatalogs.FindTheme(appState.Themes, appState.Theme);

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
    /// <summary>The catalog of <paramref name="builtIns"/>, the built-in themes in the order a menu lists them, which
    /// must include <see cref="ThemeCatalog.DarkId"/> and <see cref="ThemeCatalog.LightId"/>.</summary>
    public static ThemeCatalog CreateCatalog(IReadOnlyList<ThemeFile> builtIns)
    {
        var catalog = new ThemeCatalog
        {
            BuiltIns = builtIns,
            DefaultId = ThemeCatalog.DarkId,
            Build = static (themes, file, chain) => BuildChainTheme(themes, file, chain),
            FindFallbackId = static file => GetFallbackId(file),
        };
        ThemeCatalogs.LoadUserThemes(catalog, []);
        return catalog;
    }

    /// <summary>The theme the chain makes, merged over the code and icon colors of the built-in theme of
    /// its root's type, or null when it does not make a whole one.</summary>
    private static AppTheme? BuildChainTheme(ThemeCatalog<ThemeFile, ThemeContents, AppTheme> catalog, ThemeFile file, IReadOnlyList<(ThemeFile File, ThemeContents Contents)> chain)
    {
        var root = chain[0].File;
        var implicitBase = FindImplicitBase(catalog, root);
        if (implicitBase == null)
            return null;
        var merged = new MergedTheme();
        AddCodeAndIcons(merged, implicitBase);
        foreach (var link in chain)
            AddContents(merged, link.Contents);
        var errors = new List<ThemeError>();
        var theme = BuildTheme(merged, file.File, errors);
        ThemeCatalogs.AddCatalogErrors(catalog, errors);
        return theme;
    }

    /// <summary>The built-in theme whose code and icon colors <paramref name="root"/>, which extends no
    /// other, builds on: the one of its type. Null after reporting that it has no type.</summary>
    private static ThemeContents? FindImplicitBase(ThemeCatalog<ThemeFile, ThemeContents, AppTheme> catalog, ThemeFile root)
    {
        if (root.Type == null)
        {
            ThemeCatalogs.AddCatalogError(catalog, new ThemeError(root.File, 0, ThemeProblem.NoType));
            return null;
        }
        return ThemeCatalogs.GetBuiltInContents(catalog, GetIdOfType(root.Type.Value));
    }

    private static ThemeId<ThemeFile> GetFallbackId(ThemeFile file)
    {
        var type = file.Type ?? ThemeType.Dark;
        return GetIdOfType(type);
    }

    private static ThemeId<ThemeFile> GetIdOfType(ThemeType type)
    {
        return type == ThemeType.Light ? ThemeCatalog.LightId : ThemeCatalog.DarkId;
    }

    private static ThemeFile CreateFile(ThemeId<ThemeFile> id, string file)
    {
        return new ThemeFile { Id = id, Json = new ThemeJson { File = file }, Name = new ThemeName.Plain(id.Text) };
    }

    /// <summary>A watch of the color theme files at the top of a folder.</summary>
    public static ThemeFolderWatch CreateThemeWatch()
    {
        return new ThemeFolderWatch { Filters = [ThemeFolderWatch.ThemeFilter], IncludesSubfolders = false };
    }

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
        var parent = ThemeCatalogs.FindShownId(themes, appState.Theme);
        var id = FindFreeCustomId(parent, candidate => IsTaken(themes, folder, candidate));
        var path = Path.Combine(folder, id.Text + ThemeFile.Extension);
        var text = ThemeDraft.Compose(nameOf(parent), parent, ThemeCatalogs.FindTheme(themes, parent));
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
        if (ThemeCatalogs.FindThemeIndex(themes, id) >= 0)
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
            var outcome = await Background.Wait(Theming.FindNextChange(folder.Watch));
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
        var error = Theming.StartWatch(folder.Watch, path);
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
        Theming.StopWatch(appState.ThemesWatch);
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

    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    private static readonly JsonReaderOptions Options = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Reads the File at <paramref name="path"/>; null after marking it unreadable.</summary>
    internal static byte[]? ReadJsonBytes(ThemeJson json, string path)
    {
        try
        {
            return System.IO.File.ReadAllBytes(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            MarkJsonUnreadable(json, 0, ThemeProblem.Unreadable, e.Message);
            return null;
        }
    }

    /// <summary>Reads <paramref name="bytes"/>, which must hold one object, handing each of its keys to
    /// <paramref name="readKey"/>.</summary>
    internal static void ReadJson(ThemeJson json, byte[] bytes, ThemeJson.TopLevelKey readKey)
    {
        var content = SkipBom(bytes);
        json.LineStarts = FindLineStarts(content);
        var reader = new Utf8JsonReader(content, Options);
        try
        {
            ReadJsonRoot(json, ref reader, readKey);
        }
        catch (JsonException e)
        {
            var lineFromZero = e.LineNumber ?? 0;
            MarkJsonUnreadable(json, (int)lineFromZero + 1, ThemeProblem.InvalidJson, e.Message);
        }
        catch (InvalidOperationException e)
        {
            MarkJsonUnreadable(json, GetLine(json, reader), ThemeProblem.InvalidJson, e.Message);
        }
    }

    private static ReadOnlySpan<byte> SkipBom(byte[] json)
    {
        var content = json.AsSpan();
        if (content.StartsWith(Utf8Bom))
            return content.Slice(Utf8Bom.Length);
        return content;
    }

    private static List<int> FindLineStarts(ReadOnlySpan<byte> content)
    {
        var starts = new List<int> { 0 };
        for (var i = 0; i < content.Length; i++)
        {
            if (content[i] == (byte)'\n')
                starts.Add(i + 1);
        }
        return starts;
    }

    private static void ReadJsonRoot(ThemeJson json, ref Utf8JsonReader reader, ThemeJson.TopLevelKey readKey)
    {
        reader.Read();
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            MarkJsonUnreadable(json, GetLine(json, reader), ThemeProblem.RootNotAnObject);
            return;
        }
        while (ReadJsonKey(json, ref reader, out var key, out var line))
            readKey(ref reader, key, line);
        reader.Read();
    }

    /// <summary>Moves past the next key of the object the reader is in, onto its value. False at the
    /// object's end.</summary>
    internal static bool ReadJsonKey(ThemeJson json, ref Utf8JsonReader reader, out string key, out int line)
    {
        reader.Read();
        key = "";
        line = 0;
        if (reader.TokenType == JsonTokenType.EndObject)
            return false;
        key = reader.GetString()!;
        line = GetLine(json, reader);
        reader.Read();
        return true;
    }

    /// <summary>The name the reader is on: a string, or an object of names by language tag. Null after
    /// reporting that it is neither.</summary>
    internal static ThemeName? ReadJsonName(ThemeJson json, ref Utf8JsonReader reader, int line, string fallback)
    {
        if (reader.TokenType == JsonTokenType.String)
            return new ThemeName.Plain(reader.GetString()!);
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            AddJsonError(json, line, ThemeProblem.InvalidName);
            reader.Skip();
            return null;
        }
        var byTag = ReadNamesByTag(json, ref reader);
        return new ThemeName.PerLanguage(byTag, fallback);
    }

    /// <summary>The names by language tag, tags compared ignoring case; a tag given again is reported
    /// and its first name kept.</summary>
    private static Dictionary<string, string> ReadNamesByTag(ThemeJson json, ref Utf8JsonReader reader)
    {
        var byTag = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (ReadJsonKey(json, ref reader, out var tag, out var line))
        {
            var key = ThemeJson.NameKey + "." + tag;
            var name = ReadJsonString(json, ref reader, key, line);
            if (name == null)
                continue;
            if (!byTag.TryAdd(tag, name))
                AddJsonError(json, line, ThemeProblem.DuplicateLanguage, key);
        }
        return byTag;
    }

    /// <summary>True when the reader is on an object; else skips the value after reporting that it is not one.</summary>
    internal static bool IsJsonObject(ThemeJson json, ref Utf8JsonReader reader, string key, int line)
    {
        if (reader.TokenType == JsonTokenType.StartObject)
            return true;
        AddJsonError(json, line, ThemeProblem.NotAnObject, key);
        reader.Skip();
        return false;
    }

    /// <summary>The string the reader is on, or null after reporting that it is something else.</summary>
    internal static string? ReadJsonString(ThemeJson json, ref Utf8JsonReader reader, string key, int line)
    {
        if (reader.TokenType == JsonTokenType.String)
            return reader.GetString();
        AddJsonError(json, line, ThemeProblem.NotAString, key);
        reader.Skip();
        return null;
    }

    /// <summary>Skips the value of <paramref name="key"/> after reporting that no theme File takes it.</summary>
    internal static void SkipUnknownKey(ThemeJson json, ref Utf8JsonReader reader, string key, int line)
    {
        AddJsonError(json, line, ThemeProblem.UnknownKey, key);
        reader.Skip();
    }

    private static int GetLine(ThemeJson json, Utf8JsonReader reader)
    {
        var offset = (int)reader.TokenStartIndex;
        var index = json.LineStarts.BinarySearch(offset);
        if (index >= 0)
            return index + 1;
        return ~index;
    }

    internal static void MarkJsonUnreadable(ThemeJson json, int line, ThemeProblem problem, string detail = "")
    {
        json.IsUnreadable = true;
        AddJsonError(json, line, problem, "", detail);
    }

    internal static void AddJsonError(ThemeJson json, int line, ThemeProblem problem, string subject = "", string detail = "")
    {
        json.FoundErrors.Add(new ThemeError(json.File, line, problem, subject, detail));
    }

    private const string TypeKey = "type";
    private const string PaletteKey = "palette";
    private const string UiKey = "ui";
    private const string CodeKey = "code";
    private const string IconsKey = "icons";
    private const string DarkType = "dark";
    private const string LightType = "light";
    private const string TransparentBlack = "#00000000";

    private static readonly HashSet<string> UiKeys = ListColorKeys(UiTheme.ColorKeys);
    private static readonly HashSet<string> CodeKeys = ListColorKeys(CodeViewPalette.ColorKeys);
    private static readonly HashSet<string> IconKeys = ListColorKeys(NodeIconPalette.ColorKeys);

    /// <summary>Reads the theme <paramref name="id"/> from the file at <paramref name="path"/>; a file that
    /// cannot be read has no <see cref="Contents"/>.</summary>
    public static ThemeFile ReadThemeFile(ThemeId<ThemeFile> id, string path)
    {
        var theme = CreateFile(id, path);
        var json = Theming.ReadJsonBytes(theme.Json, path);
        if (json != null)
            Theming.ReadJson(theme.Json, json, (ref Utf8JsonReader r, string k, int l) => ReadThemeKey(theme, ref r, k, l));
        Theming.KeepThemeContents(theme);
        return theme;
    }

    /// <summary>Reads the theme <paramref name="id"/> from <paramref name="json"/>, the content of
    /// <paramref name="file"/>.</summary>
    public static ThemeFile ReadTheme(ThemeId<ThemeFile> id, string file, byte[] json)
    {
        var theme = CreateFile(id, file);
        Theming.ReadJson(theme.Json, json, (ref Utf8JsonReader r, string k, int l) => ReadThemeKey(theme, ref r, k, l));
        Theming.KeepThemeContents(theme);
        return theme;
    }

    private static void KeepThemeContents(ThemeFile theme)
    {
        if (theme.Json.IsUnreadable)
            return;
        theme.Contents = new ThemeContents(theme.Palette, theme.Ui, theme.Code, theme.Icons);
    }

    private static void ReadThemeKey(ThemeFile theme, ref Utf8JsonReader reader, string key, int line)
    {
        switch (key)
        {
            case ThemeJson.SchemaKey:
                reader.Skip();
                break;
            case ThemeJson.NameKey:
                ReadThemeName(theme, ref reader, line);
                break;
            case TypeKey:
                ReadType(theme, ref reader, line);
                break;
            case ThemeJson.ExtendsKey:
                ReadExtends(theme, ref reader, line);
                break;
            case PaletteKey:
                ReadPalette(theme, ref reader, line);
                break;
            case UiKey:
                ReadColors(theme, ref reader, key, line, theme.Ui, UiKeys);
                break;
            case CodeKey:
                ReadColors(theme, ref reader, key, line, theme.Code, CodeKeys);
                break;
            case IconsKey:
                ReadColors(theme, ref reader, key, line, theme.Icons, IconKeys);
                break;
            default:
                Theming.SkipUnknownKey(theme.Json, ref reader, key, line);
                break;
        }
    }

    private static void ReadThemeName(ThemeFile theme, ref Utf8JsonReader reader, int line)
    {
        var name = Theming.ReadJsonName(theme.Json, ref reader, line, theme.Id.Text);
        if (name != null)
            theme.Name = name;
    }

    private static void ReadType(ThemeFile theme, ref Utf8JsonReader reader, int line)
    {
        var type = Theming.ReadJsonString(theme.Json, ref reader, TypeKey, line);
        if (type == null)
            return;
        theme.Type = ParseThemeType(type);
        if (theme.Type == null)
            Theming.AddJsonError(theme.Json, line, ThemeProblem.UnknownType, type);
    }

    private static ThemeType? ParseThemeType(string type)
    {
        return type switch
        {
            DarkType => ThemeType.Dark,
            LightType => ThemeType.Light,
            _ => null,
        };
    }

    private static void ReadExtends(ThemeFile theme, ref Utf8JsonReader reader, int line)
    {
        var parent = Theming.ReadJsonString(theme.Json, ref reader, ThemeJson.ExtendsKey, line);
        if (parent == null)
            return;
        var id = ThemeId<ThemeFile>.Parse(parent);
        if (id == null)
        {
            Theming.AddJsonError(theme.Json, line, ThemeProblem.UnknownParent, parent);
            return;
        }
        theme.Extends = new ThemeParent<ThemeFile>(id, line);
    }

    /// <summary>Reads the palette, whose colors must be written out, not named.</summary>
    private static void ReadPalette(ThemeFile theme, ref Utf8JsonReader reader, int line)
    {
        if (!Theming.IsJsonObject(theme.Json, ref reader, PaletteKey, line))
            return;
        while (Theming.ReadJsonKey(theme.Json, ref reader, out var name, out var nameLine))
        {
            var key = PaletteKey + "." + name;
            var color = Theming.ReadJsonString(theme.Json, ref reader, key, nameLine);
            if (color == null)
                continue;
            if (!IsValidColor(theme, key, color, nameLine))
                continue;
            theme.Palette[name] = new ThemeValue(color, theme.File, nameLine);
        }
    }

    /// <summary>Reads a section of colors whose keys are <paramref name="keys"/>, each a color or a palette name.</summary>
    private static void ReadColors(ThemeFile theme, ref Utf8JsonReader reader, string section, int line, Dictionary<string, ThemeValue> colors, HashSet<string> keys)
    {
        if (!Theming.IsJsonObject(theme.Json, ref reader, section, line))
            return;
        while (Theming.ReadJsonKey(theme.Json, ref reader, out var name, out var nameLine))
            ReadColor(theme, ref reader, section, name, nameLine, colors, keys);
    }

    private static void ReadColor(ThemeFile theme, ref Utf8JsonReader reader, string section, string name, int line, Dictionary<string, ThemeValue> colors, HashSet<string> keys)
    {
        var key = section + "." + name;
        if (!keys.Contains(name))
        {
            Theming.SkipUnknownKey(theme.Json, ref reader, key, line);
            return;
        }
        var text = Theming.ReadJsonString(theme.Json, ref reader, key, line);
        if (text == null)
            return;
        var value = new ThemeValue(text, theme.File, line);
        if (!value.IsPaletteName && !IsValidColor(theme, key, text, line))
            return;
        colors[name] = value;
    }

    /// <summary>True when <paramref name="color"/> is a color a theme can hold; else reports why not.</summary>
    private static bool IsValidColor(ThemeFile theme, string key, string color, int line)
    {
        if (!UiColor.TryParse(color, out _))
        {
            Theming.AddJsonError(theme.Json, line, ThemeProblem.NotAColor, key, color);
            return false;
        }
        if (color == TransparentBlack)
        {
            Theming.AddJsonError(theme.Json, line, ThemeProblem.TransparentBlack, key);
            return false;
        }
        return true;
    }

    private static HashSet<string> ListColorKeys(IReadOnlyList<ThemeColorKey> keys)
    {
        return keys
            .Select(static k => k.Key)
            .ToHashSet();
    }

    private const int QuietMilliseconds = 200;
    private const string RemovedReason = "The folder was removed or renamed.";

    private static readonly Task<ThemeFolderEvent> Changed = Task.FromResult(new ThemeFolderEvent(ThemeFolderEventKind.Changed));
    private static readonly Task<ThemeFolderEvent> Stopped = Task.FromResult(new ThemeFolderEvent(ThemeFolderEventKind.Stopped));

    /// <summary>Starts watching <paramref name="folder"/> in place of the folder watched before. Returns
    /// why the system cannot watch it, such as a folder gone or a limit on watches reached, and then
    /// watches nothing; else null.</summary>
    public static string? StartWatch(ThemeFolderWatch watch, string folder)
    {
        StopWatch(watch);
        try
        {
            WatchFolder(watch, folder);
            return null;
        }
        catch (Exception e) when (IsWatchFailure(e))
        {
            StopWatch(watch);
            return DescribeWatchFailure(folder, e);
        }
    }

    private static void WatchFolder(ThemeFolderWatch watch, string folder)
    {
        var watcher = new FileSystemWatcher(folder);
        foreach (var filter in watch.Filters)
            watcher.Filters.Add(filter);
        watcher.IncludeSubdirectories = watch.IncludesSubfolders;
        var quiet = new Timer(state => OnQuiet(watch, state));
        watcher.Changed += (sender, e) => OnChanged(watch, sender, e);
        watcher.Created += (sender, e) => OnChanged(watch, sender, e);
        watcher.Deleted += (sender, e) => OnChanged(watch, sender, e);
        watcher.Renamed += (sender, e) => OnChanged(watch, sender, e);
        watcher.Error += (sender, e) => OnError(watch, sender, e);
        var removalWatcher = WatchRemoval(watch, watcher);
        lock (watch.Gate)
        {
            watch.Quiet = quiet;
            watch.Watcher = watcher;
            watch.RemovalWatcher = removalWatcher;
        }
        watcher.EnableRaisingEvents = true;
        if (removalWatcher != null)
            removalWatcher.EnableRaisingEvents = true;
    }

    /// <summary>Watches the folder holding the one <paramref name="watcher"/> watches, to lose the watch
    /// once that folder is removed or renamed away: macOS tells its own watcher nothing of either. Null
    /// for a folder at the root of a drive, which cannot be removed.</summary>
    private static FileSystemWatcher? WatchRemoval(ThemeFolderWatch watch, FileSystemWatcher watcher)
    {
        var folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(watcher.Path));
        var parent = Path.GetDirectoryName(folder);
        if (parent == null)
            return null;
        var removalWatcher = new FileSystemWatcher(parent, Path.GetFileName(folder))
        {
            NotifyFilter = NotifyFilters.DirectoryName,
        };
        removalWatcher.Deleted += (_, _) => LoseFolder(watch, watcher);
        removalWatcher.Renamed += (_, e) =>
        {
            if (e.OldFullPath == folder)
                LoseFolder(watch, watcher);
        };
        return removalWatcher;
    }

    private static bool IsWatchFailure(Exception e)
    {
        return e is IOException or ArgumentException or UnauthorizedAccessException or PlatformNotSupportedException or Win32Exception;
    }

    private static string DescribeWatchFailure(string folder, Exception e)
    {
        return DescribeWatchFailure(folder, e.Message);
    }

    private static string DescribeWatchFailure(string folder, string message)
    {
        return $"{folder}{Environment.NewLine}{message}";
    }

    /// <summary>Completes once the watched files changed or the watch was lost, at once when either is
    /// pending, or as stopped when watching stops first or is not running.</summary>
    public static Task<ThemeFolderEvent> FindNextChange(ThemeFolderWatch watch)
    {
        lock (watch.Gate)
        {
            var loss = watch.PendingLoss;
            if (loss != null)
                return TakePendingLoss(watch, loss);
            if (watch.Watcher == null)
                return Stopped;
            if (watch.IsChangePending)
                return TakePendingChange(watch);
            watch.Waiter ??= new TaskCompletionSource<ThemeFolderEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
            return watch.Waiter.Task;
        }
    }

    private static Task<ThemeFolderEvent> TakePendingLoss(ThemeFolderWatch watch, string loss)
    {
        watch.PendingLoss = null;
        return Task.FromResult(new ThemeFolderEvent(ThemeFolderEventKind.Lost, loss));
    }

    private static Task<ThemeFolderEvent> TakePendingChange(ThemeFolderWatch watch)
    {
        watch.IsChangePending = false;
        return Changed;
    }

    /// <summary>Stops watching, ending the wait as stopped and dropping what is pending.</summary>
    public static void StopWatch(ThemeFolderWatch watch)
    {
        var waiter = CloseWatch(watch, null);
        waiter?.TrySetResult(new ThemeFolderEvent(ThemeFolderEventKind.Stopped));
    }

    /// <summary>Lets go of the watcher and its timer and drops what is pending, keeping
    /// <paramref name="loss"/> pending when no one waits to be told; returns the waiter to tell. Does
    /// nothing when <paramref name="lost"/>, the watcher that failed, is no longer the one watching.</summary>
    private static TaskCompletionSource<ThemeFolderEvent>? CloseWatch(ThemeFolderWatch watch, string? loss, FileSystemWatcher? lost = null)
    {
        FileSystemWatcher? watcher;
        FileSystemWatcher? removalWatcher;
        Timer? quiet;
        TaskCompletionSource<ThemeFolderEvent>? waiter;
        lock (watch.Gate)
        {
            if (IsWatchReplaced(watch, lost))
                return null;
            watcher = watch.Watcher;
            removalWatcher = watch.RemovalWatcher;
            quiet = watch.Quiet;
            waiter = watch.Waiter;
            watch.Watcher = null;
            watch.RemovalWatcher = null;
            watch.Quiet = null;
            watch.Waiter = null;
            watch.IsChangePending = false;
            watch.PendingLoss = waiter == null ? loss : null;
        }
        watcher?.Dispose();
        removalWatcher?.Dispose();
        quiet?.Dispose();
        return waiter;
    }

    private static void OnChanged(ThemeFolderWatch watch, object sender, FileSystemEventArgs e)
    {
        WaitForQuiet(watch);
    }

    /// <summary>Reads the folder again after the system dropped changes it could not keep up with; on
    /// any other error the system has stopped telling, so the watch is lost.</summary>
    private static void OnError(ThemeFolderWatch watch, object sender, ErrorEventArgs e)
    {
        if (e.GetException() is InternalBufferOverflowException)
        {
            WaitForQuiet(watch);
            return;
        }
        LoseWatch(watch, (FileSystemWatcher)sender, e.GetException());
    }

    private static void LoseWatch(ThemeFolderWatch watch, FileSystemWatcher watcher, Exception error)
    {
        LoseWatch(watch, watcher, DescribeWatchFailure(watcher.Path, error));
    }

    private static void LoseFolder(ThemeFolderWatch watch, FileSystemWatcher watcher)
    {
        LoseWatch(watch, watcher, DescribeWatchFailure(watcher.Path, RemovedReason));
    }

    private static void LoseWatch(ThemeFolderWatch watch, FileSystemWatcher watcher, string reason)
    {
        var waiter = CloseWatch(watch, reason, watcher);
        waiter?.TrySetResult(new ThemeFolderEvent(ThemeFolderEventKind.Lost, reason));
    }

    private static bool IsWatchReplaced(ThemeFolderWatch watch, FileSystemWatcher? lost)
    {
        if (lost == null)
            return false;
        return !ReferenceEquals(lost, watch.Watcher);
    }

    private static void WaitForQuiet(ThemeFolderWatch watch)
    {
        lock (watch.Gate)
            watch.Quiet?.Change(QuietMilliseconds, Timeout.Infinite);
    }

    /// <summary>Hands the settled change to whoever waits for it, else keeps it pending.</summary>
    private static void OnQuiet(ThemeFolderWatch watch, object? state)
    {
        TaskCompletionSource<ThemeFolderEvent>? waiter;
        lock (watch.Gate)
        {
            if (watch.Watcher == null)
                return;
            waiter = watch.Waiter;
            watch.Waiter = null;
            if (waiter == null)
                watch.IsChangePending = true;
        }
        waiter?.TrySetResult(new ThemeFolderEvent(ThemeFolderEventKind.Changed));
    }
}
