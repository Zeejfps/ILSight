namespace ILSight.Core;

/// <summary>What reading a NuGet.Config came to: there is none (NuGet's documented default applies), it couldn't be
/// read (and why), or what it says. What an unreadable one means is the reader's policy.</summary>
public union NuGetConfigRead(NuGetConfigRead.Missing, NuGetConfigRead.Unreadable, NuGetConfigRead.Read)
{
    /// <summary>No config at its place: an optional file, NuGet's default (nuget.org) standing for it.</summary>
    public sealed record Missing;

    /// <summary>A config that couldn't be read, and <see cref="Why"/>, as the system or the XML reader said it.</summary>
    public sealed record Unreadable
    {
        public required string Why { get; init; }
    }

    /// <summary>What the config says: its enabled sources, none when it clears or disables them all.</summary>
    public sealed record Read
    {
        public required NuGetConfig Config { get; init; }
    }
}
