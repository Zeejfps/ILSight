namespace ILSight.Core;

/// <summary>What the summary of a node without code of its own lists.</summary>
internal union SummaryInfo(SummaryInfo.Comments, SummaryInfo.Namespace, SummaryInfo.ForwardedType, SummaryInfo.References,
    SummaryInfo.ModuleReference, SummaryInfo.ImportedMethod, SummaryInfo.AssemblyReference, SummaryInfo.Used, SummaryInfo.Types)
{
    /// <summary>A comment a line; an empty line is left blank. Read as they are written.</summary>
    public sealed record Comments(IEnumerable<string> Lines);

    public sealed record Namespace
    {
        public required string Name { get; init; }

        public required IReadOnlyList<NamespaceEntry> Entries { get; init; }
    }

    public sealed record ForwardedType
    {
        public required string FullName { get; init; }

        public required CodeReference? Target { get; init; }

        public required string? Destination { get; init; }
    }

    /// <summary>A references folder: its assemblies, then its modules.</summary>
    public sealed record References
    {
        public required IReadOnlyList<SummaryLink> Assemblies { get; init; }

        public required IReadOnlyList<string> Modules { get; init; }
    }

    public sealed record ModuleReference
    {
        public required string Name { get; init; }

        public required bool ContainsMetadata { get; init; }

        public required IReadOnlyList<SummaryLink> Imports { get; init; }
    }

    public sealed record ImportedMethod
    {
        public required SummaryLink Method { get; init; }

        public required string EntryPoint { get; init; }

        public required string ModuleName { get; init; }
    }

    /// <summary>A reference of <see cref="Module"/>, with where it resolves from.</summary>
    public sealed record AssemblyReference
    {
        public required LoadedModule Module { get; init; }

        public required ICSharpCode.Decompiler.Metadata.AssemblyReference Reference { get; init; }
    }

    /// <summary>What an assembly uses from a reference, each type with what is used of it under it.</summary>
    public sealed record Used(IReadOnlyList<UsedEntry> Entries);

    /// <summary>A line per type, going to it, as base and derived types are listed.</summary>
    public sealed record Types(IReadOnlyList<SummaryLink> Entries);
}
