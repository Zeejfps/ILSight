namespace ILSight.Core;

/// <summary>A file of a single-file bundle ILSight's own reader can't read: <see cref="Fault"/> is the cause the app
/// words, the message its English diagnostic. Not an <see cref="IOException"/>: the bundle's data is wrong, the file
/// itself was read.</summary>
public sealed class BundleEntryException(BundleEntryFault fault, string diagnostic) : Exception(diagnostic)
{
    public BundleEntryFault Fault { get; } = fault;
}
