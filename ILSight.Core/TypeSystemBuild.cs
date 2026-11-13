namespace ILSight.Core;

/// <summary>What a build of a module's type system came to, and the modules its resolver handed out to it, which the
/// type system keeps and reads on every later use: a unit using it reads each of them, under its own reading
/// (<see cref="TryReadHandedOut"/>), never under a hold for the type system's life (two modules referencing each other
/// would never be released).</summary>
internal sealed class TypeSystemBuild<T>
{
    public required T Built { get; init; }

    public required IReadOnlyList<ModuleLibrary> HandedOut { get; init; }

    /// <summary>Adds each module handed out to the build to <paramref name="reading"/>, the unit's; false when one was
    /// closed, which the unit answers by building the type system again, resolving without it.</summary>
    public bool TryReadHandedOut(ModuleReading reading)
    {
        foreach (var library in HandedOut)
        {
            if (!reading.TryAdd(library))
            {
                return false;
            }
        }

        return true;
    }
}
