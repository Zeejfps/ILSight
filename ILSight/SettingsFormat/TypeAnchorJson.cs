namespace ILSight.SettingsFormat;

/// <summary>A type as a settings file can hold it. Without its assembly or name it is none.</summary>
internal sealed record TypeAnchorJson(string? AssemblyPath, string? FullName);
