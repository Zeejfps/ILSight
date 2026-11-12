namespace ILSight.Core;

/// <summary>A fold the decompiler started and hasn't ended: where it starts and how it shows folded.</summary>
internal readonly record struct OpenFold(int Line, int Column, string Text, bool DefaultCollapsed, bool IsDefinition);
