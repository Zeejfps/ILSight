namespace ILSight.Core;

/// <summary>A value that counts its own changes: <see cref="Version"/> rises each time <see cref="Value"/> is set
/// to a different one, and only then, so whoever must notice it changing compares <see cref="Version"/> with the
/// last one it saw. A class, so that there is one count however many hold it.</summary>
public sealed class Tracked<T>
{
    private T _value;

    public Tracked(T value)
    {
        _value = value;
    }

    /// <summary>The value; setting the one it holds changes nothing.</summary>
    public T Value
    {
        get => _value;
        set
        {
            if (EqualityComparer<T>.Default.Equals(_value, value))
            {
                return;
            }

            _value = value;
            Version++;
        }
    }

    public int Version { get; private set; }
}
