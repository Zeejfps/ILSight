using ILSight.SettingsFormat;

namespace ILSight.Diagramming;

/// <summary>How a diagram was viewed, as a settings file can hold it.</summary>
internal sealed record DiagramViewJson(float X, float Y, float Zoom, List<CardFoldJson?>? Folds, bool ShowsHiddenMembers);
