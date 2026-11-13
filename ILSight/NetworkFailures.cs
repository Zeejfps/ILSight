using ILSight.Core;

namespace ILSight;

/// <summary>The failures an adapter fetching over the network expects, translated where it awaits the fetch: the
/// request failed (<see cref="HttpRequestException"/>, <see cref="IOException"/>), timed out (a cancellation its own
/// token didn't ask for), or the answer wasn't what the protocol says (<see cref="InvalidDataException"/>). Anything
/// else is a bug and goes on to the workflow's edge, as does the cancellation of the fetch's token.</summary>
public static class NetworkFailures
{
    /// <summary>What <paramref name="fetch"/> gave, or the failure of the fetch it ended with; <paramref name="ct"/> is
    /// the token the fetch was given, whose cancellation is let through.</summary>
    public static async Task<Outcome<T>> CatchAsync<T>(BackgroundWork<T> fetch, CancellationToken ct)
    {
        try
        {
            var value = await fetch;
            ct.ThrowIfCancellationRequested();
            return new Outcome<T>.Ok(value);
        }
        catch (TaskCanceledException e) when (!ct.IsCancellationRequested)
        {
            return new Outcome<T>.Failed(new TimeoutException("The request timed out.", e));
        }
        catch (HttpRequestException e)
        {
            return new Outcome<T>.Failed(e);
        }
        catch (IOException e)
        {
            return new Outcome<T>.Failed(e);
        }
        catch (InvalidDataException e)
        {
            return new Outcome<T>.Failed(e);
        }
    }

    /// <summary>What <paramref name="fetch"/>, which gives nothing back, came to, as
    /// <see cref="CatchAsync{T}(BackgroundWork{T}, CancellationToken)"/> says.</summary>
    public static async Task<Outcome> CatchAsync(BackgroundWait fetch, CancellationToken ct)
    {
        try
        {
            await fetch;
            ct.ThrowIfCancellationRequested();
            return new Outcome.Done();
        }
        catch (TaskCanceledException e) when (!ct.IsCancellationRequested)
        {
            return new Outcome.Failed(new TimeoutException("The request timed out.", e));
        }
        catch (HttpRequestException e)
        {
            return new Outcome.Failed(e);
        }
        catch (IOException e)
        {
            return new Outcome.Failed(e);
        }
        catch (InvalidDataException e)
        {
            return new Outcome.Failed(e);
        }
    }
}
