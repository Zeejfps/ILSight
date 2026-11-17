namespace ILSight.CommandArea;

/// <summary>One decision of what a command applies to where the user is, which every input of it reads, its menu item
/// and its shortcut alike: it runs on <see cref="Runs.Target"/>; it is unavailable now, with why (the menu shows it
/// disabled with the reason, a key press reports it where the user acted); or it isn't offered there at all (the menu
/// doesn't list it, the key does nothing).</summary>
public union CommandOffer<T>(CommandOffer<T>.Runs, CommandOffer<T>.Unavailable, CommandOffer<T>.NotOffered) where T : notnull
{
    public sealed record Runs
    {
        public required T Target { get; init; }
    }

    public sealed record Unavailable
    {
        public required Refusal Why { get; init; }
    }

    public sealed record NotOffered;
}
