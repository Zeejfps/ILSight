using ILSight.UserArea;

namespace ILSight;

/// <summary>One theme file as it is written, without the themes it extends: its name and type, the theme
/// it extends, its palette and its <c>ui</c>, <c>code</c> and <c>icons</c> colors by key, read as
/// <see cref="ThemeJson"/> reads, leaving out a bad key or value.</summary>
public sealed class ThemeFile : IUserThemeFile<ThemeFile, ThemeContents, AppTheme>
{
    public const char ColorPrefix = '#';

    public static ThemeKind Kind => ThemeKind.Color;

    public static string Extension => ".json";

    /// <summary>A theme extending <paramref name="parent"/> that lists every color, each commented out at the one
    /// <paramref name="shown"/> has (<see cref="ThemeDraft"/>).</summary>
    public static string ComposeDraft(string name, ThemeId<ThemeFile> parent, AppTheme shown)
    {
        return ThemeDraft.Compose(name, parent, shown);
    }

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
