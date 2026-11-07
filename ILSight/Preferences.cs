using ILSight.Core;
using ILSight.Localization;

namespace ILSight;

/// <summary>Choosing the theme, the icon theme, the interface language and how code is decompiled. The screen decompiles
/// again what it shows when the language or the options change.</summary>
public static class Preferences
{
    public static void SetTheme(AppState appState, ThemeId<ThemeFile> theme)
    {
        appState.Theme = theme;
    }

    public static void SetIconTheme(AppState appState, ThemeId<IconThemeFile> iconTheme)
    {
        appState.IconTheme = iconTheme;
    }

    public static void SetLocale(AppState appState, Locale locale)
    {
        if (locale == appState.Locale)
            return;
        appState.Locale = locale;
    }

    public static void SetApiVisibility(AppState appState, ApiVisibility visibility)
    {
        if (visibility == appState.ApiVisibility)
            return;
        appState.ApiVisibility = visibility;
    }

    public static void SetLanguage(AppState appState, DecompilationLanguage language)
    {
        if (language == appState.Language)
            return;
        appState.Language = language;
    }

    public static void SetLanguageVersion(AppState appState, CSharpVersion version)
    {
        var options = appState.DecompilerOptions with { LanguageVersion = version };
        SetDecompilerOptions(appState, options);
    }

    public static void SetDecompilerToggle(AppState appState, DecompilerToggle toggle, bool value)
    {
        var options = DecompilerToggles.SetToggle(appState.DecompilerOptions, toggle, value);
        SetDecompilerOptions(appState, options);
    }

    public static void SetDecompilerToggles(AppState appState, IEnumerable<DecompilerToggle> toggles, bool value)
    {
        var options = DecompilerToggles.SetToggle(appState.DecompilerOptions, toggles, value);
        SetDecompilerOptions(appState, options);
    }

    /// <summary>Turns every toggle the options panel shows back to its default, keeping the language
    /// version and how projects are exported.</summary>
    public static void ResetDecompilerToggles(AppState appState)
    {
        var options = DecompilerToggles.ResetCodeToggles(appState.DecompilerOptions);
        SetDecompilerOptions(appState, options);
    }

    private static void SetDecompilerOptions(AppState appState, DecompilerOptions options)
    {
        if (options == appState.DecompilerOptions)
            return;
        appState.DecompilerOptions = options;
    }
}
