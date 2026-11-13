using System.Threading.Channels;

namespace ILSight.ILSightAppArea;

/// <summary>
/// The role of taking the arguments later runs send. One run at a time at an address holds it; the
/// others either hand over and leave, or stand by to take it when the holder goes. Holding it and
/// listening are one thing here, so a listener cannot outlive the role: the server stops before the
/// role is released, because stopping it removes the name the next receiver listens on. The receiver keeps that
/// rule itself: it holds the role's mutex, the thread that holds it and what stops it, privately, and
/// <see cref="Dispose"/> stops, joins and releases.
/// </summary>
/// <remarks>A mutex holds the role because the pipe alone cannot. Unix lets a second server start
/// without complaint, and an exclusive server stays refused after a crash leaves its socket file
/// behind, where a crashed mutex holder frees the mutex. A mutex belongs to the thread that took it,
/// so one thread takes it, waits on the serving and releases it: that thread is edge code.</remarks>
[Edge]
public sealed class RunArgumentsReceiver : IDisposable
{
    private static readonly TimeSpan RoleCheck = TimeSpan.FromMilliseconds(250);

    private readonly string _name;

    private readonly Mutex _role;

    private readonly CancellationTokenSource _stop = new();

    private readonly Thread _thread;

    private bool _isDisposed;

    /// <summary>A receiver of the role at <paramref name="address"/> whose thread does <paramref name="run"/>, not
    /// started yet, putting what later runs send in <paramref name="inbox"/>.</summary>
    private RunArgumentsReceiver(RunArgumentsAddress address, Channel<RunArguments> inbox, Action<RunArgumentsReceiver> run)
    {
        _name = address.Name;
        Inbox = inbox;
        _role = new Mutex(false, address.Name);
        _thread = new Thread(() => run(this)) { IsBackground = true, Name = "Run arguments" };
    }

    /// <summary>The arguments of later runs waiting for the app, after this run's own, which the launcher put there
    /// before the role was claimed. It exists before the app does, so those that arrive while the app starts wait here;
    /// the app reads them on the UI thread once it runs, and completing it refuses arguments from then on, so that the
    /// runs sending them open windows of their own.</summary>
    public Channel<RunArguments> Inbox { get; }

    /// <summary>A receiver already listening when the role at <paramref name="address"/> is free, putting what later
    /// runs send in <paramref name="inbox"/>; null when another run holds it. Never waits for the role: awaits the role
    /// thread's answer.</summary>
    public static async Task<RunArgumentsReceiver?> TryClaimAsync(RunArgumentsAddress address, Channel<RunArguments> inbox, CancellationToken ct)
    {
        var claimed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var receiver = new RunArgumentsReceiver(address, inbox, r => r.ClaimAndServe(claimed));
        receiver._thread.Start();
        bool isClaimed;
        try
        {
            isClaimed = await claimed.Task.WaitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            receiver.Dispose();
            throw;
        }

        if (isClaimed)
        {
            return receiver;
        }

        receiver.Dispose();
        return null;
    }

    /// <summary>A receiver that takes the role at <paramref name="address"/> whenever it comes free, putting what later
    /// runs send in <paramref name="inbox"/>. A run must only stand by once it will not send, or it could end up taking
    /// its own arguments.</summary>
    public static RunArgumentsReceiver StandBy(RunArgumentsAddress address, Channel<RunArguments> inbox)
    {
        var receiver = new RunArgumentsReceiver(address, inbox, static r => r.WaitAndServe());
        receiver._thread.Start();
        return receiver;
    }

    /// <summary>Stops listening, then releases the role, which the next receiver can then take; once.</summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _stop.Cancel();
        _thread.Join();
        _stop.Dispose();
        _role.Dispose();
    }

    private void ClaimAndServe(TaskCompletionSource<bool> claimed)
    {
        var holdsRole = TryTakeRole(TimeSpan.Zero);
        claimed.SetResult(holdsRole);
        if (holdsRole)
        {
            ServeThenRelease();
        }
    }

    private void WaitAndServe()
    {
        while (!_stop.IsCancellationRequested)
        {
            if (TryTakeRole(RoleCheck))
            {
                ServeThenRelease();
                return;
            }
        }
    }

    private bool TryTakeRole(TimeSpan wait)
    {
        try
        {
            return _role.WaitOne(wait);
        }
        catch (AbandonedMutexException)
        {
            // The holder crashed, and the wait took the role all the same.
            return true;
        }
    }

    private void ServeThenRelease()
    {
        try
        {
#pragma warning disable VSTHRD002 // Only the thread that took the role's named Mutex may release it, and holding it is this thread's job; the serving's continuations run on the thread pool.
            RunArgumentsPipe.ServeAsync(_name, Inbox, _stop.Token).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
        }
        finally
        {
            _role.ReleaseMutex();
        }
    }
}
