using System.IO.Pipes;

namespace ILSight;

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
public sealed class RunArgumentsReceiver : IDisposable
{
    private static readonly TimeSpan RoleCheck = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan BrokenPipeWait = TimeSpan.FromMilliseconds(100);

    private readonly string _name;
    private readonly Mutex _role;
    private readonly CancellationTokenSource _stop = new();
    private readonly Thread _thread;

    private RunArgumentsReceiver(RunArgumentsAddress address, Action<RunArgumentsReceiver> run)
    {
        _name = address.Name;
        _role = new Mutex(false, address.Name);
        _thread = new Thread(() => run(this)) { IsBackground = true, Name = "Run arguments" };
        _thread.Start();
    }

    /// <summary>Where the arguments taken go.</summary>
    public RunArgumentsInbox Inbox { get; } = new();

    /// <summary>A receiver already listening when the role at <paramref name="address"/> is free; null
    /// when another run holds it. Never waits for the role.</summary>
    public static RunArgumentsReceiver? TryClaim(RunArgumentsAddress address)
    {
        var claimed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var receiver = new RunArgumentsReceiver(address, r => r.ClaimAndServe(claimed));
        if (claimed.Task.GetAwaiter().GetResult())
            return receiver;
        receiver.Dispose();
        return null;
    }

    /// <summary>A receiver that takes the role at <paramref name="address"/> whenever it comes free.
    /// A run must only stand by once it will not send, or it could end up taking its own arguments.</summary>
    public static RunArgumentsReceiver StandBy(RunArgumentsAddress address)
    {
        return new RunArgumentsReceiver(address, static r => r.WaitAndServe());
    }

    private void ClaimAndServe(TaskCompletionSource<bool> claimed)
    {
        var holdsRole = TryTakeRole(TimeSpan.Zero);
        claimed.SetResult(holdsRole);
        if (holdsRole)
            ServeThenRelease();
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
            ServeAsync().GetAwaiter().GetResult();
        }
        finally
        {
            _role.ReleaseMutex();
        }
    }

    /// <summary>Listens until stopped, on one server: replacing it for each run would drop the runs
    /// already waiting in its backlog.</summary>
    private async Task ServeAsync()
    {
        var token = _stop.Token;
        while (!token.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(_name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await ServeRunsAsync(pipe, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }
            catch (Exception e) when (RunArgumentsPipe.IsBroken(e))
            {
                await WaitForBrokenPipe(token);
            }
        }
    }

    private async Task ServeRunsAsync(NamedPipeServerStream pipe, CancellationToken stop)
    {
        while (true)
        {
            await pipe.WaitForConnectionAsync(stop);
            await TakeArgumentsAsync(pipe, stop);
            pipe.Disconnect();
        }
    }

    /// <summary>Takes the arguments of the connected run, then tells it so. A run that goes away partway
    /// costs only its own arguments, and a closed inbox hangs up on it unanswered.</summary>
    private async Task TakeArgumentsAsync(Stream pipe, CancellationToken stop)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop);
        timeout.CancelAfter(RunArgumentsPipe.HandOverTimeout);
        try
        {
            var arguments = await RunArgumentsPipe.ReceiveAsync(pipe, timeout.Token);
            if (Inbox.TryAdd(arguments))
                await RunArgumentsPipe.AcknowledgeAsync(pipe, timeout.Token);
        }
        catch (Exception e) when (RunArgumentsPipe.IsBroken(e) && !stop.IsCancellationRequested)
        {
        }
    }

    /// <summary>Waits a moment after the server broke, so that one that stays broken is not retried in a tight loop.</summary>
    private static async Task WaitForBrokenPipe(CancellationToken token)
    {
        try
        {
            await Task.Delay(BrokenPipeWait, token);
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>Stops listening, then releases the role, which the next receiver can then take.</summary>
    public void Dispose()
    {
        _stop.Cancel();
        _thread.Join();
        _stop.Dispose();
        _role.Dispose();
    }
}
