using System.Collections.Concurrent;
using ZGF.Gui.Immediate;

namespace ILSight.App.Tests;

/// <summary>Actions posted from other threads, queued until the test runs them. A test's drivers
/// share one, which is also where <c>await</c> on the test's thread continues. Each app's frames are woken
/// when something is posted, so one waiting for its UI to be ready runs a frame, which runs the queue.</summary>
internal sealed class PostedActions
{
    [ThreadStatic] private static PostedActions? _ofThisTest;

    private readonly ConcurrentQueue<Action> _queue = new();
    private readonly object _gate = new();
    private Action[] _wakes = [];
    private readonly UiSynchronizationContext _context;

    private PostedActions()
    {
        _context = new UiSynchronizationContext(Post);
    }

    public bool IsEmpty => _queue.IsEmpty;

    /// <summary>The queue of the test running on this thread. xUnit gives every test a context of
    /// its own, so a test that has not made one yet gets a new queue.</summary>
    public static PostedActions OfThisTest()
    {
        var existing = _ofThisTest;
        if (existing is not null && SynchronizationContext.Current == existing._context)
        {
            return existing;
        }

        var posted = new PostedActions();
        SynchronizationContext.SetSynchronizationContext(posted._context);
        _ofThisTest = posted;
        return posted;
    }

    public void Post(Action action)
    {
        _queue.Enqueue(action);
        Action[] wakes;
        lock (_gate)
        {
            wakes = _wakes;
        }

        foreach (var wake in wakes)
        {
            wake();
        }
    }

    /// <summary>Has <paramref name="wake"/> called, from any thread, whenever something is posted.</summary>
    public void WakeOnPost(Action wake)
    {
        lock (_gate)
        {
            _wakes = [.. _wakes, wake];
        }
    }

    /// <summary>Runs what was posted so far, and what that posts.</summary>
    public void Run()
    {
        while (_queue.TryDequeue(out var action))
        {
            action();
        }
    }
}
