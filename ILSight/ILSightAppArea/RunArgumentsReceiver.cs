using System.IO.Pipes;
using System.Threading.Channels;

namespace ILSight.ILSightAppArea;

/// <summary>
/// The role of taking the arguments later runs send. One run at a time at an address holds it; the
/// others either hand over and leave, or stand by to take it when the holder goes. Holding it and
/// listening are one thing here, so a listener cannot outlive the role: the server stops before the
/// role is released, because stopping it removes the name the next receiver listens on.
/// </summary>
/// <remarks>A mutex holds the role because the pipe alone cannot. Unix lets a second server start
/// without complaint, and an exclusive server stays refused after a crash leaves its socket file
/// behind, where a crashed mutex holder frees the mutex. A mutex belongs to the thread that took it,
/// so one thread takes it, waits on the serving and releases it.</remarks>
/// <remarks>IDisposable demands <see cref="Dispose"/>, which forwards to <see cref="RunArgumentsPipe"/>.</remarks>
public sealed class RunArgumentsReceiver : IDisposable
{
    public required string Name { get; init; }
    public required Mutex Role { get; init; }
    public readonly CancellationTokenSource Stop = new();

    /// <summary>What listens, set once the receiver it runs over is made; there is one by the time anything can
    /// dispose the receiver.</summary>
    public Thread? Thread;

    /// <summary>The arguments of later runs waiting for the app. It exists before the app does, so those that
    /// arrive while the app starts wait here; the app reads them on the UI thread once it runs, and completing
    /// it refuses arguments from then on, so that the runs sending them open windows of their own.</summary>
    public Channel<RunArguments> Inbox { get; } = Channel.CreateUnbounded<RunArguments>();

    /// <summary>Stops listening, then releases the role, which the next receiver can then take.</summary>
    public void Dispose()
    {
        RunArgumentsPipe.StopReceiver(this);
    }
}
