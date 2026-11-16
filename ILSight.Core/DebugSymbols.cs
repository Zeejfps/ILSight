// Adapted from ILSpy's PortableDebugInfoProvider and DebugInfoUtils (MIT License, Copyright (c) AlphaSierraPapa for the SharpDevelop Team).

using System.Reflection.Metadata;
using ICSharpCode.Decompiler.Metadata;

namespace ILSight.Core;

/// <summary>
/// The portable PDB that goes with an assembly: the one picked for it, else the one embedded in it,
/// else the one next to it whose id matches its CodeView entry; next to it in the package, for an
/// assembly read out of one. The decompiler takes local names from it, and the tree shows its
/// metadata beside the assembly's. Shared by every read of the assembly: each hands the decompiler a
/// <see cref="DebugSymbolsReader"/> of its own, which holds that read's count.
/// </summary>
public sealed class DebugSymbols
{
    internal const string PdbExtension = ".pdb";

    /// <summary>The PDB's own metadata.</summary>
    public required MetadataFile File { get; init; }

    /// <summary>What the tree calls <see cref="File"/>.</summary>
    public required string Title { get; init; }

    public required string Description { get; init; }

    public string SourceFileName => File.FileName;

    public MetadataReader Metadata => File.Metadata;
}
