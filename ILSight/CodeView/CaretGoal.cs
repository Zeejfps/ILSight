namespace ILSight.CodeView;

/// <summary>The column Up and Down keep the caret to, <see cref="Column"/>, while it stands at <see cref="At"/>,
/// where the last of them left it; once it moves any other way the goal is stale.</summary>
internal readonly record struct CaretGoal(TextPosition At, int Column);
