
namespace ILSight.Diagramming;

/// <summary>What a diagram's card faces are measured for: the cards, which parts of them are open, whether
/// members hidden from other assemblies show, the font and the language.</summary>
public readonly record struct FaceKey(int DiagramVersion, int FoldsVersion, bool ShowsHiddenMembers, float FontSize, string? FontFamily, Strings Strings);
