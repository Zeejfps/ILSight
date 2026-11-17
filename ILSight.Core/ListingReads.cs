namespace ILSight.Core;

/// <summary>The reads of a node's children listing (<see cref="BrowseNodes.CreateChildren"/>), each under a catch of
/// its own that returns what it read or what the library threw there: what the library threw is thrown on here, outside
/// that catch, as a <see cref="ListingReadException"/>, which the listing's build catches alone
/// (<see cref="BrowseSessions"/>), so its children fail with why. The app's rules over what was read (which rows show,
/// in what order, grouped how) run between the reads, outside their catches, so a bug of the app's goes on to the
/// edge.</summary>
internal static class ListingReads
{
    /// <summary>The value <paramref name="read"/> read; what the library threw there, thrown on as the listing's failed
    /// read.</summary>
    internal static T ValueOf<T>(Outcome<T> read)
    {
        return read switch
        {
            Outcome<T>.Ok ok => ok.Value,
            Outcome<T>.Failed failed => throw new ListingReadException(failed.Error),
        };
    }
}
