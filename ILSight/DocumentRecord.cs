namespace ILSight;

/// <summary>An open tab as the settings keep it: the panel it was docked as, and what it showed under which
/// title.</summary>
public sealed record DocumentRecord
{
    public required string Panel { get; init; }

    public required DocumentRecordContent Content { get; init; }
}
