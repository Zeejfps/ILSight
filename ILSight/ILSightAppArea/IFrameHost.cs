namespace ILSight.ILSightAppArea;

/// <summary>What runs a frame of the app loop (<see cref="AppLoop"/>): the time the frame runs at, and another frame asked
/// of it. The window's frames (<see cref="WindowFrameHost"/>) and a test host's without a window both run them.</summary>
public interface IFrameHost
{
    /// <summary>The time this frame runs at.</summary>
    TimeSpan Now { get; }

    /// <summary>Asks for another frame as soon as this one is done.</summary>
    void RequestFrame();

    /// <summary>Asks for another frame once <paramref name="wait"/> has gone by.</summary>
    void RequestFrameIn(TimeSpan wait);
}
