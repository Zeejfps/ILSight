using ZGF.Gui.Immediate;
using ILSight.ILSightAppArea;

namespace ILSight.App.Tests;

/// <summary>Counts each time the window was brought to the front through its port (<see cref="Over"/>), the real
/// adapter asked too: what lasts of a request the frame that acts on it has cleared by the next.</summary>
internal sealed class FrontRecord
{
    private int _times;

    /// <summary>How many times the window was brought to the front, so far.</summary>
    public int Times => Volatile.Read(ref _times);

    /// <summary>The port the root hands the app (<see cref="AdapterHooks.BringingToFront"/>): <paramref name="real"/>,
    /// the real adapter, counted first.</summary>
    public IWindowFront Over(IWindowFront real)
    {
        return new Counting(real, this);
    }

    private sealed class Counting(IWindowFront real, FrontRecord record) : IWindowFront
    {
        public void BringToFront(Ui ui)
        {
            Interlocked.Increment(ref record._times);
            real.BringToFront(ui);
        }
    }
}
