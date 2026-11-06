using ILSight.Core;
using ILSight.Localization;

namespace ILSight;

/// <summary>What the settings are saved from: the versions of the open assemblies, the named lists, the bookmarks,
/// the analyzer and the open tabs, what the screen handed over of its view state, and the preferences themselves.</summary>
public readonly record struct SavedSources(
    int Assemblies,
    int Lists,
    int Bookmarks,
    int Analyzer,
    int Documents,
    int View,
    ThemeId<ThemeFile> Theme,
    ThemeId<IconThemeFile> IconTheme,
    DecompilationLanguage Language,
    DecompilerOptions DecompilerOptions,
    ApiVisibility ApiVisibility,
    Locale Locale,
    bool CheckForUpdates);
