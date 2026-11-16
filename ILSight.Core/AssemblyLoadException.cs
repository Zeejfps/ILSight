namespace ILSight.Core;

/// <summary>Thrown when a file cannot be opened as a .NET assembly or module: <see cref="Fault"/> is the cause the app
/// words, the message its English diagnostic, and the inner exception, where there is one, what the system or the
/// library said.</summary>
public sealed class AssemblyLoadException(AssemblyPath filePath, AssemblyLoadFault fault, string diagnostic, Exception? inner = null)
    : Exception(diagnostic, inner)
{
    public AssemblyPath FilePath { get; } = filePath;

    public AssemblyLoadFault Fault { get; } = fault;
}
