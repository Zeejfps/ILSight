using System.Diagnostics.CodeAnalysis;

namespace ILSight;

/// <summary>What the screen asked of the app state while it drew, in the order asked, for the frame edge to run once
/// the frame is drawn. Writers are handed <see cref="IAppCommandWriter"/>, which only asks; the frame edge takes the
/// commands (<see cref="TryTake"/>).</summary>
public sealed class AppCommandQueue : IAppCommandWriter
{
    private readonly Queue<AppCommand> _commands = new();

    /// <summary>Whether nothing waits to run.</summary>
    public bool IsEmpty => _commands.Count == 0;

    public void Enqueue(AppCommand command)
    {
        _commands.Enqueue(command);
    }

    /// <summary>Takes the oldest command asked for, if any. The frame edge's.</summary>
    public bool TryTake([MaybeNullWhen(false)] out AppCommand command)
    {
        return _commands.TryDequeue(out command);
    }
}
