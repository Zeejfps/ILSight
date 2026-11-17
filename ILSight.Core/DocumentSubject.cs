namespace ILSight.Core;

/// <summary>What a <see cref="DocumentRequest"/>'s document is written of: a module's preview, a type's or member's code,
/// a summary, a resource, BAML, or why a listed file could not be read.</summary>
internal union DocumentSubject(DocumentSubject.ModulePreview, DocumentSubject.Type, DocumentSubject.Member, DocumentSubject.Summary,
    DocumentSubject.Resource, DocumentSubject.Baml, DocumentSubject.Failure)
{
    /// <summary>A module's summary and attributes, not its types.</summary>
    public sealed record ModulePreview
    {
        public required LoadedModule Module { get; init; }
    }

    public sealed record Type
    {
        public required DefinedType Definition { get; init; }
    }

    public sealed record Member
    {
        public required DefinedMember Definition { get; init; }
    }

    /// <summary>What a node without code of its own lists, read from <see cref="Module"/>, or
    /// from none for a package's.</summary>
    public sealed record Summary
    {
        public required SummaryInfo Info { get; init; }

        public required LoadedModule? Module { get; init; }
    }

    /// <summary>A resource as its text, or a hex dump of its bytes, which <see cref="Read"/> reads.</summary>
    public sealed record Resource
    {
        public required LoadedModule? Module { get; init; }

        public required string Description { get; init; }

        public required Func<byte[]> Read { get; init; }
    }

    /// <summary>WPF's compiled XAML, as the XAML it was compiled from.</summary>
    public sealed record Baml
    {
        public required LoadedModule Module { get; init; }

        public required byte[] Data { get; init; }
    }

    /// <summary>Why a listed file could not be read.</summary>
    public sealed record Failure
    {
        public required FailedAssembly Assembly { get; init; }
    }

    /// <summary>The module the document is read from; null for a package's.</summary>
    public LoadedModule? Module => this switch
    {
        ModulePreview preview => preview.Module,
        Type type => type.Definition.Module,
        Member member => member.Definition.Module,
        Summary summary => summary.Module,
        Resource resource => resource.Module,
        Baml baml => baml.Module,
        Failure => null,
    };

    /// <summary>Whether writing the document reads its module's type system: a module's preview, a type's or member's
    /// code; not a summary, resource or BAML.</summary>
    public bool IsReadThroughTypeSystem => this switch
    {
        ModulePreview or Type or Member => true,
        Summary or Resource or Baml or Failure => false,
    };
}
