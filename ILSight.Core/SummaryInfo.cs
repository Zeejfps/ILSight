namespace ILSight.Core;

/// <summary>What the summary of a node without code of its own lists.</summary>
internal abstract record SummaryInfo
{
    private SummaryInfo()
    {
    }

    /// <summary>A comment a line; an empty line is left blank. Read as they are written.</summary>
    public sealed record Comments(IEnumerable<string> Lines) : SummaryInfo;

    public sealed record Namespace(string Name, IReadOnlyList<NamespaceEntry> Entries) : SummaryInfo;

    public sealed record ForwardedType(string FullName, CodeReference? Target, string? Destination) : SummaryInfo;

    /// <summary>A references folder: its assemblies, then its modules.</summary>
    public sealed record References(IReadOnlyList<SummaryLink> Assemblies, IReadOnlyList<string> Modules) : SummaryInfo;

    public sealed record ModuleReference(string Name, bool ContainsMetadata, IReadOnlyList<SummaryLink> Imports) : SummaryInfo;

    public sealed record ImportedMethod(SummaryLink Method, string EntryPoint, string ModuleName) : SummaryInfo;

    /// <summary>A reference of <paramref name="Module"/>, with where it resolves from.</summary>
    public sealed record AssemblyReference(LoadedModule Module, ICSharpCode.Decompiler.Metadata.AssemblyReference Reference) : SummaryInfo;

    /// <summary>What an assembly uses from a reference, each type with what is used of it under it.</summary>
    public sealed record Used(IReadOnlyList<UsedEntry> Entries) : SummaryInfo;

    /// <summary>A line per type, going to it, as base and derived types are listed.</summary>
    public sealed record Types(IReadOnlyList<SummaryLink> Entries) : SummaryInfo;

    public sealed record Error(Exception Exception) : SummaryInfo;
}
