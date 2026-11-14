using ZGF.Gui.Immediate;

namespace ILSight.ILSightAppArea;

/// <summary>The port the window is brought to the front through, when a later run's arguments asked for it: a port so a
/// host other than the app's window, a test, sees each time the request crosses it.</summary>
public interface IWindowFront
{
    /// <summary>Brings the window <paramref name="ui"/> draws to the front once its frame is drawn.</summary>
    void BringToFront(Ui ui);
}
