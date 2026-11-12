using System.Runtime.CompilerServices;

namespace ILSight.Core;

/// <summary>Something compared as this very object, whatever its own <c>Equals</c> says, as the key of a
/// <see cref="Memo{TKey, TValue}"/> or anything else that compares with <c>Equals</c>. One made by
/// <c>default</c> is the same as one of null.</summary>
public readonly struct ByReference<T> : IEquatable<ByReference<T>> where T : class
{
    private readonly T? _item;

    public ByReference(T? item)
    {
        _item = item;
    }

    public bool Equals(ByReference<T> other)
    {
        return ReferenceEquals(_item, other._item);
    }

    public override bool Equals(object? obj)
    {
        return obj is ByReference<T> other && Equals(other);
    }

    public override int GetHashCode()
    {
        return RuntimeHelpers.GetHashCode(_item);
    }
}
