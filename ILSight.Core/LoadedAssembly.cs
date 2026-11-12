using ICSharpCode.Decompiler.CSharp.ProjectDecompiler;

namespace ILSight.Core;

/// <summary>
/// A file known to an <see cref="AssemblyList"/>: one the user opened, or one loaded to resolve a
/// reference. It is a <see cref="LoadedModule"/> that was read, a <see cref="FailedAssembly"/> that
/// could not be, or a <see cref="LoadedPackage"/> whose files are browsed; no other kind can be made.
/// Made by <see cref="LoadedAssemblies"/>, whose functions work out what these hold from the file.
/// A <see cref="BrowseSession"/> keeps the nodes of its tree for as long as this object lives.
/// </summary>
public abstract class LoadedAssembly
{
    private protected LoadedAssembly()
    {
    }

    /// <summary>The absolute path of the file this was loaded from; for an assembly read out of a
    /// package, the path through the package, as <see cref="AssemblyPackages.GetFilePath"/> gives it.</summary>
    public required AssemblyPath FilePath { get; init; }

    /// <summary>The item of the list that holds this assembly: itself, or the package it was read out of.</summary>
    public LoadedAssembly ListedItem
    {
        get
        {
            if (this is LoadedModule module && module.Place is not null)
            {
                return module.Place.Container;
            }

            return this;
        }
    }

    /// <summary>The simple name, e.g. <c>System.Private.CoreLib</c>.</summary>
    public required string Name { get; init; }

    /// <summary>The full display name, with version, culture and public key token.</summary>
    public required string FullName { get; init; }

    public required Version? Version { get; init; }

    /// <summary>The simple name with the version, e.g. <c>System.Runtime (10.0.0.0)</c>.</summary>
    public required string NameAndVersion { get; init; }

    public required AssemblyList List { get; init; }

    /// <summary>The name a portable PDB of this assembly is written under: its name, cleaned up as a file name.</summary>
    public string PdbFileName => WholeProjectDecompiler.CleanUpFileName(Name, DebugSymbols.PdbExtension);

    public override string ToString()
    {
        return FullName;
    }
}
