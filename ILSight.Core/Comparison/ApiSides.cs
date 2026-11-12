namespace ILSight.Core.Comparison;

/// <summary>Which of the assemblies compared a type or member is in, and how each has it.</summary>
public abstract record ApiSides
{
    private ApiSides()
    {
    }

    /// <summary>How the type or member differs from the left assembly to the right one, as its sides say.</summary>
    public abstract ApiChange Change { get; }

    public sealed record LeftOnly(ApiSide Left) : ApiSides
    {
        public override ApiChange Change => ApiChange.Removed;
    }

    public sealed record RightOnly(ApiSide Right) : ApiSides
    {
        public override ApiChange Change => ApiChange.Added;
    }

    public sealed record Both(ApiSide Left, ApiSide Right) : ApiSides
    {
        /// <summary>Whether the two assemblies declare it differently.</summary>
        public bool IsDeclaredDifferently => Left.Declaration != Right.Declaration;

        public override ApiChange Change => IsDeclaredDifferently ? ApiChange.Changed : ApiChange.Unchanged;
    }
}
