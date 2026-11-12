using ILSight.Core.Diagrams;

namespace ILSight.SettingsFormat;

/// <summary>A card's part folded or opened, as a settings file can hold it: a section of its own or what it
/// inherits, so that with both or neither, or without its card, it is none.</summary>
internal sealed record CardFoldJson(TypeAnchorJson? Card, MemberSection? Section, string? InheritedFrom);
