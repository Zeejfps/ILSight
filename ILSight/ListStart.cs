using ILSight.ILSightAppArea;

namespace ILSight;

/// <summary>The start the composition root records on the state it restores, nothing read yet: the active list's
/// reopening begun as a reload (its files on their way) and the finding of its saved analyses, for the app to start
/// once it has taken the arguments already waiting (<see cref="AppFrames.StartOpeningStartupList"/>), which decide
/// from the list as on its way. Dropped, the app says it is reloading for good.</summary>
[MustUse]
public sealed class ListStart
{
    public required ListReopening Reopening { get; init; }

    public required AnalysesRestore? Analyses { get; init; }
}
