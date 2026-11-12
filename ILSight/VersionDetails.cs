using System.Diagnostics;
using ILSight.Core;

namespace ILSight;

/// <summary>What a feed says of one version of a package: loading, read, or why it could not be.</summary>
public abstract record VersionDetails
{
    private VersionDetails()
    {
    }

    public sealed record Loading : VersionDetails;

    public sealed record Read(PackageVersionDetails Details) : VersionDetails;

    public sealed record Failed(string Error) : VersionDetails;

    /// <summary>Does what the handler of whichever case this is does with it and <paramref name="state"/>, which it
    /// is handed, so that a handler that needs the caller's values can be static and allocate nothing.</summary>
    public void Switch<TState>(TState state, Action<TState, Loading> loading, Action<TState, Read> read,
        Action<TState, Failed> failed)
    {
        switch (this)
        {
            case Loading c:
                loading(state, c);
                return;
            case Read c:
                read(state, c);
                return;
            case Failed c:
                failed(state, c);
                return;
            default:
                throw new UnreachableException($"VersionDetails.Switch does not handle {GetType().Name}.");
        }
    }
}
