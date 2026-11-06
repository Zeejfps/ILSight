using System.Threading.Channels;

namespace ILSight;

/// <summary>The commands waiting for the app. It exists before the app does, so a command that arrives
/// while the app starts waits here; the app reads them on the UI thread once it runs.</summary>
public sealed class ExternalCommandInbox
{
    private readonly Channel<ExternalCommand> _commands = Channel.CreateUnbounded<ExternalCommand>();

    /// <summary>Adds <paramref name="command"/>, from any thread; false once closed.</summary>
    public bool TryAdd(ExternalCommand command)
    {
        return _commands.Writer.TryWrite(command);
    }

    /// <summary>Refuses commands from now on, so that the runs sending them open windows of their own.</summary>
    public void Close()
    {
        _commands.Writer.TryComplete();
    }

    /// <summary>The commands as they arrive, ending once closed and read.</summary>
    public IAsyncEnumerable<ExternalCommand> ReadAllAsync()
    {
        return _commands.Reader.ReadAllAsync();
    }
}
