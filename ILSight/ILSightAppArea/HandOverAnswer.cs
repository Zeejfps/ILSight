namespace ILSight.ILSightAppArea;

/// <summary>What a hand-over of a run's arguments to the run holding the role came to, as far as the sending run can
/// tell (<see cref="RunRouting.IPorts.SendAsync"/>).</summary>
public enum HandOverAnswer
{
    /// <summary>The holder acknowledged it took them.</summary>
    Taken,

    /// <summary>They never reached the holder (none could be reached), or it said it refused them.</summary>
    NotTaken,

    /// <summary>They were sent, and no answer came back: the holder may or may not have taken them.</summary>
    Unknown,
}
