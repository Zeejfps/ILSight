namespace ILSight.Core;

/// <summary>The modules a change took off the list (a close, a clear, an item read again in its place, an arrival that
/// stopped), which nothing on the list reads any more: the edge hands them to the I/O side to release
/// (<see cref="LoadedAssemblies.Release"/>), which lets each go once its readings end.</summary>
[MustUse]
public sealed class RemovedModules
{
    public static RemovedModules None { get; } = new RemovedModules { Modules = [] };

    public required IReadOnlyList<LoadedModule> Modules { get; init; }

    /// <summary>These and <paramref name="other"/>'s, in that order.</summary>
    public RemovedModules With(RemovedModules other)
    {
        if (other.Modules.Count == 0)
        {
            return this;
        }

        if (Modules.Count == 0)
        {
            return other;
        }

        return new RemovedModules { Modules = [.. Modules, .. other.Modules] };
    }
}
