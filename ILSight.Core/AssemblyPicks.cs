using System.Collections.Immutable;

namespace ILSight.Core;

/// <summary>
/// What the user picked for each assembly of a list, by the assembly's path, such as the PDB it reads
/// or the framework its references are resolved against. An assembly takes its pick when it is opened
/// next. Kept when the list is cleared, as a reload does; forgotten with the assembly when it is
/// closed. Safe from any thread.
/// </summary>
public sealed class AssemblyPicks<T> where T : class
{
    private static readonly ImmutableDictionary<AssemblyPath, T> None = ImmutableDictionary<AssemblyPath, T>.Empty;

    private readonly Lock _gate = new();
    private ImmutableDictionary<AssemblyPath, T> _picks = None;

    /// <summary>Every pick, by the assembly's path.</summary>
    public IReadOnlyDictionary<AssemblyPath, T> All => Volatile.Read(ref _picks);

    /// <summary>Has the assembly at <paramref name="assemblyPath"/> take <paramref name="value"/> from
    /// the next time it is opened.</summary>
    public void Pick(AssemblyPath assemblyPath, T value)
    {
        lock (_gate)
            Volatile.Write(ref _picks, _picks.SetItem(assemblyPath, value));
    }

    /// <summary>Has the assembly at <paramref name="assemblyPath"/> find for itself again what it was
    /// given, from the next time it is opened.</summary>
    public void Forget(AssemblyPath assemblyPath)
    {
        lock (_gate)
            Volatile.Write(ref _picks, _picks.Remove(assemblyPath));
    }

    /// <summary>Has the assembly at <paramref name="assemblyPath"/> find for itself again what it was
    /// given, from the next time it is opened; with <paramref name="andUnder"/>, as for a package, every
    /// assembly under it as well. All at once, under the one lock.</summary>
    public void Forget(AssemblyPath assemblyPath, bool andUnder)
    {
        lock (_gate)
        {
            var picks = _picks.Remove(assemblyPath);
            if (andUnder)
                picks = picks.RemoveRange(picks.Keys.Where(path => path.IsUnder(assemblyPath)));
            Volatile.Write(ref _picks, picks);
        }
    }

    /// <summary>Takes <paramref name="picks"/> in place of those before, as when another list opens.</summary>
    public void Use(IReadOnlyDictionary<AssemblyPath, T> picks)
    {
        var taken = ImmutableDictionary.CreateRange(picks);
        lock (_gate)
            Volatile.Write(ref _picks, taken);
    }

    /// <summary>Forgets every pick, as when the list is closed for good.</summary>
    public void ForgetAll()
    {
        lock (_gate)
            Volatile.Write(ref _picks, None);
    }

    /// <summary>What was picked for the assembly at <paramref name="assemblyPath"/>, or null.</summary>
    public T? Find(AssemblyPath assemblyPath)
    {
        return All.GetValueOrDefault(assemblyPath);
    }
}
