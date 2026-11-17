namespace ILSight.Core;

/// <summary>What work run in the background gave: its value, or the error it failed with.</summary>
public union Outcome<T>(Outcome<T>.Ok, Outcome<T>.Failed)
{
    public sealed record Ok(T Value);

    public sealed record Failed(Exception Error);
}

/// <summary>What work run in the background that gives nothing back came to: it ran to its end, or the error it
/// failed with. The sibling of <see cref="Outcome{T}"/>, as <see cref="Task"/> is of <see cref="Task{T}"/>.</summary>
public union Outcome(Outcome.Done, Outcome.Failed)
{
    public sealed record Done;

    public sealed record Failed(Exception Error);
}
