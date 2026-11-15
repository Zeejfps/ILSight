namespace ILSight;

/// <summary>What ended a reload that gave a tab no answer, read from the list's state when it ended: no lookup ran for
/// the tab's place, so it is never said gone (<see cref="TabContent.NotReloaded"/>).</summary>
public enum ReloadEnd
{
    /// <summary>The tab's assembly is no longer listed: Close All, or the list cleared.</summary>
    AssemblyClosed,

    /// <summary>The reload was stopped with the tab's assembly still listed; the member may still be in the file.</summary>
    Stopped,
}
