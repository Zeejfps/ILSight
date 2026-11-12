namespace ILSight.SettingsFormat;

/// <summary>A font to draw code in, as a settings file can hold it. Without its family or file it is
/// none, and the bundled font stands in.</summary>
internal sealed record CodeFontJson(string? Family, string? Path, int FaceIndex);
