namespace ILSight.Core;

/// <summary>A file of the list that could not be read as a .NET assembly, kept listed to show why. It
/// has no metadata: only its <see cref="LoadedAssembly.FilePath"/>, <see cref="LoadedAssembly.Name"/>
/// and <see cref="Error"/> tell anything about it.</summary>
public sealed class FailedAssembly : LoadedAssembly
{
    /// <summary>Why the file could not be read.</summary>
    public required AssemblyLoadException Error { get; init; }
}
