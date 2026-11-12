namespace ILSight.Core;

/// <summary>A foldable range of a <see cref="DecompiledDocument"/>. Collapsed, line
/// <see cref="StartLine"/> stays in view followed by <see cref="CollapsedText"/>, and the lines after
/// it up to and including <see cref="EndLine"/> are hidden.</summary>
/// <param name="DefaultCollapsed">Whether the region starts out collapsed, as the using directives do.</param>
/// <param name="IsDefinition">Whether the region is the body of a type or member definition.</param>
public readonly record struct FoldRegion(int StartLine, int EndLine, string CollapsedText, bool DefaultCollapsed, bool IsDefinition);
