namespace ILSight.Core;

/// <summary>A value reachable only holding its own gate, which may be held across an <c>await</c>, as a
/// <see cref="Lock"/> mustn't: one caller at a time, whichever thread each goes on, the others waiting
/// without blocking a thread. Unlike a <see cref="Guarded{T}"/>, the thread holding it can't enter again, as
/// a held gate belongs to no thread: that caller would wait on itself. A reference copied out stays
/// reachable after it: keep what is handed out immutable, or don't hand it out.</summary>
public sealed class AsyncGuarded<T>
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private T _value;

    public AsyncGuarded(T value)
    {
        _value = value;
    }

    /// <summary>Waits for the gate, unless <paramref name="cancellationToken"/> is cancelled first, and holds
    /// it until what this returns is disposed.</summary>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled, and
    /// nothing is held.</exception>
    public async Task<Held> EnterAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Held(this);
    }

    /// <summary>The gate held, and through it the value, until disposed. Once it is, the value is out of
    /// reach and disposing it again does nothing.</summary>
    public sealed class Held : IDisposable
    {
        private AsyncGuarded<T>? _owner;

        internal Held(AsyncGuarded<T> owner)
        {
            _owner = owner;
        }

        public T Value
        {
            get
            {
                return GetOwner()._value;
            }
            set
            {
                GetOwner()._value = value;
            }
        }

        public void Dispose()
        {
            var owner = _owner;
            if (owner is null)
            {
                return;
            }

            _owner = null;
            owner._gate.Release();
        }

        private AsyncGuarded<T> GetOwner()
        {
            if (_owner is null)
            {
                throw new InvalidOperationException("The gate is not held.");
            }

            return _owner;
        }
    }
}
