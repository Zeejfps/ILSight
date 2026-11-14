namespace ILSight.Core;

/// <summary>A reference of the module <see cref="Module"/> whose file was found beside it but could not be read
/// (<see cref="Error"/>, naming the file: a lock, a corrupt file): the module's type system reads the reference by name
/// only, as it does one not found, and its code says which and why. The failure is the reference's, reported once
/// (<see cref="TryTakeReport"/>) however many documents of the module say it.</summary>
public sealed class UnreadReference
{
    private int _reported;

    /// <summary>The name the module references it by: an assembly's name, or a module's file name.</summary>
    public required string Name { get; init; }

    /// <summary>The file name of the module that references it.</summary>
    public required string Module { get; init; }

    public required AssemblyLoadException Error { get; init; }

    /// <summary>True the first time it is asked, for the one who reports the failure; false after.</summary>
    public bool TryTakeReport()
    {
        return Interlocked.Exchange(ref _reported, 1) == 0;
    }
}
