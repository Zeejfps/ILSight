using System.Globalization;
using System.Text;
using System.Text.Json;
using ZGF.Fonts;
using ZGF.Gui.Immediate;
using ZGF.Svg;

namespace ILSight;

/// <summary>The icon themes the app can be drawn with, built-in or the user's, as their files make them: each
/// file read leniently with the fonts and pictures it names, every problem kept with its line, and each icon
/// theme built over the ones it extends and the reference one.</summary>
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
        catalog.ReplaceUserThemes([]);
        return catalog;
    }

    private static IconTheme BuildChainTheme(ThemeCatalog<IconThemeFile, IconThemeContents, IconTheme> catalog, IconThemeFile file,
        IReadOnlyList<(IconThemeFile File, IconThemeContents Contents)> chain)
    {
        var merged = new MergedIconTheme();
        var isReference = IsReference(file);
        if (!isReference)
        {
            Theming.AddFonts(merged, catalog.GetBuiltInContents(IconTheming.ReferenceId));
        }

        foreach (var link in chain)
        {
            Theming.AddContents(merged, link.Contents);
        }

        var fallback = isReference ? null : catalog.ResolveTheme(IconTheming.ReferenceId);
        var errors = new List<ThemeError>();
        var theme = Theming.BuildTheme(merged, fallback, errors);
        catalog.AddErrors(errors);
        return theme;
    }

    private static bool IsReference(IconThemeFile file)
    {
        return file.Id.Equals(IconTheming.ReferenceId);
    }

    private static IconThemeFile CreateFile(ThemeId<IconThemeFile> id, string file)
    {
        return new IconThemeFile { Id = id, Json = new ThemeJson { File = file }, Name = new ThemeName.Plain(id.Text) };
    }

    /// <summary>The asset that couldn't be read, with why.</summary>
    public static IconAsset CreateFailedAsset(ThemeProblem problem, string detail)
    {
        return new IconAsset { Bytes = null, Problem = problem, Detail = detail };
    }

    /// <summary>The icon theme the app is drawn with: the chosen one as resolved, or the reference one while
    /// the chosen one's file is missing. The choice is kept, so the icon theme comes back with its file.</summary>
    public static IconTheme ResolveIconTheme(AppState appState)
    {
        return appState.IconThemes.GetTheme(appState.IconTheme);
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

    /// <summary>The icon theme <paramref name="id"/> of <paramref name="file"/>, which could not be read for
    /// <paramref name="reason"/>: it has no <see cref="IconThemeFile.Contents"/>.</summary>
    public static IconThemeFile CreateUnreadableTheme(ThemeId<IconThemeFile> id, string file, string reason)
    {
        var theme = CreateFile(id, file);
        Theming.MarkJsonUnreadable(theme.Json, 0, ThemeProblem.Unreadable, reason);
        return theme;
    }

    /// <summary>What the icon theme <paramref name="id"/> says in <paramref name="json"/>, the content of
    /// <paramref name="file"/>: its icons drawn in fonts, and the fonts and pictures it names, each with its line
    /// (<see cref="IconThemeFile.NamedFonts"/>, <see cref="IconThemeFile.NamedPictures"/>), loaded once read
    /// (<see cref="Complete"/>).</summary>
    public static IconThemeFile Parse(ThemeId<IconThemeFile> id, string file, byte[] json)
    {
        var theme = CreateFile(id, file);
        Theming.ReadJson(theme.Json, json, (ref Utf8JsonReader r, string k, int l) => ReadThemeKey(theme, ref r, k, l));
        IconTheming.KeepThemeContents(theme);
        return theme;
    }

    /// <summary><paramref name="theme"/>, as <see cref="Parse"/> read it, with the fonts and pictures it names loaded
    /// from what reading them came to, <paramref name="named"/> by the path the theme writes: each checked, and what
    /// is wrong with one told at the line naming it; one missing there is unreadable.</summary>
    public static IconThemeFile Complete(IconThemeFile theme, IReadOnlyDictionary<string, IconAsset> named)
    {
        foreach (var (id, font) in theme.NamedFonts)
        {
            theme.Fonts[id] = BuildFont(theme, id, font, GetReadOrDefault(named, font.Path));
        }

        foreach (var (key, picture) in theme.NamedPictures)
        {
            var value = BuildPicture(theme, picture, GetReadOrDefault(named, picture.Path));
            if (value is not null)
            {
                theme.Icons[key] = value;
            }
        }
        return theme;
    }

    /// <summary>What reading <paramref name="path"/> came to, among <paramref name="named"/>; by default, when it is
    /// not there, that it is unreadable.</summary>
    private static IconAsset GetReadOrDefault(IReadOnlyDictionary<string, IconAsset> named, string path)
    {
        if (named.TryGetValue(path, out var asset))
        {
            return asset;
        }

        return CreateFailedAsset(ThemeProblem.AssetUnreadable, path);
    }

    private static void KeepThemeContents(IconThemeFile theme)
    {
        if (theme.Json.IsUnreadable)
        {
            return;
        }

        theme.Contents = new IconThemeContents { Fonts = theme.Fonts, Icons = theme.Icons };
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
        theme.Extends = new ThemeParent<IconThemeFile> { Id = id, Line = line };
    }

    private static void ReadFonts(IconThemeFile theme, ref Utf8JsonReader reader, int line)
    {
        if (!Theming.IsJsonObject(theme.Json, ref reader, FontsKey, line))
        {
            return;
        }

        while (Theming.TryReadJsonKey(theme.Json, ref reader, out var id, out var idLine))
        {
            var key = FontsKey + "." + id;
            var path = Theming.ReadJsonString(theme.Json, ref reader, key, idLine);
            if (path is null)
            {
                continue;
            }

            theme.NamedFonts[id] = new ThemeNamedFile { Path = path, Subject = key, Line = idLine };
        }
    }

    /// <summary>The font <paramref name="id"/>, named by <paramref name="named"/>, loaded from what reading it came to,
    /// <paramref name="asset"/>, once to check that it can be and to read its characters, as the family its source
    /// draws it as, else as its id; broken after reporting why it cannot be.</summary>
    private static IconFont BuildFont(IconThemeFile theme, string id, ThemeNamedFile named, IconAsset asset)
    {
        var bytes = TakeBytes(theme, named, asset);
        if (bytes is null)
        {
            return new IconFont.Broken { Path = named.Path };
        }

        var face = new FontFace(asset.Family ?? id, bytes);
        var characters = face.ReadCharacters();
        if (characters is null)
        {
            Theming.AddJsonError(theme.Json, named.Line, ThemeProblem.NotAFont, named.Subject, named.Path);
            return new IconFont.Broken { Path = named.Path };
        }
        return new IconFont.Loaded { Path = named.Path, Face = face, Characters = characters };
    }

    private static void ReadIcons(IconThemeFile theme, ref Utf8JsonReader reader, int line)
    {
        if (!Theming.IsJsonObject(theme.Json, ref reader, IconsKey, line))
        {
            return;
        }

        while (Theming.TryReadJsonKey(theme.Json, ref reader, out var key, out var keyLine))
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
        TakeIcon(theme, key, subject, written, line);
    }

    /// <summary>The fields of an icon as written, each null when left out.</summary>
    private readonly record struct IconFields
    {
        public string? Font { get; init; }

        public string? Glyph { get; init; }

        public string? Svg { get; init; }
    }

    private static IconFields ReadIconFields(IconThemeFile theme, ref Utf8JsonReader reader, string subject)
    {
        var fields = new IconFields();
        while (Theming.TryReadJsonKey(theme.Json, ref reader, out var name, out var nameLine))
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

    /// <summary>Takes the icon <paramref name="fields"/> make: one drawn in a font at once, a picture named for reading
    /// (<see cref="IconThemeFile.NamedPictures"/>); nothing after reporting why they make none. The one written last
    /// for a key wins.</summary>
    private static void TakeIcon(IconThemeFile theme, string key, string subject, IconFields fields, int line)
    {
        var font = fields.Font;
        var glyph = fields.Glyph;
        var svg = fields.Svg;
        if (svg is not null && font is null && glyph is null)
        {
            theme.NamedPictures[key] = new ThemeNamedFile { Path = svg, Subject = subject, Line = line };
            return;
        }

        if (svg is null && font is not null && glyph is not null)
        {
            TakeGlyph(theme, key, MakeGlyph(theme, subject, font, glyph, line));
            return;
        }

        Theming.AddJsonError(theme.Json, line, ThemeProblem.InvalidIcon, subject);
    }

    private static void TakeGlyph(IconThemeFile theme, string key, IconValue? value)
    {
        if (value is null)
        {
            return;
        }

        theme.NamedPictures.Remove(key);
        theme.Icons[key] = value;
    }

    private static IconValue? MakeGlyph(IconThemeFile theme, string subject, string font, string glyph, int line)
    {
        var codepoint = ParseCodepoint(glyph);
        if (codepoint is null)
        {
            Theming.AddJsonError(theme.Json, line, ThemeProblem.NotACodepoint, subject, glyph);
            return null;
        }
        return new IconValue.Glyph { Font = font, Codepoint = codepoint.Value, File = theme.File, Line = line };
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

    /// <summary>The picture <paramref name="named"/> names, parsed from what reading it came to,
    /// <paramref name="asset"/>; null after reporting why it cannot be.</summary>
    private static IconValue? BuildPicture(IconThemeFile theme, ThemeNamedFile named, IconAsset asset)
    {
        var bytes = TakeBytes(theme, named, asset);
        if (bytes is null)
        {
            return null;
        }

        var text = DecodeText(bytes);
        if (!SvgDocument.TryParse(text, out var svg, out var error))
        {
            Theming.AddJsonError(theme.Json, named.Line, ThemeProblem.InvalidSvg, named.Subject, $"{named.Path}: {error}");
            return null;
        }
        return new IconValue.Picture { SvgPath = named.Path, Svg = svg, File = theme.File, Line = named.Line };
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

    /// <summary>The bytes of the file <paramref name="named"/> names, as reading it came to; null after reporting
    /// why it cannot be read.</summary>
    private static byte[]? TakeBytes(IconThemeFile theme, ThemeNamedFile named, IconAsset asset)
    {
        if (asset.Bytes is null)
        {
            Theming.AddJsonError(theme.Json, named.Line, asset.Problem, named.Subject, asset.Detail);
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
