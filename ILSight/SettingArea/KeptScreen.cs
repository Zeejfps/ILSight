using ZGF.Desktop;

namespace ILSight.SettingArea;

/// <summary>What the settings keep of the screen and of the main window, as the screen last handed them over, or as
/// the settings held them until it does.</summary>
public sealed class KeptScreen
{
    private ViewSettings? _view;
    private WindowBounds? _window;

    public required ViewSettings View
    {
        get => _view ?? throw new InvalidOperationException("What the settings keep of the screen is made with the view settings.");
        init => _view = value;
    }

    /// <summary>The main window as it was left: as the settings held it until the app closes.</summary>
    public WindowBounds? Window
    {
        get => _window;
        init => _window = value;
    }

    /// <summary>Counts each time the screen hands over a change to <see cref="View"/>.</summary>
    public int ViewChanges { get; private set; }

    public void KeepView(ViewSettings view)
    {
        _view = view;
        ViewChanges++;
    }

    public void KeepWindow(WindowBounds window)
    {
        _window = SettingsJson.SanitizeWindow(window);
    }
}
