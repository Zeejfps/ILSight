namespace ILSight.SettingsFormat;

/// <summary>Where a metadata grid stood, as a settings file can hold it.</summary>
internal readonly record struct GridPositionJson(int TopRow, float ScrollX, int? Row, int? Column);
