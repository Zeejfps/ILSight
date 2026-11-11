using System.Collections;

namespace ILSight.Core;

/// <summary>A value that counts its own changes: <see cref="Changes"/> rises each time <see cref="Set"/> gives
/// it a different one, and only then. A class, so that there is one count however many hold it.</summary>
public sealed class Tracked<T>
{
    public Tracked(T value)
    {
        Value = value;
    }

    public T Value { get; private set; }

    public int Changes { get; private set; }

    /// <summary>Takes <paramref name="value"/>; returns whether it differs from the one before.</summary>
    public bool Set(T value)
    {
        if (EqualityComparer<T>.Default.Equals(Value, value))
        {
            return false;
        }

        Value = value;
        Changes++;
        return true;
    }
}

/// <summary>A set that counts its own changes: <see cref="Revision"/> rises with each one, and only then, so
/// that a view can tell it changed by comparing a number.</summary>
public sealed class TrackedSet<T> : IReadOnlyCollection<T>
{
    private readonly HashSet<T> _items = new();

    public int Revision { get; private set; }

    public int Count => _items.Count;

    public bool Contains(T item)
    {
        return _items.Contains(item);
    }

    /// <summary>Takes <paramref name="item"/> out when it is in, and puts it in when it is out.</summary>
    public void Flip(T item)
    {
        if (!_items.Remove(item))
        {
            _items.Add(item);
        }

        Revision++;
    }

    /// <summary>Holds <paramref name="items"/> in place of what it held.</summary>
    public void Replace(IEnumerable<T> items)
    {
        var replacing = new HashSet<T>(items);
        if (_items.SetEquals(replacing))
        {
            return;
        }

        _items.Clear();
        _items.UnionWith(replacing);
        Revision++;
    }

    public HashSet<T>.Enumerator GetEnumerator()
    {
        return _items.GetEnumerator();
    }

    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
        return _items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _items.GetEnumerator();
    }
}
