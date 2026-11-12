using ILSight.Core;

namespace ILSight.Tests;

/// <summary>The browse session the tests of this assembly read trees through, and that the app states
/// they make browse with. A session keeps what it knows of an assembly with the assembly, so tests of
/// lists of their own never share a node.</summary>
internal static class TestSession
{
    public static BrowseSession Shared { get; } = new();
}
