namespace ILSight.Core;

/// <summary>What a project export writes besides the project: the strong-name key it copies into
/// the project and signs with, and a portable PDB beside the project, with the decompiled sources
/// embedded or not.</summary>
public sealed record ProjectExtras(string? StrongNameKeyFile = null, bool Pdb = false, bool EmbedSourcesInPdb = true)
{
    public static ProjectExtras None { get; } = new();
}
