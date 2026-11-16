namespace ILSight.Core.ProcessArea;

/// <summary>A failure ILSight's own diagnostics client found in a runtime's answer: <see cref="Cause"/> is what the app
/// words, the message its English diagnostic. An <see cref="IOException"/>, as the connection's own failures are.</summary>
public sealed class DiagnosticsIpcException(UnreachableCause cause, string diagnostic) : IOException(diagnostic)
{
    public UnreachableCause Cause { get; } = cause;
}
