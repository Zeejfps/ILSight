using System.Diagnostics.CodeAnalysis;

namespace ILSight;

/// <summary>The right to be the run that later runs with the same settings hand their command lines to.
/// Only the first run gets it, and holds it until it exits; a run that does not get it keeps nothing.</summary>
public sealed class SingleInstanceLock : IDisposable
{
    private readonly Mutex _mutex;

    private SingleInstanceLock(Mutex mutex)
    {
        _mutex = mutex;
    }

    /// <summary>Takes the lock at <paramref name="address"/>; false, with no lock, when another run has it.
    /// Finding out and taking it are one step, so two runs starting together cannot both get it.</summary>
    public static bool TryAcquire(CommandLineAddress address, [NotNullWhen(true)] out SingleInstanceLock? instanceLock)
    {
        var mutex = new Mutex(false, address.Name, out var createdNew);
        if (!createdNew)
        {
            mutex.Dispose();
            instanceLock = null;
            return false;
        }
        instanceLock = new SingleInstanceLock(mutex);
        return true;
    }

    public void Dispose()
    {
        _mutex.Dispose();
    }
}
