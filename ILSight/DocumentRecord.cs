namespace ILSight;

/// <summary>An open tab as the settings keep it: the panel it was docked as, its title, and what it showed.</summary>
public abstract record DocumentRecord(string Panel, string? Title);
