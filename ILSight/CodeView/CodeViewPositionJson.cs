
namespace ILSight.CodeView;

/// <summary>Where a code view stood, as a settings file can hold it.</summary>
internal readonly record struct CodeViewPositionJson(int TopLine, float ScrollX, TextPositionJson Anchor, TextPositionJson Caret);
