using ILSight.Core;

namespace ILSight;

/// <summary>What the settings are saved from: the versions of the open assemblies, the named lists, the bookmarks,
/// the analyzer and the open tabs, what the screen handed over of its view state, and the preferences themselves.</summary>
public readonly record struct SavedSources
{
    public required int Assemblies { get; init; }

    public required int Lists { get; init; }

    public required int Bookmarks { get; init; }

    public required int Analyzer { get; init; }

    public required int Documents { get; init; }

    public required int View { get; init; }

    public required ThemeId<ThemeFile> Theme { get; init; }

    public required ThemeId<IconThemeFile> IconTheme { get; init; }

    public required DecompilationLanguage Language { get; init; }

    public required DecompilerOptions DecompilerOptions { get; init; }

    public required ApiVisibility ApiVisibility { get; init; }

    public required Locale Locale { get; init; }

    public required bool CheckForUpdates { get; init; }
}
