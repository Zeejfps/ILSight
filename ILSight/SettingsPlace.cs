using ILSight.Core;

namespace ILSight;

/// <summary>Where the settings are written, and the user's themes and icon themes are found beside
/// them; <paramref name="FilePath"/> is null while the settings are kept in memory only.</summary>
public sealed record SettingsPlace(FullFilePath? FilePath)
{
    private const string ThemesFolderName = "themes";
    private const string IconThemesFolderName = "icon-themes";

    /// <summary>Where the user's themes are; null while the settings are kept in memory only.</summary>
    public string? ThemesFolder { get; } = Settings.FindFolderBeside(FilePath, ThemesFolderName);

    /// <summary>Where the user's icon themes are; null while the settings are kept in memory only.</summary>
    public string? IconThemesFolder { get; } = Settings.FindFolderBeside(FilePath, IconThemesFolderName);
}
