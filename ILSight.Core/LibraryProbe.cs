namespace ILSight.Core;

/// <summary>What the library's own resolver answered for a reference (<see cref="AssemblyResolver.ProbeLibrary"/>): a
/// file found, none, or a failure that ends the search.</summary>
internal readonly record struct LibraryProbe
{
    public static LibraryProbe Failure => new LibraryProbe { Found = null, Failed = true };

    public required AssemblyPath? Found { get; init; }

    public required bool Failed { get; init; }
}
