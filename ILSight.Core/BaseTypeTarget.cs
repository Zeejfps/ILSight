using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

/// <summary>What a base type resolves to: a definition that can be navigated to, one that cannot, or none.</summary>
public abstract record BaseTypeTarget
{
    private BaseTypeTarget()
    {
    }

    public sealed record Navigable(ITypeDefinition Definition, CodeReference.Entity Reference) : BaseTypeTarget;

    public sealed record Unaddressed(ITypeDefinition Definition) : BaseTypeTarget;

    public sealed record Unresolved : BaseTypeTarget;
}
