namespace ILSight.App.Tests;

/// <summary>Methods that load one number, for a search of a module read only in part
/// (<see cref="BrokenAssemblies.WritePartlyReadable"/>): <see cref="Shown"/> is found and shown; the body of
/// <see cref="Unreadable"/> is the one made unreadable. <see cref="Unplaced.UnplacedNumberLoad"/> loads it too.</summary>
internal static class NumberLoads
{
    public static int Shown()
    {
        return 7340033;
    }

    public static int Unreadable()
    {
        return 7340033;
    }
}
