namespace ILSight.Core;

/// <summary>A line naming <see cref="Text"/>, going to <see cref="Target"/> when there is one.</summary>
internal readonly record struct SummaryLink(string Text, CodeReference? Target);
