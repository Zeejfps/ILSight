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

    private static IconThemeFile CreateFile(ThemeId<IconThemeFile> id, string file, IconAssets assets)
    {
        return new IconThemeFile { Id = id, Json = new ThemeJson { File = file }, Assets = assets, Name = new ThemeName.Plain(id.Text) };
    }

    /// <summary>The asset that couldn't be read, with why.</summary>
    public static IconAsset CreateFailedAsset(ThemeProblem problem, string detail)
    {
        return new IconAsset(null, problem, detail);
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
    public static IconThemeFile CreateUnreadableTheme(ThemeId<IconThemeFile> id, string file, IconAssets assets, string reason)
    {
        var theme = CreateFile(id, file, assets);
        Theming.MarkJsonUnreadable(theme.Json, 0, ThemeProblem.Unreadable, reason);
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

        while (Theming.TryReadJsonKey(theme.Json, ref reader, out var id, out var idLine))
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
        var value = MakeValue(theme, subject, written, line);
        if (value is not null)
        {
            theme.Icons[key] = value;
        }
    }

    /// <summary>The fields of an icon as written, each null when left out.</summary>
    private readonly record struct IconFields(string? Font, string? Glyph, string? Svg);

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

    /// <summary>The icon <paramref name="fields"/> make; null after reporting why they make none.</summary>
    private static IconValue? MakeValue(IconThemeFile theme, string subject, IconFields fields, int line)
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
