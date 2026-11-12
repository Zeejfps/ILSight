using System.Diagnostics;

namespace ILSight;

/// <summary>What saving the settings when they changed did.</summary>
public abstract record Saving
{
    private Saving()
    {
    }

    /// <summary>Nothing changed since the last write, or the assemblies are reloading.</summary>
    public sealed record Unchanged : Saving;

    public sealed record Saved : Saving;

    /// <summary>A failed write is tried again only in <see cref="RetryIn"/> seconds.</summary>
    public sealed record Waiting(double RetryIn) : Saving;

    /// <summary>The write failed, and is tried again in <see cref="RetryIn"/> seconds.</summary>
    public sealed record Failed(string Reason, double RetryIn) : Saving;

    /// <summary>What the handler of whichever case this is makes of it.</summary>
    public TResult Match<TResult>(Func<Unchanged, TResult> unchanged, Func<Saved, TResult> saved,
        Func<Waiting, TResult> waiting, Func<Failed, TResult> failed)
    {
        return this switch
        {
            Unchanged c => unchanged(c),
            Saved c => saved(c),
            Waiting c => waiting(c),
            Failed c => failed(c),
            _ => throw new UnreachableException($"Saving.Match does not handle {GetType().Name}."),
        };
    }
}
