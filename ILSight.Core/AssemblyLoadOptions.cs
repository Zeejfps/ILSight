using System.Reflection.Metadata;

namespace ILSight.Core;

/// <summary>The options that apply as assemblies are read, changed together.</summary>
public sealed record AssemblyLoadOptions
{
    public required bool LoadsReferences { get; init; }

    public required MetadataReaderOptions ReaderOptions { get; init; }

    public static AssemblyLoadOptions Default { get; } = new AssemblyLoadOptions
    {
        LoadsReferences = true,
        ReaderOptions = MetadataReaderOptions.ApplyWindowsRuntimeProjections,
    };
}
