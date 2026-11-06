namespace ILSight;

/// <summary>Where a run's command goes.</summary>
public static class ExternalCommandRouting
{
    /// <summary>Null when the app already running at <paramref name="address"/> took
    /// <paramref name="command"/>, and this run has nothing left to do. Otherwise this run opens a
    /// window, with the receiver it gets: one listening already if none was, or one standing by to take
    /// over, whether <paramref name="newInstance"/> asked for that or the running app could not be reached.</summary>
    public static ExternalCommandReceiver? ForwardOrReceive(ExternalCommandAddress address, ExternalCommand command, bool newInstance)
    {
        var receiver = ExternalCommandReceiver.TryClaim(address);
        if (receiver != null)
            return receiver;
        if (newInstance)
            return ExternalCommandReceiver.StandBy(address);
        var handedOver = ExternalCommandPipe.TrySend(address, command);
        if (handedOver)
            return null;
        return ExternalCommandReceiver.StandBy(address);
    }
}
