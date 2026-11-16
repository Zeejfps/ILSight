using System.Collections;
using ILSight.Core;

namespace ILSight.Tests;

/// <summary>A node's children as if the app's listing had made them wrong: walking them throws. Matching a path's step
/// among them is the app's work after the listing's catch, so what it throws is the app's bug.</summary>
internal sealed class BrokenListing : IReadOnlyList<BrowseNode>
{
    public const string Message = "The app's listing is broken.";

    public int Count => 1;

    // Explicit, so the test assembly's metadata gains no property named Item beside the fixtures' indexer.
    BrowseNode IReadOnlyList<BrowseNode>.this[int index] => throw new InvalidOperationException(Message);

    public IEnumerator<BrowseNode> GetEnumerator()
    {
        throw new InvalidOperationException(Message);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>Settles <paramref name="node"/>'s children in <paramref name="session"/> as a broken listing.</summary>
    public static void Settle(BrowseSession session, BrowseNode node)
    {
        using var held = session.EnsureChildrenSlot(node).Enter();
        held.Value = new ChildrenState.Ready(new BrokenListing());
    }
}
