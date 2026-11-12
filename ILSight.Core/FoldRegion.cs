namespace ILSight.Core;

/// <summary>A foldable range of a <see cref="DecompiledDocument"/>. Collapsed, line
/// <see cref="StartLine"/> stays in view followed by <see cref="CollapsedText"/>, and the lines after
/// it up to and including <see cref="EndLine"/> are hidden.</summary>
public readonly record struct FoldRegion
{
    public required int StartLine { get; init; }

    public required int EndLine { get; init; }

    public required string CollapsedText { get; init; }

    /// <summary>Whether the region starts out collapsed, as the using directives do.</summary>
    public required bool DefaultCollapsed { get; init; }

    /// <summary>Whether the region is the body of a type or member definition.</summary>
    public required bool IsDefinition { get; init; }
}
