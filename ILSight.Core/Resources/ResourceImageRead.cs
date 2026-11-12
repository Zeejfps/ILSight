using System.Diagnostics;

namespace ILSight.Core.Resources;

/// <summary>What reading a picture came to: the picture, or why it can't be read.</summary>
public abstract record ResourceImageRead
{
    private ResourceImageRead()
    {
    }

    public sealed record Decoded : ResourceImageRead
    {
        public required ResourceImage Image { get; init; }
    }

    /// <summary>No picture these decoders know, or one they fail on.</summary>
    public sealed record Unreadable : ResourceImageRead
    {
        public required string Why { get; init; }
    }

    public TResult Match<TResult>(Func<Decoded, TResult> decoded, Func<Unreadable, TResult> unreadable)
    {
        return this switch
        {
            Decoded d => decoded(d),
            Unreadable u => unreadable(u),
            _ => throw new UnreachableException($"ResourceImageRead.Match does not handle {GetType().Name}."),
        };
    }
}
