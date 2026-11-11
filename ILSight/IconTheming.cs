using System.Globalization;
using System.Text;
using System.Text.Json;
using ZGF.Fonts;
using ZGF.Gui.Immediate;
using ZGF.Svg;
using ILSight.Core;
using static ILSight.Preferences;
using static ILSight.Theming;

namespace ILSight;

/// <summary>The icon themes the app can be drawn with: the built-in ones and those the user keeps in the
/// icon themes folder beside the settings, read again, fonts and pictures too, whenever a file in that
/// folder changes. What is wrong in an icon theme is reported once, until it is fixed.</summary>
public static class IconTheming
{
    /// <summary>The reference icon theme: every theme can draw in its fonts, and a key no theme in a chain
    /// draws is drawn as it draws it.</summary>
    public static readonly ThemeId<IconThemeFile> ReferenceId = ThemeId<IconThemeFile>.Of("lucide");

    /// <summary>The catalog of <paramref name="builtIns"/>, the built-in icon themes in the order a menu lists them,
    /// which must include <see cref="IconTheming.ReferenceId"/>, mapping every key.</summary>
    public static ThemeCatalog<IconThemeFile, IconThemeContents, IconTheme> CreateCatalog(IReadOnlyList<IconThemeFile> builtIns)
    {
        var catalog = new ThemeCatalog<IconThemeFile, IconThemeContents, IconTheme>
        {
            BuiltIns = builtIns,
            DefaultId = IconTheming.ReferenceId,
            Build = static (themes, file, chain) => BuildChainTheme(themes, file, chain),
            FindFallbackId = static _ => IconTheming.ReferenceId,
        };
        ThemeCatalogs.ReplaceUserThemes(catalog, []);
        return catalog;
    }

    private static IconTheme BuildChainTheme(ThemeCatalog<IconThemeFile, IconThemeContents, IconTheme> catalog, IconThemeFile file,
        IReadOnlyList<(IconThemeFile File, IconThemeContents Contents)> chain)
    {
        var merged = new MergedIconTheme();
        var isReference = IsReference(file);
        if (!isReference)
        {
            Theming.AddFonts(merged, ThemeCatalogs.GetBuiltInContents(catalog, IconTheming.ReferenceId));
        }

        foreach (var link in chain)
        {
            Theming.AddContents(merged, link.Contents);
        }

        var fallback = isReference ? null : ThemeCatalogs.ResolveTheme(catalog, IconTheming.ReferenceId);
        var errors = new List<ThemeError>();
        var theme = Theming.BuildTheme(merged, fallback, errors);
        ThemeCatalogs.AddCatalogErrors(catalog, errors);
        return theme;
    }

    private static bool IsReference(IconThemeFile file)
    {
        return file.Id.Equals(IconTheming.ReferenceId);
    }

    private static IconThemeFile CreateFile(ThemeId<IconThemeFile> id, string file, IconAssets assets)
    {
        return new IconThemeFile { Id = id, Json = new ThemeJson { File = file }, Assets = assets, Name = new ThemeName.Plain(id.Text) };
    }

    private const string IconThemeFilePattern = "*" + IconThemeFile.Extension;
    private const string SchemaFileName = "icon-theme.schema.json";

    /// <summary>The files whose changes the icon themes folder is watched for: the theme files, the fonts
    /// and the pictures.</summary>
    public static readonly IReadOnlyList<string> IconThemeFiles = [IconThemeFilePattern, "*.ttf", "*.otf", "*.svg"];

    /// <summary>The asset that couldn't be read, with why.</summary>
    public static IconAsset CreateFailedAsset(ThemeProblem problem, string detail)
    {
        return new IconAsset(null, problem, detail);
    }

    /// <summary>Reads the user's icon themes into the catalog and reports what is wrong in any icon theme.</summary>
    public static void ReadIconThemes(AppState appState)
    {
        RefreshUserIconThemes(appState, []);
    }

    /// <summary>Reads the user's icon themes again, reporting only what is newly wrong.</summary>
    public static void ReloadUserIconThemes(AppState appState)
    {
        var known = appState.IconThemes.Errors.ToList();
        RefreshUserIconThemes(appState, known);
    }

    private static void RefreshUserIconThemes(AppState appState, IReadOnlyList<ThemeError> known)
    {
        var files = ReadUserIconThemes(appState.SettingsPlace.IconThemesFolder);
        ThemeCatalogs.ReplaceUserThemes(appState.IconThemes, files);
        ReportNewErrors(appState, appState.IconThemes.Errors, known, ThemeKind.Icon);
    }

