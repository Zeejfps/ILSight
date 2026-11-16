using ZGF.Gui.Immediate;

namespace ILSight.ILSightAppArea;

/// <summary>The window's frames, as the app loop runs in them: the time and the frames asked of the framework's
/// <see cref="Ui"/> the window draws the current frame with.</summary>
internal sealed class WindowFrameHost : IFrameHost
{
    private Ui? _ui;

    public TimeSpan Now => TimeSpan.FromSeconds(Current.RealTime);

    private Ui Current => _ui ?? throw new InvalidOperationException("The window runs no frame yet.");

    /// <summary>Runs what the loop asks of a frame through <paramref name="ui"/>, the one the window draws this frame
    /// with.</summary>
    public void Enter(Ui ui)
    {
        _ui = ui;
    }

    public void RequestFrame()
    {
        Current.RequestUpdate();
    }

    public void RequestFrameIn(TimeSpan wait)
    {
        Current.RequestUpdateIn(wait.TotalSeconds);
    }
}
