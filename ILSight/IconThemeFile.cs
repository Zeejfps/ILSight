using ILSight.UserArea;

namespace ILSight;

/// <summary>One icon theme file as it is written, without the themes it extends: its name, the theme it
/// extends, its fonts by id and its icons by key, its fonts and pictures read as it is. Read as
/// <see cref="ThemeJson"/> reads, leaving out a bad key, value, font or picture.</summary>
public sealed class IconThemeFile : IUserThemeFile<IconThemeFile, IconThemeContents, IconTheme>
{
    public static ThemeKind Kind => ThemeKind.Icon;

    public static string Extension => ".json";

    /// <summary>An icon theme extending <paramref name="parent"/> that lists every icon key, each commented out as
    /// <paramref name="shown"/> draws it (<see cref="IconThemeDraft"/>).</summary>
    public static string ComposeDraft(string name, ThemeId<IconThemeFile> parent, IconTheme shown)
    {
        return IconThemeDraft.Compose(name, parent, shown);
    }

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
}
