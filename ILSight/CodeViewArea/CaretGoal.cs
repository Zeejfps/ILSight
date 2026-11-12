namespace ILSight.CodeViewArea;

/// <summary>The column Up and Down keep the caret to, <see cref="Column"/>, while it stands at <see cref="At"/>,
/// where the last of them left it; once it moves any other way the goal is stale.</summary>
internal readonly record struct CaretGoal
{
    public required TextPosition At { get; init; }

    public required int Column { get; init; }
}
