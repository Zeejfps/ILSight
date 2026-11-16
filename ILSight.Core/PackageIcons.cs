using ILSight.Core.ResourceArea;

namespace ILSight.Core;

/// <summary>
/// The icons packages show beside their names: each fetched from the URL its package gives, a few at
/// a time, decoded and shrunk to fit a square. Every call may be made from any thread.
/// </summary>
public sealed class PackageIcons
{
    public const int MaxFetches = 4;

    // nuget.org takes no icon file larger.
    private const int MaxBytes = 1024 * 1024;

    private readonly SemaphoreSlim _fetches = new(MaxFetches, MaxFetches);

    public required HttpClient Client { get; init; }

    /// <summary>The picture at <paramref name="url"/>, shrunk to fit a square <paramref name="side"/>
    /// pixels across; no more than <see cref="MaxFetches"/> are fetched at a time, the rest waiting their turn.</summary>
    /// <exception cref="HttpRequestException">The picture could not be fetched.</exception>
    /// <exception cref="InvalidDataException">It is no picture, or too large a file.</exception>
    public async Task<ResourceImage> GetIconAsync(WebUrl url, int side, CancellationToken cancellationToken = default)
    {
        await _fetches.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var bytes = await ReadIconBytesAsync(url, cancellationToken).ConfigureAwait(false);
            var read = ResourceImages.DecodeImage(bytes);
            return read.Match(decoded => ResourceImages.FitInto(decoded.Image, side), static unreadable => throw new InvalidDataException(unreadable.Error.Message, unreadable.Error));
        }
        finally
        {
            _fetches.Release();
        }
    }

    private async Task<byte[]> ReadIconBytesAsync(WebUrl url, CancellationToken cancellationToken)
    {
        using var response = await Client.GetAsync(url.Uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > MaxBytes)
        {
            throw new InvalidDataException($"The icon at {url} is larger than {MaxBytes} bytes.");
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        if (bytes.Length > MaxBytes)
        {
            throw new InvalidDataException($"The icon at {url} is larger than {MaxBytes} bytes.");
        }

        return bytes;
    }
}
