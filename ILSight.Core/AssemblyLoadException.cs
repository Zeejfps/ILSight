namespace ILSight.Core;

/// <summary>Thrown when a file cannot be opened as a .NET assembly or module.</summary>
public sealed class AssemblyLoadException(AssemblyPath filePath, string message, Exception? inner = null) : Exception(message, inner)
{
    public AssemblyPath FilePath { get; } = filePath;
}
