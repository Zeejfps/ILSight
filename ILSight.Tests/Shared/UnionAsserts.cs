using System.Runtime.CompilerServices;

namespace ILSight.Tests;

/// <summary>Assertions on the case a union holds. xUnit's <c>Assert.IsType</c> takes an object, and a union is a
/// struct: boxed, it is the union, never its case, so <c>Assert.IsType&lt;Case&gt;(union)</c> fails on every union.
/// A type pattern sees the case (<c>union is Case c</c>), but not a generic one over a union not known statically.</summary>
public static class UnionAsserts
{
    /// <summary>The case <paramref name="union"/> holds, asserted to be a <typeparamref name="TCase"/>.</summary>
    public static TCase GetCase<TCase>(IUnion? union)
    {
        Assert.NotNull(union);
        // The suite's one read of a union's Value: xUnit's type check needs the case as an object.
        return Assert.IsType<TCase>(union.Value);
    }
}
