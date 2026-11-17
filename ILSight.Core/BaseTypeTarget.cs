namespace ILSight.Core;

/// <summary>What a base type resolves to: a definition that can be navigated to, one that cannot, or none.</summary>
public union BaseTypeTarget(BaseTypeTarget.Navigable, BaseTypeTarget.Unaddressed, BaseTypeTarget.Unresolved)
{
    public sealed record Navigable(CodeReference.Entity Reference);

    public sealed record Unaddressed;

    public sealed record Unresolved;
}
