using System.Diagnostics;

namespace ILSight;

/// <summary>What saving a resource to a file came to.</summary>
public abstract record ResourceSave
{
    private ResourceSave()
    {
    }

    /// <summary>The file holds the resource's bytes.</summary>
    public sealed record Saved : ResourceSave;

    /// <summary>The resource's assembly, the file <see cref="Assembly"/>, was closed before it was read: nothing was
    /// written.</summary>
    public sealed record Closed : ResourceSave
    {
        public required string Assembly { get; init; }
    }

    /// <summary>The resource could not be read, or the file written.</summary>
    public sealed record Failed : ResourceSave
    {
        public required Exception Error { get; init; }
    }

    /// <summary>Does what the handler of whichever case this is does.</summary>
    public void Switch(Action<Saved> saved, Action<Closed> closed, Action<Failed> failed)
    {
        switch (this)
        {
            case Saved s:
                saved(s);
                return;
            case Closed c:
                closed(c);
                return;
            case Failed f:
                failed(f);
                return;
            default:
                throw new UnreachableException($"ResourceSave.Switch does not handle {GetType().Name}.");
        }
    }
}
