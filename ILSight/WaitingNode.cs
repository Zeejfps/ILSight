using ILSight.Core;

namespace ILSight;

/// <summary>A reopened tab waiting for the node <see cref="Record"/> kept, at <see cref="Place"/> while its file is
/// listed; null when the list holds it no more.</summary>
public sealed record WaitingNode
{
    public required ReopenedTab Reopened { get; init; }

    public required NodeDocumentRecord Record { get; init; }

    public required NodePath? Place { get; init; }
}
