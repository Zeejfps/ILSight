using System.Threading.Channels;

namespace ILSight;

/// <summary>The arguments of later runs waiting for the app. It exists before the app does, so those that arrive
/// while the app starts wait here; the app reads them on the UI thread once it runs.</summary>
public sealed class RunArgumentsInbox
{
    private readonly Channel<RunArguments> _arguments = Channel.CreateUnbounded<RunArguments>();

    /// <summary>Adds <paramref name="arguments"/>, from any thread; false once closed.</summary>
    public bool TryAdd(RunArguments arguments)
    {
        return _arguments.Writer.TryWrite(arguments);
    }

    /// <summary>Refuses arguments from now on, so that the runs sending them open windows of their own.</summary>
    public void Close()
    {
        _arguments.Writer.TryComplete();
    }

    /// <summary>The arguments of each run as they arrive, ending once closed and read.</summary>
    public IAsyncEnumerable<RunArguments> ReadAllAsync()
    {
        return _arguments.Reader.ReadAllAsync();
    }
}
