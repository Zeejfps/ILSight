namespace ILSight;

/// <summary>Why two assemblies could not be compared.</summary>
public abstract record ComparisonFailure
{
    private ComparisonFailure()
    {
    }

    /// <summary>The assembly read from <see cref="File"/> is no longer open.</summary>
    public sealed record AssemblyClosed(string File) : ComparisonFailure;

    /// <summary>Comparing the assemblies <see cref="Left"/> and <see cref="Right"/> failed.</summary>
    public sealed record Failed(string Left, string Right) : ComparisonFailure;
}
