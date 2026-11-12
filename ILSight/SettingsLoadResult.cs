namespace ILSight;

/// <summary>What reading the settings file gave: the settings, with defaults for what the file could not
/// give, and what was wrong with it.</summary>
public sealed record SettingsLoadResult(AppSettings Settings, SettingsProblem Problem);
