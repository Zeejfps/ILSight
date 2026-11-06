namespace ILSight;

/// <summary>Where a run's arguments go.</summary>
public static class RunArgumentsRouting
{
    /// <summary>Null when the app already running at <paramref name="address"/> took
    /// <paramref name="arguments"/>, and this run has nothing left to do. Otherwise this run opens a
    /// window, with the receiver it gets: one listening already if none was, or one standing by to take
    /// over, whether <paramref name="newInstance"/> asked for that or the running app could not be reached.</summary>
    public static RunArgumentsReceiver? ForwardOrReceive(RunArgumentsAddress address, RunArguments arguments, bool newInstance)
    {
        var receiver = RunArgumentsReceiver.TryClaim(address);
        if (receiver != null)
            return receiver;
        if (newInstance)
            return RunArgumentsReceiver.StandBy(address);
        var handedOver = RunArgumentsPipe.TrySend(address, arguments);
        if (handedOver)
            return null;
        return RunArgumentsReceiver.StandBy(address);
    }
}
