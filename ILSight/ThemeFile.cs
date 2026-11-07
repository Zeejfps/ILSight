using System.Text.Json;
using ZGF.Gui.Immediate;

namespace ILSight;

/// <summary>Whether a theme is drawn dark or light. A theme that extends no other takes the code and
/// icon colors of the built-in theme of its type.</summary>
public enum ThemeType
{
    Dark,
    Light,
}

/// <summary>A color as a theme file writes it, a <c>#RRGGBB</c> or <c>#RRGGBBAA</c> color or a palette
/// name, with the file and line it is written on.</summary>
public readonly record struct ThemeValue(string Text, string File, int Line)
{
    public bool IsPaletteName => !Text.StartsWith(ThemeFile.ColorPrefix);
}

/// <summary>What a readable theme file sets: its palette, and its <c>ui</c>, <c>code</c> and <c>icons</c> colors by key.</summary>
/// <param name="Palette">The named colors the other sections can use, by name.</param>
/// <param name="Ui">The colors of <see cref="UiTheme"/>, by key.</param>
/// <param name="Code">The colors of <see cref="CodeViewPalette"/>, by key.</param>
/// <param name="Icons">The colors of <see cref="NodeIconPalette"/>, by key.</param>
public sealed record ThemeContents(
    IReadOnlyDictionary<string, ThemeValue> Palette,
    IReadOnlyDictionary<string, ThemeValue> Ui,
    IReadOnlyDictionary<string, ThemeValue> Code,
    IReadOnlyDictionary<string, ThemeValue> Icons);

/// <summary>One theme file as it is written, without the themes it extends: its name and type, the theme
/// it extends, its palette and its <c>ui</c>, <c>code</c> and <c>icons</c> colors by key, read as
/// <see cref="ThemeJson"/> reads, leaving out a bad key or value.</summary>
public sealed class ThemeFile : IThemeFile<ThemeFile, ThemeContents>
{
    public const string Extension = ".json";
    public const char ColorPrefix = '#';

    public required ThemeJson Json { get; init; }
    public readonly Dictionary<string, ThemeValue> Palette = new();
    public readonly Dictionary<string, ThemeValue> Ui = new();
    public readonly Dictionary<string, ThemeValue> Code = new();
    public readonly Dictionary<string, ThemeValue> Icons = new();

    /// <summary>The file's name without its extension, which settings and <see cref="Extends"/> know it by.</summary>
    public required ThemeId<ThemeFile> Id { get; init; }

    /// <summary>Where the file is, as errors name it.</summary>
    public string File => Json.File;

    public required ThemeName Name { get; set; }

    /// <summary>The theme's type, or null to take that of the theme it extends.</summary>
    public ThemeType? Type { get; set; }

    /// <summary>The theme this one builds on, or null.</summary>
    public ThemeParent<ThemeFile>? Extends { get; set; }

    /// <summary>The palette and colors the file sets; null when it could not be read as a JSON object.</summary>
    public ThemeContents? Contents { get; set; }

    public IReadOnlyList<ThemeError> Errors => Json.Errors;
}
