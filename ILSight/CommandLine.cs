namespace ILSight;

/// <summary>The arguments a run was started with, read: how it starts, and what it asks of the app.</summary>
public sealed record CommandLine(LaunchConfig Launch, RunArguments Arguments);
