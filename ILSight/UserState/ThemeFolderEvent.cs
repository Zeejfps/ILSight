namespace ILSight.UserState;

/// <summary>What <see cref="ThemeFolderWatch.NextChangeAsync"/> completes with; <see cref="Reason"/> says why
/// a watch was <see cref="ThemeFolderEventKind.Lost"/>.</summary>
public readonly record struct ThemeFolderEvent(ThemeFolderEventKind Kind, string Reason = "");
