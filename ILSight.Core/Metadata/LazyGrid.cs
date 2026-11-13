namespace ILSight.Core.Metadata;

/// <summary>A grid made the first time it is asked for, with the rows its first show needs read
/// (<see cref="MetadataGrids.CreateGrid"/>), then kept: every later ask gets the same grid, whatever rows it names.
/// Safe to ask from several threads at once: the first ask makes it while the others wait for it.</summary>
public sealed class LazyGrid
{
    private readonly Lock _gate = new();
    private MetadataGrid? _made;

    /// <summary>Makes the grid with at least the number of rows given read, as its content is made.</summary>
    public required Func<int, MetadataGrid> Make { private get; init; }

    /// <summary>The grid, made now with at least <paramref name="firstRows"/> rows read when it wasn't made before.
    /// I/O for a grid over a file the library maps: call it off the screen's thread, as the node's content is made.</summary>
    public MetadataGrid Ensure(int firstRows)
    {
        lock (_gate)
        {
            var made = _made;
            if (made is null)
            {
                made = Make(firstRows);
                _made = made;
            }

            return made;
        }
    }
}
