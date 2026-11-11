using System.Collections;

namespace ILSight.Core;

/// <summary>A list that counts its own changes: <see cref="Revision"/> rises with each one, and only then, so
/// that a view can tell it changed by comparing a number. What changes inside an item the list can't see, so
/// that is told to it with <see cref="NoteChange"/>.</summary>
public sealed class TrackedList<T> : IReadOnlyList<T>
{
    private readonly List<T> _items = new();

    public int Revision { get; private set; }

    public int Count => _items.Count;

    public T this[int index]
    {
        get
        {
            return _items[index];
        }
        set
        {
            if (EqualityComparer<T>.Default.Equals(_items[index], value))
            {
                return;
            }

            _items[index] = value;
            Revision++;
        }
    }

    public void Add(T item)
    {
        _items.Add(item);
        Revision++;
    }

    public void RemoveAt(int index)
    {
        _items.RemoveAt(index);
        Revision++;
    }

    /// <summary>Removes <paramref name="item"/>; returns whether it was there.</summary>
    public bool TryRemove(T item)
    {
        if (!_items.Remove(item))
        {
            return false;
        }

        Revision++;
        return true;
    }

    public void Clear()
    {
        if (_items.Count == 0)
        {
            return;
        }

        _items.Clear();
        Revision++;
    }

    public int IndexOf(T item)
    {
        return _items.IndexOf(item);
    }

    public bool Contains(T item)
    {
        return _items.Contains(item);
    }

    /// <summary>Counts a change the list can't see, to what is inside an item.</summary>
    public void NoteChange()
    {
        Revision++;
    }

    public List<T>.Enumerator GetEnumerator()
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
