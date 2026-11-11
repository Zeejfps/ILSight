using System.Globalization;
using System.Text;
using System.Text.Json;
using ZGF.Fonts;
using ZGF.Gui.Immediate;
using ZGF.Svg;

namespace ILSight;

/// <summary>A font an icon theme file declares by the path it names: loaded, or broken when it could not
/// be read or loaded, which was reported.</summary>
public abstract record IconFont
{
    private IconFont(string path)
    {
        Path = path;
    }

    public string Path { get; }

    /// <summary>The face read from the font and the characters it has glyphs for.</summary>
    public sealed record Loaded(string Path, FontFace Face, FontCharacters Characters) : IconFont(Path);

    public sealed record Broken(string Path) : IconFont(Path);
}

/// <summary>An icon as an icon theme file writes it, with the file and line it is written on: a glyph of
/// a font, or a picture read from an SVG file.</summary>
public abstract record IconValue
{
    private IconValue(string file, int line)
    {
        File = file;
        Line = line;
    }

    public string File { get; }

    public int Line { get; }

    /// <summary>The codepoint <see cref="Codepoint"/> of the font the theme declares as <see cref="Font"/>.</summary>
    public sealed record Glyph(string Font, int Codepoint, string File, int Line) : IconValue(File, Line);

    /// <summary>The picture <see cref="Svg"/> read from the file at <see cref="SvgPath"/>.</summary>
    public sealed record Picture(string SvgPath, SvgDocument Svg, string File, int Line) : IconValue(File, Line);
}

/// <summary>What a readable icon theme file sets.</summary>
/// <param name="Fonts">The fonts the icons can be drawn in, by id.</param>
/// <param name="Icons">The icons, by key.</param>
public sealed record IconThemeContents(IReadOnlyDictionary<string, IconFont> Fonts, IReadOnlyDictionary<string, IconValue> Icons);

/// <summary>One icon theme file as it is written, without the themes it extends: its name, the theme it
/// extends, its fonts by id and its icons by key, its fonts and pictures read as it is. Read as
/// <see cref="ThemeJson"/> reads, leaving out a bad key, value, font or picture.</summary>
public sealed class IconThemeFile : IThemeFile<IconThemeFile, IconThemeContents>
{
    public static ThemeKind Kind => ThemeKind.Icon;

    public static string Extension => ".json";

    public required ThemeJson Json { get; init; }
    public required IconAssets Assets { get; init; }
    public readonly Dictionary<string, IconFont> Fonts = new();
    public readonly Dictionary<string, IconValue> Icons = new();

    public required ThemeId<IconThemeFile> Id { get; init; }

    public string File => Json.File;

    public required ThemeName Name { get; set; }

    public ThemeParent<IconThemeFile>? Extends { get; set; }

    /// <summary>The fonts and icons the file sets; null when it could not be read as a JSON object.</summary>
    public IconThemeContents? Contents { get; set; }

    public IReadOnlyList<ThemeError> Errors => Json.Errors;

    /// <summary>The fields of an icon as written, each null when left out.</summary>
    public readonly record struct IconFields(string? Font, string? Glyph, string? Svg);
}
