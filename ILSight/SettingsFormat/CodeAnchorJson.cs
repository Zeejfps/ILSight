using ILSight.Core;

namespace ILSight.SettingsFormat;

/// <summary>A symbol analyzed, as a settings file can hold it. Without a file it is none.</summary>
internal sealed record CodeAnchorJson(CodeAnchorKind Kind, string? FilePath, int Token, int ILOffset);
