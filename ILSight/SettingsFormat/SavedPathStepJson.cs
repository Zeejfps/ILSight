namespace ILSight.SettingsFormat;

/// <summary>A step of a place as a settings or bookmarks file can hold it. Without a segment that reads,
/// or a label, it is none.</summary>
internal sealed record SavedPathStepJson(string? Step, string? Label);
