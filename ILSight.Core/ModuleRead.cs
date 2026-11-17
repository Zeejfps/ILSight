namespace ILSight.Core;

/// <summary>What a unit reading one module came to: what it read, or that the module was released (closed, and
/// nothing read it any more) before the unit's reading began, and nothing was read
/// (<see cref="LoadedAssemblies.TryRead"/>).</summary>
public union ModuleRead<T>(ModuleRead<T>.Read, ModuleRead<T>.Closed)
{
    public sealed record Read
    {
        public required T Value { get; init; }
    }

    public sealed record Closed
    {
        public required LoadedModule Module { get; init; }
    }
}
