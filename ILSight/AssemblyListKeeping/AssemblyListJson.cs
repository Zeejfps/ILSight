using ILSight.SettingsFormat;

namespace ILSight.AssemblyListKeeping;

/// <summary>An assembly list as a settings file can hold it. Without a name it is no list.</summary>
internal sealed record AssemblyListJson(
    string? Name,
    List<string?>? Assemblies,
    List<CodeAnchorJson?>? Analyzed,
    Dictionary<string, string?>? Pdbs,
    Dictionary<string, string?>? Frameworks,
    DocumentsJson? Documents);
