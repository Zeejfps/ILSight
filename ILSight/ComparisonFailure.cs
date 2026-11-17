namespace ILSight;

/// <summary>Why two assemblies could not be compared.</summary>
public union ComparisonFailure(ComparisonFailure.AssemblyClosed, ComparisonFailure.Failed)
{
    /// <summary>The assembly read from <see cref="File"/> is no longer open.</summary>
    public sealed record AssemblyClosed(string File);

    /// <summary>Comparing the assemblies <see cref="Left"/> and <see cref="Right"/> failed.</summary>
    public sealed record Failed
    {
        public required string Left { get; init; }

        public required string Right { get; init; }
    }
}
