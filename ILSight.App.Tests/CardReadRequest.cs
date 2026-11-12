using ILSight.Core;
using ILSight.Core.Diagrams;

namespace ILSight.App.Tests;

/// <summary>What the use case asked the port to read, and where the test answers it.</summary>
internal sealed record CardReadRequest
{
    public required string Read { get; init; }

    public required List<CodeReference.Entity> Types { get; init; }

    public required TaskCompletionSource<Outcome<List<TypeCard>>> Answer { get; init; }
}
