namespace ILSight.Core;

/// <summary>What work over a module's type system came to, its read awaited and matched once before the work began
/// (<see cref="LoadedAssemblies.ReadTypeSystemAsync"/>): the work's answer, or the module the library could not read,
/// with why, and no work done, or the module released before the work began (closed), and nothing done.</summary>
public union TypeSystemWork<T>(TypeSystemWork<T>.Done, TypeSystemWork<T>.Unreadable, TypeSystemWork<T>.Closed)
{
    public sealed record Done
    {
        public required T Value { get; init; }
    }

    public sealed record Unreadable
    {
        public required UnreadableModule Module { get; init; }
    }

    public sealed record Closed
    {
        public required LoadedModule Module { get; init; }
    }
}
