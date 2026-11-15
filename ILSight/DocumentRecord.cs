namespace ILSight;

/// <summary>An open tab as the settings keep it: the panel it was docked as, and what it showed under which
/// title.</summary>
public abstract record DocumentRecord
{
    public required string Panel { get; init; }
}
