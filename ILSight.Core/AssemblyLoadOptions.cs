using System.Reflection.Metadata;

namespace ILSight.Core;

/// <summary>The options that apply as assemblies are read, changed together.</summary>
public sealed record AssemblyLoadOptions(bool LoadsReferences, MetadataReaderOptions ReaderOptions)
{
    public static AssemblyLoadOptions Default { get; } = new(true, MetadataReaderOptions.ApplyWindowsRuntimeProjections);
}