    private static List<IconThemeFile> ReadUserIconThemes(string? folder)
    {
        if (folder is null)
        {
            return new List<IconThemeFile>();
        }

        return ListThemeFiles<IconThemeFile>(folder, IconThemeFilePattern, SchemaFileName)
            .Select(static f => IconTheming.ReadThemeFile(f.Id, f.Path))
            .ToList();
    }

    /// <summary>The icon theme the app is drawn with: the chosen one as resolved, or the reference one while
    /// the chosen one's file is missing. The choice is kept, so the icon theme comes back with its file.</summary>
    public static IconTheme ResolveIconTheme(AppState appState)
    {
        return ThemeCatalogs.GetTheme(appState.IconThemes, appState.IconTheme);
    }

    /// <summary>The user's icon themes folder, made with the icon theme schema in it when missing, for the
    /// screen to show; null when the settings live in memory only, or after reporting why it cannot be made.</summary>
    public static string? PrepareIconThemesFolder(AppState appState)
    {
        var folder = appState.SettingsPlace.IconThemesFolder;
        if (folder is null)
        {
            return null;
        }

        var error = CreateThemeFolder(folder, SchemaFileName, BuiltInIconThemes.ReadSchema(), BuiltInIconThemes.ReadGuide());
        if (error is null)
        {
            return folder;
        }

        Errors.Report(appState, new AppProblem.ThemesFolderNotMade(ThemeKind.Icon, error));
        return null;
    }

    /// <summary>Writes a new user icon theme that extends the icon theme shown and lists every icon it can
    /// change, commented out as shown, then reads the icon themes again and switches to it. Returns its file
    /// for the screen to open; null when the settings live in memory only, or after reporting why the
    /// file cannot be written. <paramref name="nameOf"/> names the icon theme by the id of the one it is made from.</summary>
    public static string? CreateCustomIconTheme(AppState appState, Func<ThemeId<IconThemeFile>, string> nameOf)
    {
        var folder = PrepareIconThemesFolder(appState);
        if (folder is null)
        {
            return null;
        }

        var themes = appState.IconThemes;
        var parent = ThemeCatalogs.GetShownId(themes, appState.IconTheme);
        var id = GetFreeCustomId(parent, candidate => IsTaken(themes, folder, candidate));
        var path = Path.Combine(folder, id.Text + IconThemeFile.Extension);
        var text = IconThemeDraft.Compose(nameOf(parent), parent, ThemeCatalogs.GetTheme(themes, parent));
        if (!TryWriteNewFile(appState, path, text, ThemeKind.Icon))
        {
            return null;
        }

        ReloadUserIconThemes(appState);
        SetIconTheme(appState, id);
        return path;
    }

    /// <summary>Reads the user's icon themes again whenever a file in their folder changes, until
    /// <see cref="StopWatchingUserIconThemes"/>, so the chosen icon theme redraws as its files are saved.</summary>
    public static void WatchUserIconThemes(AppState appState)
    {
        var folder = new WatchedThemeFolder(appState.IconThemesWatch, PrepareIconThemesFolder, ReloadUserIconThemes, ThemeKind.Icon);
        WatchThemeFolder(appState, folder);
    }

    public static void StopWatchingUserIconThemes(AppState appState)
    {
        Theming.StopWatch(appState.IconThemesWatch);
    }

    private const string FontsKey = "fonts";
    private const string IconsKey = "icons";
    private const string FontKey = "font";
    private const string GlyphKey = "glyph";
    private const string SvgKey = "svg";
    private const int MaxCodepointDigits = 6;

    private static readonly HashSet<string> Keys = ListKeys();

    /// <summary>Icons the app once drew and no longer does. A theme written while it had them still
    /// names them, so they are passed over without an error.</summary>
    private static readonly HashSet<string> RetiredKeys = ["file.exit"];
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    /// <summary>Reads the icon theme <paramref name="id"/> from the file at <paramref name="path"/> and the
    /// fonts and pictures it names inside its folder; a file that cannot be read has no <see cref="IconThemeFile.Contents"/>.</summary>
    public static IconThemeFile ReadThemeFile(ThemeId<IconThemeFile> id, string path)
    {
        var folder = FullFilePath.Of(Path.GetFullPath(path)).Folder;
        var theme = CreateFile(id, path, IconAssetFolders.Open(folder));
        var json = Theming.ReadJsonBytes(theme.Json, path);
        if (json is not null)
        {
            Theming.ReadJson(theme.Json, json, (ref Utf8JsonReader r, string k, int l) => ReadThemeKey(theme, ref r, k, l));
        }

        IconTheming.KeepThemeContents(theme);
        return theme;
    }

