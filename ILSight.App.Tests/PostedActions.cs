using System.Collections.Concurrent;
using ZGF.Gui.Immediate;

namespace ILSight.App.Tests;

/// <summary>Actions posted from other threads, queued until the test runs them. A test's drivers
/// share one; it is the UI context only while the drivers run the app's turns (<see cref="Run"/>,
/// <see cref="Install"/>), as the app's loop installs it for its own, so a test's own <c>await</c> never waits
/// on it. Each app's frames are woken when something is posted, so one waiting for its UI to be ready runs a
/// frame, which runs the queue.</summary>
internal sealed class PostedActions
{
    private static readonly AsyncLocal<PostedActions?> s_ofThisTest = new();

    private readonly ConcurrentQueue<Action> _queue = new();
    private readonly object _gate = new();
    private Action[] _wakes = [];
    private readonly UiSynchronizationContext _context;

    private PostedActions()
    {
        _context = new UiSynchronizationContext(Post);
    }

    public bool IsEmpty => _queue.IsEmpty;

    /// <summary>The queue of the test running in this flow: found again after the test awaits, on whatever thread it
    /// continues. xUnit runs every test in a flow of its own, so a test that has not made one yet gets a new queue.</summary>
    public static PostedActions OfThisTest()
    {
        var existing = s_ofThisTest.Value;
        if (existing is not null)
        {
            return existing;
        }

        var posted = new PostedActions();
        s_ofThisTest.Value = posted;
        return posted;
    }

    /// <summary>Makes this queue the thread's UI context for a turn of the app's code, until the scope is disposed.</summary>
    public UiSynchronizationContext.Scope Install()
    {
        return _context.Install();
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
        using var turn = _context.Install();
        while (_queue.TryDequeue(out var action))
        {
            action();
        }
    }
}
