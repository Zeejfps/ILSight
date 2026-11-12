namespace ILSight;

/// <summary>A place in a code view's text, as a settings file can hold it.</summary>
internal readonly record struct TextPositionJson(int Line, int Column);
