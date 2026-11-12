using ILSight.Core;

namespace ILSight.App.Tests;

/// <summary>A search's stream that yields nothing until it is told to fail, whatever cancels it.</summary>
internal sealed class HeldSearch
{
    private readonly TaskCompletionSource<IReadOnlyList<SearchResult>> _next = new();

    public async IAsyncEnumerable<IReadOnlyList<SearchResult>> StreamAsync()
    {
        yield return await _next.Task;
    }

    public void Fail()
    {
        _next.SetException(new InvalidOperationException("The search broke."));
    }
}
