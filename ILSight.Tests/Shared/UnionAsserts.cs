using System.Runtime.CompilerServices;
using ILSight.Core;

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

    /// <summary><paramref name="reference"/>, asserted to be there and navigable: an assembly's or an entity's. A
    /// navigable is a union of its own, so <c>Assert.IsAssignableFrom</c> never sees a reference as one.</summary>
    public static CodeReference.Navigable GetNavigable(CodeReference? reference)
    {
        Assert.NotNull(reference);
        var navigable = reference.Value.AsNavigable;
        Assert.NotNull(navigable);
        return navigable.Value;
    }

    /// <summary>The type of the case <paramref name="union"/> holds, asserted to hold one: a union's own
    /// <c>GetType()</c> is the union's type, whichever case it holds.</summary>
    public static Type GetCaseType(IUnion union)
    {
        // Read as GetCase reads it: the case is the union's Value.
        var held = union.Value;
        Assert.NotNull(held);
        return held.GetType();
    }
}
