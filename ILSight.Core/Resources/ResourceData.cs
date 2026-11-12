namespace ILSight.Core.Resources;

/// <summary>A resource's bytes, which can be read to save them.</summary>
internal sealed record ResourceData
{
    /// <summary>What the resource is, as a resources folder lists it.</summary>
    public required string Description { get; init; }

    /// <summary>The name a saved copy is offered under.</summary>
    public required string FileName { get; init; }

    /// <summary>What a saved copy holds, read only when asked for.</summary>
    public required Func<byte[]> ReadData { get; init; }
}
