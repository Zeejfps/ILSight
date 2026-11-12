namespace ILSight.Core;

/// <summary>An assembly of the running .NET runtime, offered to open from a picker.</summary>
public sealed record RuntimeAssembly
{
    public required string Name { get; init; }

    public required string Framework { get; init; }

    public required AssemblyPath FilePath { get; init; }
}
