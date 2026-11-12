using ILSight.Core;

namespace ILSight;

/// <summary>Choosing how C# is decompiled, each choice computing new options from those chosen before. The screen
/// decompiles again what it shows when the language or the options change. The theme, the icon theme, the interface
/// language, what API shows and the decompilation language are chosen by assigning them on the app state.</summary>
public static class Preferences
{
    public static void SetLanguageVersion(AppState appState, CSharpVersion version)
    {
        var options = appState.Decompiler.Setup.Options with { LanguageVersion = version };
        SetDecompilerOptions(appState, options);
    }

    public static void SetDecompilerToggle(AppState appState, DecompilerToggle toggle, bool value)
    {
        var options = DecompilerToggles.SetToggle(appState.Decompiler.Setup.Options, toggle, value);
        SetDecompilerOptions(appState, options);
    }

    public static void SetDecompilerToggles(AppState appState, IEnumerable<DecompilerToggle> toggles, bool value)
    {
        var options = DecompilerToggles.SetToggle(appState.Decompiler.Setup.Options, toggles, value);
        SetDecompilerOptions(appState, options);
    }

    /// <summary>Turns every toggle the options panel shows back to its default, keeping the language
    /// version and how projects are exported.</summary>
    public static void ResetDecompilerToggles(AppState appState)
    {
        var options = DecompilerToggles.ResetCodeToggles(appState.Decompiler.Setup.Options);
        SetDecompilerOptions(appState, options);
    }

    /// <summary>Keeps the options chosen before when <paramref name="options"/> equal them: the screen takes new
    /// options by reference (<see cref="ShownChanges.TryTakeDecompilation"/>), so equal new ones would count as a change.</summary>
    private static void SetDecompilerOptions(AppState appState, DecompilerOptions options)
    {
        if (options == appState.Decompiler.Setup.Options)
        {
            return;
        }

        appState.Decompiler.UseOptions(options);
    }
}
