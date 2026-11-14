using ZGF.Gui.Immediate;

namespace ILSight.ILSightAppArea;

/// <summary>Brings the window to the front through the framework's window.</summary>
internal sealed class WindowFrontAdapter : IWindowFront
{
    public void BringToFront(Ui ui)
    {
        ui.BringWindowToFront();
    }
}
