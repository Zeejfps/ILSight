namespace ILSight.Core;

/// <summary>A run of a <see cref="DocumentLine"/>'s text: characters <see cref="Start"/> up to
/// <see cref="End"/>, optionally pointing at a <see cref="Reference"/>, which this run defines when
/// <see cref="IsDefinition"/> is set.</summary>
public readonly record struct DocumentSpan(int Start, int Length, SpanKind Kind, CodeReference? Reference, bool IsDefinition)
{
    public int End => Start + Length;
}
