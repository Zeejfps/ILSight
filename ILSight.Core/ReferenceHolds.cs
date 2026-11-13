namespace ILSight.Core;

/// <summary>The modules a module's resolver handed out to the library (<see cref="AssemblyResolver.Resolve"/>), each
/// under a reading held for as long as the resolving module's library lives: its type systems keep the files they were
/// handed and read them on every later use, so a referenced module is released only once every module that reads it
/// through its type system is. Ended when the resolving module is released.</summary>
internal sealed class ReferenceHolds
{
    private readonly Lock _sync = new();

    private readonly HashSet<ModuleLibrary> _held = new(ReferenceEqualityComparer.Instance);

    private bool _isEnded;

    /// <summary>Holds <paramref name="library"/> under a reading, once; false when it was released, or these holds
    /// ended.</summary>
    public bool TryHold(ModuleLibrary library)
    {
        lock (_sync)
        {
            if (_isEnded)
            {
                return false;
            }

            if (_held.Contains(library))
            {
                return true;
            }

            if (!library.TryBeginReading())
            {
                return false;
            }

            _held.Add(library);
            return true;
        }
    }

    /// <summary>Ends the reading of each module held; none is held after.</summary>
    public void EndAll()
    {
        List<ModuleLibrary> held;
        lock (_sync)
        {
            _isEnded = true;
            held = _held.ToList();
            _held.Clear();
        }

        foreach (var library in held)
        {
            library.EndReading();
        }
    }
}
