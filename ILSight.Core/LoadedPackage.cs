namespace ILSight.Core;

/// <summary>A package of the list, whose files are browsed as folders.</summary>
public sealed class LoadedPackage : LoadedAssembly
{
    /// <summary>The package's files.</summary>
    public required AssemblyPackage Package { get; init; }

    /// <summary>The assemblies read out of the package: every assembly file it holds that loads, in
    /// the order its tree shows them. Its reading needs the package it reads for, so
    /// <see cref="LoadedAssemblies.CreatePackage"/> assigns this just after making the package, and nothing
    /// sees the package before that; reading the value this starts with throws.</summary>
    public Lazy<IReadOnlyList<LoadedModule>> Assemblies { get; internal set; } = new(static () => throw new InvalidOperationException("A package is made by LoadedAssemblies.CreatePackage."));
}
