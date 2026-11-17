namespace ILSight.Core;

/// <summary>What the library threw at a read of a node's children listing (<see cref="ListingReads"/>), thrown on outside
/// that read's catch to end the listing as failed with <see cref="Error"/>.</summary>
internal sealed class ListingReadException(Exception error) : Exception(error.Message, error)
{
    public Exception Error { get; } = error;
}
