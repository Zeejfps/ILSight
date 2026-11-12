using System.Diagnostics;

namespace ILSight.Core;

/// <summary>What the summary of a node without code of its own lists.</summary>
internal abstract record SummaryInfo
{
    private SummaryInfo()
    {
    }

    /// <summary>A comment a line; an empty line is left blank. Read as they are written.</summary>
    public sealed record Comments(IEnumerable<string> Lines) : SummaryInfo;

    public sealed record Namespace : SummaryInfo
    {
        public required string Name { get; init; }

        public required IReadOnlyList<NamespaceEntry> Entries { get; init; }
    }

    public sealed record ForwardedType : SummaryInfo
    {
        public required string FullName { get; init; }

        public required CodeReference? Target { get; init; }

        public required string? Destination { get; init; }
    }

    /// <summary>A references folder: its assemblies, then its modules.</summary>
    public sealed record References : SummaryInfo
    {
        public required IReadOnlyList<SummaryLink> Assemblies { get; init; }

        public required IReadOnlyList<string> Modules { get; init; }
    }

    public sealed record ModuleReference : SummaryInfo
    {
        public required string Name { get; init; }

        public required bool ContainsMetadata { get; init; }

        public required IReadOnlyList<SummaryLink> Imports { get; init; }
    }

    public sealed record ImportedMethod : SummaryInfo
    {
        public required SummaryLink Method { get; init; }

        public required string EntryPoint { get; init; }

        public required string ModuleName { get; init; }
    }

    /// <summary>A reference of <see cref="Module"/>, with where it resolves from.</summary>
    public sealed record AssemblyReference : SummaryInfo
    {
        public required LoadedModule Module { get; init; }

        public required ICSharpCode.Decompiler.Metadata.AssemblyReference Reference { get; init; }
    }

    /// <summary>What an assembly uses from a reference, each type with what is used of it under it.</summary>
    public sealed record Used(IReadOnlyList<UsedEntry> Entries) : SummaryInfo;

    /// <summary>A line per type, going to it, as base and derived types are listed.</summary>
    public sealed record Types(IReadOnlyList<SummaryLink> Entries) : SummaryInfo;

    public sealed record Error(Exception Exception) : SummaryInfo;

    /// <summary>Does what the handler of whichever case this is does with it and <paramref name="state"/>, which
    /// it is handed, so that a handler that needs the caller's values can be static and allocate nothing.</summary>
    public void Switch<TState>(TState state, Action<TState, Comments> comments, Action<TState, Namespace> @namespace,
        Action<TState, ForwardedType> forwardedType, Action<TState, References> references,
        Action<TState, ModuleReference> moduleReference, Action<TState, ImportedMethod> importedMethod,
        Action<TState, AssemblyReference> assemblyReference, Action<TState, Used> used, Action<TState, Types> types,
        Action<TState, Error> error)
    {
        switch (this)
        {
            case Comments c:
                comments(state, c);
                return;
            case Namespace c:
                @namespace(state, c);
                return;
            case ForwardedType c:
                forwardedType(state, c);
                return;
            case References c:
                references(state, c);
                return;
            case ModuleReference c:
                moduleReference(state, c);
                return;
            case ImportedMethod c:
                importedMethod(state, c);
                return;
            case AssemblyReference c:
                assemblyReference(state, c);
                return;
            case Used c:
                used(state, c);
                return;
            case Types c:
                types(state, c);
                return;
            case Error c:
                error(state, c);
                return;
            default:
                throw new UnreachableException($"SummaryInfo.Switch does not handle {GetType().Name}.");
        }
    }
}
