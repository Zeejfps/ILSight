namespace ILSight.App.Tests.Unplaced;

/// <summary>A method loading <see cref="NumberLoads"/>' number whose name is the one made unreadable
/// (<see cref="BrokenAssemblies.WritePartlyReadable"/>), so the types of its namespace can't be listed and it is found
/// but not shown. A namespace of its own, so no other type's place fails with it.</summary>
internal static class UnplacedNumberLoad
{
    public static int Unplaced()
    {
        return 7340033;
    }
}
