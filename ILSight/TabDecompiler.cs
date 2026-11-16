using ILSight.CommandArea;

namespace ILSight;

/// <summary>Decompiles what the open tabs show again when the language or the decompiler's options change
/// (<see cref="Decompilation"/>).</summary>
internal sealed class TabDecompiler
{
    private readonly AppState _appState;

    /// <param name="appState">The app state whose decompiler's language and options the tabs are decompiled in.</param>
    internal TabDecompiler(AppState appState)
    {
        _appState = appState;
    }

    /// <summary>When the language or the options changed since <paramref name="shown"/> last took them, starts
    /// decompiling again each tab in front in <paramref name="panels"/> that showed what it shows otherwise.</summary>
    public void DecompileOpenTabsAgainWhenSettingsChange(ShownChanges shown, Panels panels)
    {
        var setup = _appState.Decompiler.Setup;
        var language = setup.Language;
        if (!shown.TryTakeDecompilation(language, setup.Options))
        {
            return;
        }

        foreach (var tab in panels.ListFrontTabs())
        {
            Commands.StartDecompiling(_appState, Decompilation.StartDecompilingAgainIfOutdated(tab, language, setup.Options));
        }
    }
}
