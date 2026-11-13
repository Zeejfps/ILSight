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

    /// <summary>What the file says (<see cref="IconTheming.Parse"/>): its icons drawn in fonts, and the fonts and
    /// pictures it names, to be read.</summary>
    public static IconThemeFile Parse(ThemeId<IconThemeFile> id, string file, byte[] json)
    {
        return IconTheming.Parse(id, file, json);
    }

    /// <summary><paramref name="parsed"/> with the fonts and pictures it names loaded from what reading them came to
    /// (<see cref="IconTheming.Complete"/>).</summary>
    public static IconThemeFile Complete(IconThemeFile parsed, IReadOnlyDictionary<string, IconAsset> named)
    {
        return IconTheming.Complete(parsed, named);
    }

    public static IconThemeFile CreateUnreadable(ThemeId<IconThemeFile> id, string file, string reason)
    {
        return IconTheming.CreateUnreadableTheme(id, file, reason);
    }

    public required ThemeJson Json { get; init; }
    public IReadOnlyDictionary<string, IconFont> Fonts { get; init; } = new Dictionary<string, IconFont>();

    public IReadOnlyDictionary<string, IconValue> Icons { get; init; } = new Dictionary<string, IconValue>();

    /// <summary>The fonts the file names by id, to be read and loaded as <see cref="Fonts"/>.</summary>
    public IReadOnlyDictionary<string, ThemeNamedFile> NamedFonts { get; init; } = new Dictionary<string, ThemeNamedFile>();

    /// <summary>The pictures the file names by icon key, to be read and loaded as <see cref="Icons"/>.</summary>
    public IReadOnlyDictionary<string, ThemeNamedFile> NamedPictures { get; init; } = new Dictionary<string, ThemeNamedFile>();

    public IReadOnlyList<ThemeNamedFile> NamedFiles => [.. NamedFonts.Values, .. NamedPictures.Values];

    public required ThemeId<IconThemeFile> Id { get; init; }

    public string File => Json.File;

    public required ThemeName Name { get; init; }

    public ThemeParent<IconThemeFile>? Extends { get; init; }

    /// <summary>The fonts and icons the file sets; null when it could not be read as a JSON object.</summary>
    public IconThemeContents? Contents { get; init; }

    public IReadOnlyList<ThemeError> Errors => Json.Errors;
}
