namespace ILSight.Core;

/// <summary>A value reachable only holding its own lock: <see cref="Enter"/> takes the lock and hands back
/// the one way to the value, which lets go as it is disposed. It takes no lambda, so holding it allocates
/// nothing, and a <c>return</c> from inside works as it does from a <c>lock</c>. Being a <c>ref struct</c>, the
/// scope can't be kept in a field or held across an <c>await</c>, so the compiler stops what a
/// <see cref="Lock"/> mustn't do. The lock is taken again freely by the thread holding it, as a
/// <c>lock</c> is. A reference copied out of the scope stays reachable after it: keep what is handed out
/// immutable, or don't hand it out.</summary>
public sealed class Guarded<T>
{
    private readonly Lock _gate = new();
    private T _value;

    public Guarded(T value)
    {
        _value = value;
    }

    /// <summary>Waits for the lock, and holds it until what this returns is disposed.</summary>
    public Scope Enter()
    {
        _gate.Enter();
        return new Scope(this);
    }

    /// <summary>The lock held, and through it the value. One made by <c>default</c> holds nothing: its
    /// <see cref="Value"/> throws, as nothing was locked, and disposing it does nothing.</summary>
    public ref struct Scope
    {
        private Guarded<T>? _owner;

        internal Scope(Guarded<T> owner)
        {
            _owner = owner;
        }

        public readonly ref T Value
        {
            get
            {
                if (_owner == null)
                    throw new InvalidOperationException("The lock is not held.");
                return ref _owner._value;
            }
        }

        public void Dispose()
        {
            var owner = _owner;
            if (owner == null)
                return;
            _owner = null;
            owner._gate.Exit();
        }
    }
}
