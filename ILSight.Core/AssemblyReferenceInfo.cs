namespace ILSight.Core;

/// <summary>An assembly <see cref="Module"/> references, as its metadata names it.</summary>
public sealed class AssemblyReferenceInfo
{
    public required LoadedModule Module { get; init; }

    public required ICSharpCode.Decompiler.Metadata.AssemblyReference Reference { get; init; }
}
