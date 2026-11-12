namespace ILSight.SettingsFormat;

/// <summary>Where a window sat and how big it was, as a settings file can hold it.</summary>
internal sealed record WindowBoundsJson(int X, int Y, int Width, int Height, bool Maximized);