    /// <summary>Reads the icon theme <paramref name="id"/> from <paramref name="json"/>, the content of
    /// <paramref name="file"/>, its fonts and pictures from <paramref name="assets"/>.</summary>
    public static IconThemeFile ReadTheme(ThemeId<IconThemeFile> id, string file, byte[] json, IconAssets assets)
    {
        var theme = CreateFile(id, file, assets);
        Theming.ReadJson(theme.Json, json, (ref Utf8JsonReader r, string k, int l) => ReadThemeKey(theme, ref r, k, l));
        IconTheming.KeepThemeContents(theme);
        return theme;
    }

    private static void KeepThemeContents(IconThemeFile theme)
    {
        if (theme.Json.IsUnreadable)
        {
            return;
        }

        theme.Contents = new IconThemeContents(theme.Fonts, theme.Icons);
    }

    private static void ReadThemeKey(IconThemeFile theme, ref Utf8JsonReader reader, string key, int line)
    {
        switch (key)
        {
            case ThemeJson.SchemaKey:
                reader.Skip();
                break;
            case ThemeJson.NameKey:
                ReadThemeName(theme, ref reader, line);
                break;
            case ThemeJson.ExtendsKey:
                ReadExtends(theme, ref reader, line);
                break;
            case FontsKey:
                ReadFonts(theme, ref reader, line);
                break;
            case IconsKey:
                ReadIcons(theme, ref reader, line);
                break;
            default:
                Theming.SkipUnknownKey(theme.Json, ref reader, key, line);
                break;
        }
    }

    private static void ReadThemeName(IconThemeFile theme, ref Utf8JsonReader reader, int line)
    {
        var name = Theming.ReadJsonName(theme.Json, ref reader, line, theme.Id.Text);
        if (name is not null)
        {
            theme.Name = name;
        }
    }

    private static void ReadExtends(IconThemeFile theme, ref Utf8JsonReader reader, int line)
    {
        var parent = Theming.ReadJsonString(theme.Json, ref reader, ThemeJson.ExtendsKey, line);
        if (parent is null)
        {
            return;
        }

        var id = ThemeId<IconThemeFile>.Parse(parent);
        if (id is null)
        {
            Theming.AddJsonError(theme.Json, line, ThemeProblem.UnknownParent, parent);
            return;
        }
        theme.Extends = new ThemeParent<IconThemeFile>(id, line);
    }

    private static void ReadFonts(IconThemeFile theme, ref Utf8JsonReader reader, int line)
    {
        if (!Theming.IsJsonObject(theme.Json, ref reader, FontsKey, line))
        {
            return;
        }

        while (Theming.ReadJsonKey(theme.Json, ref reader, out var id, out var idLine))
        {
            var key = FontsKey + "." + id;
            var path = Theming.ReadJsonString(theme.Json, ref reader, key, idLine);
            if (path is null)
            {
                continue;
            }

            theme.Fonts[id] = ReadFont(theme, key, id, path, idLine);
        }
    }

    /// <summary>The font <paramref name="id"/> in the file <paramref name="path"/>, loaded once to check
    /// that it can be and to read its characters; broken after reporting why it cannot be.</summary>
    private static IconFont ReadFont(IconThemeFile theme, string key, string id, string path, int line)
    {
        var bytes = ReadAsset(theme, key, path, line);
        if (bytes is null)
        {
            return new IconFont.Broken(path);
        }

        var face = new FontFace(theme.Assets.GetFamily(id, path), bytes);
        var characters = face.ReadCharacters();
        if (characters is null)
        {
            Theming.AddJsonError(theme.Json, line, ThemeProblem.NotAFont, key, path);
            return new IconFont.Broken(path);
        }
        return new IconFont.Loaded(path, face, characters);
    }

    private static void ReadIcons(IconThemeFile theme, ref Utf8JsonReader reader, int line)
    {
        if (!Theming.IsJsonObject(theme.Json, ref reader, IconsKey, line))
        {
            return;
        }

        while (Theming.ReadJsonKey(theme.Json, ref reader, out var key, out var keyLine))
        {
            ReadIcon(theme, ref reader, key, keyLine);
        }
    }

