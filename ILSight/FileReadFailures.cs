using ILSight.Core;

namespace ILSight;

/// <summary>The failures an adapter reading files expects, translated where it awaits the read: the file can't be
/// reached (<see cref="IOException"/>, <see cref="UnauthorizedAccessException"/>) or isn't what it should be
/// (<see cref="BadImageFormatException"/>). Anything else the read throws is a bug and goes on to the workflow's edge,
/// as does the cancellation of the read's token, even when the read ended before it saw it.</summary>
public static class FileReadFailures
{
    /// <summary>What <paramref name="read"/> gave, or the failure reading the file it ended with;
    /// <paramref name="ct"/> is the token the read was given, whose cancellation is let through.</summary>
    public static async Task<Outcome<T>> CatchAsync<T>(BackgroundWork<T> read, CancellationToken ct)
    {
        try
        {
            var value = await read;
            ct.ThrowIfCancellationRequested();
            return new Outcome<T>.Ok(value);
        }
        catch (IOException e)
        {
            return new Outcome<T>.Failed(e);
        }
        catch (UnauthorizedAccessException e)
        {
            return new Outcome<T>.Failed(e);
        }
        catch (BadImageFormatException e)
        {
            return new Outcome<T>.Failed(e);
        }
    }
}
