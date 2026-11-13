namespace ILSight.Core;

/// <summary>A unit's reading of the modules it reads (<see cref="LoadedAssemblies.BeginReading"/>): each module is
/// added as the unit comes to read it, and while the reading is open none of them is released, closed or not. The unit
/// disposes it however it ends, each module's reading ended once.</summary>
public sealed class ModuleReading : IDisposable
{
    private readonly Lock _sync = new();

    private readonly List<ModuleLibrary> _libraries = [];

    private bool _isEnded;

    internal ModuleReading()
    {
    }

    /// <summary>Adds <paramref name="library"/>'s module to what this reads, once; false when it was released.</summary>
    internal bool TryAdd(ModuleLibrary library)
    {
        lock (_sync)
        {
            if (_isEnded)
            {
                throw new InvalidOperationException("A module was added to a reading that ended.");
            }

            if (_libraries.Contains(library))
            {
                return true;
            }

            if (!library.TryBeginReading())
            {
                return false;
            }

            _libraries.Add(library);
            return true;
        }
    }

    public void Dispose()
    {
        List<ModuleLibrary> read;
        lock (_sync)
        {
            if (_isEnded)
            {
                return;
            }

            _isEnded = true;
            read = [.. _libraries];
            _libraries.Clear();
        }

        foreach (var library in read)
        {
            library.EndReading();
        }
    }
}
