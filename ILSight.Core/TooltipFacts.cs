namespace ILSight.Core;

/// <summary>What a tooltip reads of the library (<see cref="Documentation.ReadTooltip"/>).</summary>
internal sealed record TooltipFacts
{
    public required CodeSignature Signature { get; init; }
    public required CodeContainer? Container { get; init; }
    public required DocNode? Documentation { get; init; }
}
