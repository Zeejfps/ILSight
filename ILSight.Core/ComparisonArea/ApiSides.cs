namespace ILSight.Core.ComparisonArea;

/// <summary>Which of the assemblies compared a type or member is in, and how each has it.</summary>
public union ApiSides(ApiSides.LeftOnly, ApiSides.RightOnly, ApiSides.Both)
{
    public sealed record LeftOnly(ApiSide Left);

    public sealed record RightOnly(ApiSide Right);

    public sealed record Both
    {
        public required ApiSide Left { get; init; }

        public required ApiSide Right { get; init; }

        /// <summary>Whether the two assemblies declare it differently.</summary>
        public bool IsDeclaredDifferently => Left.Declaration != Right.Declaration;
    }

    /// <summary>How the type or member differs from the left assembly to the right one, as its sides say.</summary>
    public ApiChange Change => this switch
    {
        LeftOnly => ApiChange.Removed,
        RightOnly => ApiChange.Added,
        Both both => both.IsDeclaredDifferently ? ApiChange.Changed : ApiChange.Unchanged,
    };
}
