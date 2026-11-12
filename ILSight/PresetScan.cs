using ILSight.Core;

namespace ILSight;

/// <summary>The ready-made lists this machine offers, as the lists dialog's preset menu shows them. Looking for them
/// reads the installed frameworks' folders, which can take longer than a frame: the menu, opened the first time, asks
/// for it (<see cref="AskForPresets"/>), the frame edge right after that draw starts it (<see cref="TryStart"/>), and the
/// menu says they are being looked for until they come (<see cref="Take"/>), kept from then on.</summary>
public sealed class PresetScan
{
    private IReadOnlyList<AssemblyListPreset>? _found;
    private bool _isAsked;
    private bool _isStarted;

    /// <summary>The presets found; null until they come.</summary>
    public IReadOnlyList<AssemblyListPreset>? Found => _found;

    /// <summary>Asks for the presets to be looked for, unless they are found or being looked for.</summary>
    public void AskForPresets()
    {
        if (_found is not null)
        {
            return;
        }

        if (_isStarted)
        {
            return;
        }

        _isAsked = true;
    }

    /// <summary>Takes the asked-for look as started; true once per ask.</summary>
    public bool TryStart()
    {
        if (!_isAsked)
        {
            return false;
        }

        _isAsked = false;
        _isStarted = true;
        return true;
    }

    /// <summary>Takes the presets found, which the menu shows from then on.</summary>
    public void Take(IReadOnlyList<AssemblyListPreset> presets)
    {
        _found = presets;
        _isStarted = false;
    }
}
