using ILSight.Core;

namespace ILSight.App.Tests;

/// <summary>What the handler asked the decompiler for, and where the test answers it.</summary>
internal sealed record DecompileRequest
{
    public required BrowseNode Node { get; init; }

    public required DecompilationLanguage Language { get; init; }

    public required TaskCompletionSource<Outcome<NodeContent>> Answer { get; init; }
}
