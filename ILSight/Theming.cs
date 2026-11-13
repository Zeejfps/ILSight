using System.Text.Json;
using System.Globalization;
using ZGF.Gui.Immediate;
using ILSight.CodeViewArea;

namespace ILSight;

/// <summary>The themes the app can be drawn in, built-in or the user's, as their files make them: each file
/// read leniently, every problem kept with its line, and each theme built over the themes it extends.</summary>
public static class Theming
{
    // Read key by key, and held here until all are in, so that the ThemeFile is made once, whole.
    private sealed class ParsedTheme
    {
        public required ThemeId<ThemeFile> Id { get; init; }
        public required ThemeJson Json { get; init; }
        public string File => Json.File;
        public required ThemeName Name;
        public ThemeType? Type;
        public ThemeParent<ThemeFile>? Extends;
        public readonly Dictionary<string, ThemeValue> Palette = new();
        public readonly Dictionary<string, ThemeValue> Ui = new();
        public readonly Dictionary<string, ThemeValue> Code = new();
        public readonly Dictionary<string, ThemeValue> Icons = new();
    }

    /// <summary>The built-in dark theme, which settings start on and an id no theme has shows.</summary>
    public static readonly ThemeId<ThemeFile> DarkId = ThemeId<ThemeFile>.Of("dark");
    public static readonly ThemeId<ThemeFile> LightId = ThemeId<ThemeFile>.Of("light");

