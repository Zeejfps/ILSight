using ILSight.Core.Resources;

namespace ILSight.Core;

/// <summary>
/// The icons packages show beside their names: each fetched from the URL its package gives, a few at
/// a time, decoded and shrunk to fit a square. Every call may be made from any thread.
/// </summary>
public sealed class PackageIcons
{
    public const int MaxFetches = 4;

    public required HttpClient Client { get; init; }
    public readonly SemaphoreSlim Fetches = new(MaxFetches, MaxFetches);
}
