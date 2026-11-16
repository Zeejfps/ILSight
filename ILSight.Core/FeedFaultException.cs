namespace ILSight.Core;

/// <summary>A NuGet feed's answer ILSight's own reader can't use: <see cref="Fault"/> is the cause the app words, the
/// message its English diagnostic.</summary>
public sealed class FeedFaultException(FeedFault fault, string diagnostic, Exception? inner = null) : Exception(diagnostic, inner)
{
    public FeedFault Fault { get; } = fault;
}
