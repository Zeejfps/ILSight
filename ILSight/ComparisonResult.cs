using ILSight.Core.ComparisonArea;

namespace ILSight;

/// <summary>What the newest comparison of two assemblies came to.</summary>
public union ComparisonResult(ComparisonResult.NotYet, ComparisonResult.Compared, ComparisonResult.Failed)
{
    /// <summary>No comparison has finished yet.</summary>
    public sealed record NotYet;

    /// <summary>The comparison finished, finding <see cref="Diff"/>.</summary>
    public sealed record Compared
    {
        public required ApiDiff Diff { get; init; }
    }

    /// <summary>The comparison could not be made, for <see cref="Failure"/>.</summary>
    public sealed record Failed
    {
        public required ComparisonFailure Failure { get; init; }
    }
}
