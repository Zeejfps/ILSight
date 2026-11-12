namespace ILSight;

/// <summary>An open tab as the settings keep it: the panel it was docked as, its title, and what it showed.</summary>
public abstract record DocumentRecord
{
    public required string Panel { get; init; }

    public required string? Title { get; init; }
}
