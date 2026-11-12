namespace ILSight.Core;

/// <summary>What a base type resolves to: a definition that can be navigated to, one that cannot, or none.</summary>
public abstract record BaseTypeTarget
{
    private BaseTypeTarget()
    {
    }

    public sealed record Navigable(CodeReference.Entity Reference) : BaseTypeTarget;

    public sealed record Unaddressed : BaseTypeTarget;

    public sealed record Unresolved : BaseTypeTarget;
}
