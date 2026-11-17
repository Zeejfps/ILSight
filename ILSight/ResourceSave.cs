namespace ILSight;

/// <summary>What saving a resource to a file came to.</summary>
public union ResourceSave(ResourceSave.Saved, ResourceSave.Closed, ResourceSave.Failed)
{
    /// <summary>The file holds the resource's bytes.</summary>
    public sealed record Saved;

    /// <summary>The resource's assembly, the file <see cref="Assembly"/>, was closed before it was read: nothing was
    /// written.</summary>
    public sealed record Closed
    {
        public required string Assembly { get; init; }
    }

    /// <summary>The resource could not be read, or the file written.</summary>
    public sealed record Failed
    {
        public required Exception Error { get; init; }
    }
}