    /// <summary>A theme of these colors, with the UI theme in the app's compact metrics.</summary>
    public static AppTheme CreateTheme(UiTheme ui, CodeViewPalette code, NodeIconPalette icons)
    {
        return new AppTheme { Ui = MakeCompact(ui), Code = code, Icons = icons };
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

    /// <summary>The theme the app is drawn in: the chosen one as resolved, or the built-in dark one while
    /// the chosen one's file is missing. The choice is kept, so the theme comes back with its file.</summary>
    public static AppTheme ResolveTheme(AppState appState)
    {
        return appState.Themes.GetTheme(appState.Theme);
    }

    /// <summary>The catalog of <paramref name="builtIns"/>, the built-in themes in the order a menu lists them, which
    /// must include <see cref="Theming.DarkId"/> and <see cref="Theming.LightId"/>.</summary>
    public static ThemeCatalog<ThemeFile, ThemeContents, AppTheme> CreateCatalog(IReadOnlyList<ThemeFile> builtIns)
    {
        var catalog = new ThemeCatalog<ThemeFile, ThemeContents, AppTheme>
        {
            BuiltIns = builtIns,
            DefaultId = Theming.DarkId,
            Build = static (themes, file, chain) => BuildChainTheme(themes, file, chain),
            FindFallbackId = static file => GetFallbackId(file),
        };
        catalog.ReplaceUserThemes([]);
        return catalog;
    }

    private static AppTheme? BuildChainTheme(ThemeCatalog<ThemeFile, ThemeContents, AppTheme> catalog, ThemeFile file, IReadOnlyList<(ThemeFile File, ThemeContents Contents)> chain)
    {
        var root = chain[0].File;
        var implicitBase = FindImplicitBase(catalog, root);
        if (implicitBase is null)
        {
            return null;
        }

        var merged = new MergedTheme();
        AddCodeAndIcons(merged, implicitBase);
        foreach (var link in chain)
        {
            AddContents(merged, link.Contents);
        }

        var errors = new List<ThemeError>();
        var theme = BuildTheme(merged, file.File, errors);
        catalog.AddErrors(errors);
        return theme;
    }

    private static ThemeContents? FindImplicitBase(ThemeCatalog<ThemeFile, ThemeContents, AppTheme> catalog, ThemeFile root)
    {
        if (root.Type is null)
        {
            catalog.AddError(new ThemeError { File = root.File, Line = 0, Problem = ThemeProblem.NoType });
            return null;
        }
        return catalog.GetBuiltInContents(GetIdOfType(root.Type.Value));
    }

    private static ThemeId<ThemeFile> GetFallbackId(ThemeFile file)
    {
        var type = file.Type ?? ThemeType.Dark;
        return GetIdOfType(type);
    }

    private static ThemeId<ThemeFile> GetIdOfType(ThemeType type)
    {
        return type == ThemeType.Light ? Theming.LightId : Theming.DarkId;
    }

    private static ThemeFile CreateFile(ThemeId<ThemeFile> id, string file)
    {
        return new ThemeFile { Id = id, Json = new ThemeJson { File = file }, Name = new ThemeName.Plain(id.Text) };
    }

    private const string UiSection = "ui";
    private const string CodeSection = "code";
    private const string IconsSection = "icons";
    private const string KeySeparator = ", ";

    // The bottom layer, under all that is added later.
    private static void AddCodeAndIcons(MergedTheme merged, ThemeContents contents)
    {
        StackValues(merged.Code, contents.Code);
        StackValues(merged.Icons, contents.Icons);
    }

    private static void AddContents(MergedTheme merged, ThemeContents contents)
    {
        foreach (var (name, value) in contents.Palette)
        {
            merged.Palette[name] = value;
        }

        StackValues(merged.Ui, contents.Ui);
        StackValues(merged.Code, contents.Code);
        StackValues(merged.Icons, contents.Icons);
    }

    private static void StackValues(Dictionary<string, List<ThemeValue>> merged, IReadOnlyDictionary<string, ThemeValue> added)
    {
        foreach (var (key, value) in added)
        {
            EnsureLayers(merged, key).Add(value);
        }
    }

    private static List<ThemeValue> EnsureLayers(Dictionary<string, List<ThemeValue>> merged, string key)
    {
        if (merged.TryGetValue(key, out var layers))
        {
            return layers;
        }

        layers = new List<ThemeValue>();
        merged[key] = layers;
        return layers;
    }

    private static AppTheme? BuildTheme(MergedTheme merged, string file, ICollection<ThemeError> errors)
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
            errors.Add(new ThemeError
            {
                File = file,
                Line = 0,
                Problem = ThemeProblem.MissingColors,
                Detail = string.Join(KeySeparator, missing),
            });
            return null;
        }
        return Theming.CreateTheme(UiTheme.FromColors(ui), CodeViewPalette.FromColors(code), NodeIconPalette.FromColors(icons));
    }

    private static Dictionary<string, uint> LookUpColors(MergedTheme merged, string section, Dictionary<string, List<ThemeValue>> values, ICollection<ThemeError> errors)
    {
        var colors = new Dictionary<string, uint>();
        foreach (var (key, layers) in values)
        {
            LookUpTopColor(merged, section, key, layers, colors, errors);
        }

        return colors;
    }

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
            errors.Add(new ThemeError
            {
                File = value.File,
                Line = value.Line,
                Problem = ThemeProblem.UnknownPaletteName,
                Subject = section + "." + key,
                Detail = value.Text,
            });
        }
    }

    private static bool TryLookUp(MergedTheme merged, ThemeValue value, out uint color)
    {
        if (!value.IsPaletteName)
        {
            return UiColor.TryParse(value.Text, out color);
        }

        if (merged.Palette.TryGetValue(value.Text, out var named))
        {
            return UiColor.TryParse(named.Text, out color);
        }

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
        {
            merged.Fonts[id] = font;
        }
    }

    /// <summary>Takes everything <paramref name="contents"/> sets, over what was added before.</summary>
    internal static void AddContents(MergedIconTheme merged, IconThemeContents contents)
    {
        AddFonts(merged, contents);
        foreach (var (key, value) in contents.Icons)
        {
            EnsureLayers(merged, key).Add(value);
        }
    }

    private static List<IconValue> EnsureLayers(MergedIconTheme merged, string key)
    {
        if (merged.Icons.TryGetValue(key, out var layers))
        {
            return layers;
        }

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
        {
            TakeTopIcon(merged, key, layers, glyphs, values, errors);
        }

        if (fallback is null)
        {
            return CreateIconTheme(new IconSet(glyphs), ListFaces(merged, null), values);
        }

        foreach (var (key, value) in fallback.Values)
        {
            values.TryAdd(key, value);
        }

        return CreateIconTheme(new IconSet(glyphs, fallback.Set), ListFaces(merged, fallback), values);
    }

    private static IconTheme CreateIconTheme(IconSet set, IReadOnlyList<FontFace> fonts, Dictionary<string, IconValue> values)
    {
        return new IconTheme { Icons = new AppIcons(set), Set = set, Fonts = fonts, Values = values };
    }

    private static void TakeTopIcon(MergedIconTheme merged, string key, List<IconValue> layers, Dictionary<string, IconGlyph> glyphs, Dictionary<string, IconValue> values,
        ICollection<ThemeError> errors)
    {
        for (var i = layers.Count - 1; i >= 0; i--)
        {
            var value = layers[i];
            var glyph = MakeGlyph(merged, key, value, errors);
            if (glyph is null)
            {
                continue;
            }

            glyphs[key] = glyph.Value;
            values[key] = value;
            return;
        }
    }

    private static IconGlyph? MakeGlyph(MergedIconTheme merged, string key, IconValue value, ICollection<ThemeError> errors)
    {
        return value.Match((merged, key, errors),
            glyph: static (s, glyph) => MakeFontGlyph(s.merged, s.key, glyph, s.errors),
            picture: static (_, picture) => IconGlyph.FromSvg(picture.Svg));
    }

    private static IconGlyph? MakeFontGlyph(MergedIconTheme merged, string key, IconValue.Glyph glyph, ICollection<ThemeError> errors)
    {
        if (!merged.Fonts.TryGetValue(glyph.Font, out var font))
        {
            errors.Add(new ThemeError
            {
                File = glyph.File,
                Line = glyph.Line,
                Problem = ThemeProblem.UnknownFont,
                Subject = IconsPrefix + key,
                Detail = glyph.Font,
            });
            return null;
        }
        if (font is not IconFont.Loaded loaded)
        {
            return null;
        }

        if (!loaded.Characters.Contains(glyph.Codepoint))
        {
            var codepoint = glyph.Codepoint.ToString(CodepointFormat, CultureInfo.InvariantCulture);
            errors.Add(new ThemeError
            {
                File = glyph.File,
                Line = glyph.Line,
                Problem = ThemeProblem.MissingGlyph,
                Subject = IconsPrefix + key,
                Detail = codepoint,
            });
            return null;
        }
        var text = char.ConvertFromUtf32(glyph.Codepoint);
        return IconGlyph.FromFont(text, loaded.Face.Family);
    }

    private static List<FontFace> ListFaces(MergedIconTheme merged, IconTheme? fallback)
    {
        var faces = merged.Fonts.Values
            .OfType<IconFont.Loaded>()
            .Select(static f => f.Face)
            .ToList();
        if (fallback is null)
        {
            return faces;
        }

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

    /// <summary>Reads <paramref name="bytes"/>, which must hold one object, handing each of its keys to
    /// <paramref name="readKey"/>.</summary>
    internal static void ReadJson(ThemeJson json, byte[] bytes, TopLevelKeyReader readKey)
    {
        var content = SkipBom(bytes);
        json.IndexLines(content);
        var reader = new Utf8JsonReader(content, Options);
        try
        {
            ReadJsonRoot(json, ref reader, readKey);
        }
        catch (JsonException e)
        {
            var lineFromZero = e.LineNumber ?? 0;
            json.MarkUnreadable((int)lineFromZero + 1, ThemeProblem.InvalidJson, e.Message);
        }
        catch (InvalidOperationException e)
        {
            json.MarkUnreadable(GetLine(json, reader), ThemeProblem.InvalidJson, e.Message);
        }
    }

    private static ReadOnlySpan<byte> SkipBom(byte[] json)
    {
        var content = json.AsSpan();
        if (content.StartsWith(Utf8Bom))
        {
            return content.Slice(Utf8Bom.Length);
        }

        return content;
    }

    private static void ReadJsonRoot(ThemeJson json, ref Utf8JsonReader reader, TopLevelKeyReader readKey)
    {
        reader.Read();
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            json.MarkUnreadable(GetLine(json, reader), ThemeProblem.RootNotAnObject);
            return;
        }
        while (TryReadJsonKey(json, ref reader, out var key, out var line))
        {
            readKey(ref reader, key, line);
        }

        reader.Read();
    }

    /// <summary>Moves past the next key of the object the reader is in, onto its value. False at the
    /// object's end.</summary>
    internal static bool TryReadJsonKey(ThemeJson json, ref Utf8JsonReader reader, out string key, out int line)
    {
        reader.Read();
        key = "";
        line = 0;
        if (reader.TokenType == JsonTokenType.EndObject)
        {
            return false;
        }

        line = GetLine(json, reader);
        var name = reader.GetString();
        if (name is null)
        {
            json.AddError(line, ThemeProblem.InvalidJson, detail: "A key is null.");
        }
        else
        {
            key = name;
        }

        reader.Read();
        return true;
    }

    /// <summary>The name the reader is on: a string, or an object of names by language tag. Null after
    /// reporting that it is neither.</summary>
    internal static ThemeName? ReadJsonName(ThemeJson json, ref Utf8JsonReader reader, int line, string fallback)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var plain = reader.GetString();
            if (plain is null)
            {
                json.AddError(line, ThemeProblem.InvalidName);
                return null;
            }
            return new ThemeName.Plain(plain);
        }
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            json.AddError(line, ThemeProblem.InvalidName);
            reader.Skip();
            return null;
        }
        var byTag = ReadNamesByTag(json, ref reader);
        return new ThemeName.PerLanguage(byTag, fallback);
    }

    private static Dictionary<string, string> ReadNamesByTag(ThemeJson json, ref Utf8JsonReader reader)
    {
        var byTag = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (TryReadJsonKey(json, ref reader, out var tag, out var line))
        {
            var key = ThemeJson.NameKey + "." + tag;
            var name = ReadJsonString(json, ref reader, key, line);
            if (name is null)
            {
                continue;
            }

            if (!byTag.TryAdd(tag, name))
            {
                json.AddError(line, ThemeProblem.DuplicateLanguage, key);
            }
        }
        return byTag;
    }

    /// <summary>True when the reader is on an object; else skips the value after reporting that it is not one.</summary>
    internal static bool IsJsonObject(ThemeJson json, ref Utf8JsonReader reader, string key, int line)
    {
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            return true;
        }

        json.AddError(line, ThemeProblem.NotAnObject, key);
        reader.Skip();
        return false;
    }

    /// <summary>The string the reader is on, or null after reporting that it is something else.</summary>
    internal static string? ReadJsonString(ThemeJson json, ref Utf8JsonReader reader, string key, int line)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString();
        }

        json.AddError(line, ThemeProblem.NotAString, key);
        reader.Skip();
        return null;
    }

    /// <summary>Skips the value of <paramref name="key"/> after reporting that no theme File takes it.</summary>
    internal static void SkipUnknownKey(ThemeJson json, ref Utf8JsonReader reader, string key, int line)
    {
        json.AddError(line, ThemeProblem.UnknownKey, key);
        reader.Skip();
    }

    private static int GetLine(ThemeJson json, Utf8JsonReader reader)
    {
        return json.GetLineAt(reader.TokenStartIndex);
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

    /// <summary>The theme <paramref name="id"/> of <paramref name="file"/>, which could not be read for
    /// <paramref name="reason"/>: it has no <see cref="ThemeFile.Contents"/>.</summary>
    public static ThemeFile CreateUnreadableTheme(ThemeId<ThemeFile> id, string file, string reason)
    {
        var theme = CreateFile(id, file);
        theme.Json.MarkUnreadable(0, ThemeProblem.Unreadable, reason);
        return theme;
    }

    /// <summary>Reads the theme <paramref name="id"/> from <paramref name="json"/>, the content of
    /// <paramref name="file"/>.</summary>
    public static ThemeFile ReadTheme(ThemeId<ThemeFile> id, string file, byte[] json)
    {
        var theme = new ParsedTheme { Id = id, Json = new ThemeJson { File = file }, Name = new ThemeName.Plain(id.Text) };
        Theming.ReadJson(theme.Json, json, (ref Utf8JsonReader r, string k, int l) => ReadThemeKey(theme, ref r, k, l));
        var contents = theme.Json.IsUnreadable ? null : new ThemeContents
        {
            Palette = theme.Palette,
            Ui = theme.Ui,
            Code = theme.Code,
            Icons = theme.Icons,
        };
        return new ThemeFile
        {
            Id = id,
            Json = theme.Json,
            Name = theme.Name,
            Type = theme.Type,
            Extends = theme.Extends,
            Contents = contents,
        };
    }

    private static void ReadThemeKey(ParsedTheme theme, ref Utf8JsonReader reader, string key, int line)
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

    private static void ReadThemeName(ParsedTheme theme, ref Utf8JsonReader reader, int line)
    {
        var name = Theming.ReadJsonName(theme.Json, ref reader, line, theme.Id.Text);
        if (name is not null)
        {
            theme.Name = name;
        }
    }

    private static void ReadType(ParsedTheme theme, ref Utf8JsonReader reader, int line)
    {
        var type = Theming.ReadJsonString(theme.Json, ref reader, TypeKey, line);
        if (type is null)
        {
            return;
        }

        theme.Type = ParseThemeType(type);
        if (theme.Type is null)
        {
            theme.Json.AddError(line, ThemeProblem.UnknownType, type);
        }
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

    private static void ReadExtends(ParsedTheme theme, ref Utf8JsonReader reader, int line)
    {
        var parent = Theming.ReadJsonString(theme.Json, ref reader, ThemeJson.ExtendsKey, line);
        if (parent is null)
        {
            return;
        }

        var id = ThemeId<ThemeFile>.Parse(parent);
        if (id is null)
        {
            theme.Json.AddError(line, ThemeProblem.UnknownParent, parent);
            return;
        }
        theme.Extends = new ThemeParent<ThemeFile> { Id = id, Line = line };
    }

    // A palette's colors must be written out, not named.
    private static void ReadPalette(ParsedTheme theme, ref Utf8JsonReader reader, int line)
    {
        if (!Theming.IsJsonObject(theme.Json, ref reader, PaletteKey, line))
        {
            return;
        }

        while (Theming.TryReadJsonKey(theme.Json, ref reader, out var name, out var nameLine))
        {
            var key = PaletteKey + "." + name;
            var color = Theming.ReadJsonString(theme.Json, ref reader, key, nameLine);
            if (color is null)
            {
                continue;
            }

            if (!IsValidColor(theme, key, color, nameLine))
            {
                continue;
            }

            theme.Palette[name] = new ThemeValue { Text = color, File = theme.File, Line = nameLine };
        }
    }

    private static void ReadColors(ParsedTheme theme, ref Utf8JsonReader reader, string section, int line, Dictionary<string, ThemeValue> colors, HashSet<string> keys)
    {
        if (!Theming.IsJsonObject(theme.Json, ref reader, section, line))
        {
            return;
        }

        while (Theming.TryReadJsonKey(theme.Json, ref reader, out var name, out var nameLine))
        {
            ReadColor(theme, ref reader, section, name, nameLine, colors, keys);
        }
    }

    private static void ReadColor(ParsedTheme theme, ref Utf8JsonReader reader, string section, string name, int line, Dictionary<string, ThemeValue> colors, HashSet<string> keys)
    {
        var key = section + "." + name;
        if (!keys.Contains(name))
        {
            Theming.SkipUnknownKey(theme.Json, ref reader, key, line);
            return;
        }
        var text = Theming.ReadJsonString(theme.Json, ref reader, key, line);
        if (text is null)
        {
            return;
        }

        var value = new ThemeValue { Text = text, File = theme.File, Line = line };
        if (!value.IsPaletteName && !IsValidColor(theme, key, text, line))
        {
            return;
        }

        colors[name] = value;
    }

    private static bool IsValidColor(ParsedTheme theme, string key, string color, int line)
    {
        if (!UiColor.TryParse(color, out _))
        {
            theme.Json.AddError(line, ThemeProblem.NotAColor, key, color);
            return false;
        }
        if (color == TransparentBlack)
        {
            theme.Json.AddError(line, ThemeProblem.TransparentBlack, key);
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

    // Merged before palette names are looked up, and built once at the end, so a color a parent names takes the palette
    // color a child set, a color whose palette name is unknown keeps the one beneath it, and a role nobody set is
    // derived from the final base roles.
    private sealed class MergedTheme
    {
        public readonly Dictionary<string, ThemeValue> Palette = new();
        public readonly Dictionary<string, List<ThemeValue>> Ui = new();
        public readonly Dictionary<string, List<ThemeValue>> Code = new();
        public readonly Dictionary<string, List<ThemeValue>> Icons = new();
    }
}