    private static void ReadIcon(IconThemeFile theme, ref Utf8JsonReader reader, string key, int line)
    {
        var subject = IconsKey + "." + key;
        if (RetiredKeys.Contains(key))
        {
            reader.Skip();
            return;
        }
        if (!Keys.Contains(key))
        {
            Theming.SkipUnknownKey(theme.Json, ref reader, subject, line);
            return;
        }
        if (!Theming.IsJsonObject(theme.Json, ref reader, subject, line))
        {
            return;
        }

        var written = ReadIconFields(theme, ref reader, subject);
        var value = MakeValue(theme, subject, written, line);
        if (value is not null)
        {
            theme.Icons[key] = value;
        }
    }

    private static IconThemeFile.IconFields ReadIconFields(IconThemeFile theme, ref Utf8JsonReader reader, string subject)
    {
        var fields = new IconThemeFile.IconFields();
        while (Theming.ReadJsonKey(theme.Json, ref reader, out var name, out var nameLine))
        {
            var key = subject + "." + name;
            switch (name)
            {
                case FontKey:
                    fields = fields with { Font = Theming.ReadJsonString(theme.Json, ref reader, key, nameLine) };
                    break;
                case GlyphKey:
                    fields = fields with { Glyph = Theming.ReadJsonString(theme.Json, ref reader, key, nameLine) };
                    break;
                case SvgKey:
                    fields = fields with { Svg = Theming.ReadJsonString(theme.Json, ref reader, key, nameLine) };
                    break;
                default:
                    Theming.SkipUnknownKey(theme.Json, ref reader, key, nameLine);
                    break;
            }
        }
        return fields;
    }

    /// <summary>The icon <paramref name="fields"/> make; null after reporting why they make none.</summary>
    private static IconValue? MakeValue(IconThemeFile theme, string subject, IconThemeFile.IconFields fields, int line)
    {
        var (font, glyph, svg) = fields;
        if (svg is not null && font is null && glyph is null)
        {
            return ReadPicture(theme, subject, svg, line);
        }

        if (svg is null && font is not null && glyph is not null)
        {
            return MakeGlyph(theme, subject, font, glyph, line);
        }

        Theming.AddJsonError(theme.Json, line, ThemeProblem.InvalidIcon, subject);
        return null;
    }

    private static IconValue? MakeGlyph(IconThemeFile theme, string subject, string font, string glyph, int line)
    {
        var codepoint = ParseCodepoint(glyph);
        if (codepoint is null)
        {
            Theming.AddJsonError(theme.Json, line, ThemeProblem.NotACodepoint, subject, glyph);
            return null;
        }
        return new IconValue.Glyph(font, codepoint.Value, theme.File, line);
    }

    /// <summary>The Unicode scalar value <paramref name="hex"/> writes in up to six hex digits, or null.</summary>
    private static int? ParseCodepoint(string hex)
    {
        if (hex.Length == 0 || hex.Length > MaxCodepointDigits)
        {
            return null;
        }

        if (!int.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var codepoint))
        {
            return null;
        }

        if (codepoint == 0)
        {
            return null;
        }

        if (!Rune.IsValid(codepoint))
        {
            return null;
        }

        return codepoint;
    }

    private static IconValue? ReadPicture(IconThemeFile theme, string subject, string path, int line)
    {
        var bytes = ReadAsset(theme, subject, path, line);
        if (bytes is null)
        {
            return null;
        }

        var text = DecodeText(bytes);
        if (!SvgDocument.TryParse(text, out var svg, out var error))
        {
            Theming.AddJsonError(theme.Json, line, ThemeProblem.InvalidSvg, subject, $"{path}: {error}");
            return null;
        }
        return new IconValue.Picture(path, svg, theme.File, line);
    }

    private static string DecodeText(byte[] bytes)
    {
        var content = bytes.AsSpan();
        if (content.StartsWith(Utf8Bom))
        {
            content = content.Slice(Utf8Bom.Length);
        }

        return Encoding.UTF8.GetString(content);
    }

    /// <summary>The bytes of the file <paramref name="path"/>; null after reporting why it cannot be read.</summary>
    private static byte[]? ReadAsset(IconThemeFile theme, string subject, string path, int line)
    {
        var asset = theme.Assets.Read(path);
        if (asset.Bytes is null)
        {
            Theming.AddJsonError(theme.Json, line, asset.Problem, subject, asset.Detail);
        }

        return asset.Bytes;
    }

    private static HashSet<string> ListKeys()
    {
        return AppIcons.Keys
            .Select(static k => k.Key)
            .ToHashSet();
    }
}
