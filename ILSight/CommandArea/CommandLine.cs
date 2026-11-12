namespace ILSight.CommandArea;

/// <summary>The arguments a run was started with, read: how it starts, and what it asks of the app.</summary>
public sealed record CommandLine
{
    public required LaunchConfig Launch { get; init; }

    public required RunArguments Arguments { get; init; }
}
