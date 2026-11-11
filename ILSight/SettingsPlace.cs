using ILSight.Core;

namespace ILSight;

/// <summary>Where the settings live, whether they may be written there, and where the user's themes and
/// icon themes are found beside them.</summary>
public sealed record SettingsPlace
{
    private const string ThemesFolderName = "themes";
    private const string IconThemesFolderName = "icon-themes";

    /// <summary>The settings file; null while the settings are kept in memory only.</summary>
    public required FullFilePath? FilePath
    {
        get;
        init
        {
            field = value;
            ThemesFolder = Settings.FindFolderBeside(value, ThemesFolderName);
            IconThemesFolder = Settings.FindFolderBeside(value, IconThemesFolderName);
        }
    }

    /// <summary>Whether saving writes over <see cref="FilePath"/>; false when the file there could be
    /// neither read whole nor copied aside.</summary>
    public required bool CanSaveOverFile { get; init; }

    /// <summary>Where the user's themes are; null while the settings are kept in memory only.</summary>
    public string? ThemesFolder { get; private init; }

    /// <summary>Where the user's icon themes are; null while the settings are kept in memory only.</summary>
    public string? IconThemesFolder { get; private init; }
}
