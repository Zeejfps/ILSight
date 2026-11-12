namespace ILSight.UserArea;

/// <summary>What <see cref="ThemeFolderWatch.NextChangeAsync"/> completes with; <see cref="Reason"/> says why
/// a watch was <see cref="ThemeFolderEventKind.Lost"/>.</summary>
public readonly record struct ThemeFolderEvent
{
    public required ThemeFolderEventKind Kind { get; init; }

    public string Reason { get; init; } = "";

    public ThemeFolderEvent()
    {
    }
}
