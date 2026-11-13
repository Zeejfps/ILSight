namespace ILSight.ILSightAppArea;

/// <summary>A run's arguments as the receiver reads them off the pipe, with the id of the request that carried them,
/// the same each time the run sends them again, so that the receiver takes them once.</summary>
internal sealed record HandedArguments
{
    public required Guid RequestId { get; init; }

    public required RunArguments Arguments { get; init; }
}
