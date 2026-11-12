namespace ILSight;

/// <summary>What a tab showed, named wherever several tabs' content meets, as the one history every tab goes back
/// and forward through: the tab's id, never handed out again (<see cref="DocumentTabs.AddNew"/>), with the
/// <see cref="DocumentTab.Version"/> it showed. Not the version alone, which two tabs count alike, nor the tab
/// itself, which would keep a closed tab alive.</summary>
public sealed record TabVersion
{
    public required int TabId { get; init; }

    public required int Version { get; init; }

    /// <summary>What <paramref name="tab"/> shows now.</summary>
    public static TabVersion Of(DocumentTab tab)
    {
        return new TabVersion { TabId = tab.Id, Version = tab.Version };
    }

    /// <summary>Whether this is what <paramref name="tab"/> shows now. Allocates nothing.</summary>
    public bool IsShownBy(DocumentTab tab)
    {
        if (TabId != tab.Id)
        {
            return false;
        }

        return Version == tab.Version;
    }
}
