using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ILSight.Core;

/// <summary>What was worked out, kept with the key it was worked out for. Immutable, so a change is a new
/// one assigned over the old, which replaces both at once, and a copy can't lose anything: a
/// <c>readonly</c> field holding one can't be assigned at all. Allocates nothing. One made by
/// <c>default</c> holds nothing. How keys compare is <typeparamref name="TKey"/>'s own <c>Equals</c>, so a
/// tuple compares each of its parts in turn; wrap what must compare by reference in a
/// <see cref="ByReference{T}"/>, as a record otherwise compares by value.</summary>
public readonly struct Memo<TKey, TValue> where TKey : IEquatable<TKey>
{
    private readonly bool _isHeld;
    private readonly TKey _key;
    private readonly TValue _value;

    public Memo(TKey key, TValue value)
    {
        _isHeld = true;
        _key = key;
        _value = value;
    }

    /// <summary>What is kept, whichever key it was for; the default while nothing is.</summary>
    public TValue? Last => _value;

    /// <summary>Gives what is kept when it was worked out for <paramref name="key"/>.</summary>
    public bool TryGet(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        if (_isHeld && EqualityComparer<TKey>.Default.Equals(_key, key))
        {
            value = _value;
            return true;
        }
        value = default;
        return false;
    }
}

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
