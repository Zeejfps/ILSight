namespace ILSight.ILSightAppArea;

/// <summary>The exit status a run of ILSight ends with: part of its command-line contract, for scripts that start
/// it. A run that opened the window, handed its arguments to the window already open, or stood by ends with
/// <see cref="Success"/>; one whose hand-over was never confirmed (<see cref="RunRoute.Unconfirmed"/>) with
/// <see cref="HandOverUnconfirmed"/>, which is neither success nor a failure's: the arguments may have been taken.</summary>
public static class RunExitCodes
{
    public const int Success = 0;

    /// <summary>The window already open never confirmed it took the arguments; no second window was opened.</summary>
    public const int HandOverUnconfirmed = 2;

    /// <summary>The status a run whose arguments went <paramref name="route"/> ends with.</summary>
    public static int Of(RunRoute route)
    {
        return route == RunRoute.Unconfirmed ? HandOverUnconfirmed : Success;
    }
}
