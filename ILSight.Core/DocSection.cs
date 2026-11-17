namespace ILSight.Core;

/// <summary>One part of an entity's documentation, by what it says: a parameter, type parameter or exception
/// part is about the one it names.</summary>
public union DocSection(DocSection.Summary, DocSection.TypeParameter, DocSection.Parameter, DocSection.Returns, DocSection.PropertyValue,
    DocSection.Exception, DocSection.Remarks, DocSection.Example)
{
    public sealed record Summary
    {
        public required IReadOnlyList<DocBlock> Blocks { get; init; }
    }

    public sealed record TypeParameter
    {
        public required IReadOnlyList<DocBlock> Blocks { get; init; }

        /// <summary>The type parameter's name; null when the documentation gives none.</summary>
        public required DocRun? Name { get; init; }
    }

    public sealed record Parameter
    {
        public required IReadOnlyList<DocBlock> Blocks { get; init; }

        /// <summary>The parameter's name; null when the documentation gives none.</summary>
        public required DocRun? Name { get; init; }
    }

    public sealed record Returns
    {
        public required IReadOnlyList<DocBlock> Blocks { get; init; }
    }

    /// <summary>A property's value (the documentation's <c>value</c> part): named so as a union's own <c>Value</c> is
    /// the case it holds.</summary>
    public sealed record PropertyValue
    {
        public required IReadOnlyList<DocBlock> Blocks { get; init; }
    }

    public sealed record Exception
    {
        public required IReadOnlyList<DocBlock> Blocks { get; init; }

        /// <summary>The exception's type, by its short name; null when the documentation names none.</summary>
        public required DocRun? Name { get; init; }
    }

    public sealed record Remarks
    {
        public required IReadOnlyList<DocBlock> Blocks { get; init; }
    }

    public sealed record Example
    {
        public required IReadOnlyList<DocBlock> Blocks { get; init; }
    }

    /// <summary>What the part says, block by block.</summary>
    public IReadOnlyList<DocBlock> Blocks => this switch
    {
        Summary summary => summary.Blocks,
        TypeParameter typeParameter => typeParameter.Blocks,
        Parameter parameter => parameter.Blocks,
        Returns returns => returns.Blocks,
        PropertyValue value => value.Blocks,
        Exception exception => exception.Blocks,
        Remarks remarks => remarks.Blocks,
        Example example => example.Blocks,
    };

    /// <summary>Which part this is; the parts are shown in its order.</summary>
    public DocSectionKind Kind => this switch
    {
        Summary => DocSectionKind.Summary,
        TypeParameter => DocSectionKind.TypeParameter,
        Parameter => DocSectionKind.Parameter,
        Returns => DocSectionKind.Returns,
        PropertyValue => DocSectionKind.Value,
        Exception => DocSectionKind.Exception,
        Remarks => DocSectionKind.Remarks,
        Example => DocSectionKind.Example,
    };

    /// <summary>The parameter, type parameter or exception the part is about; null for a part about no one
    /// thing, or when the documentation names none.</summary>
    public DocRun? Label => this switch
    {
        TypeParameter typeParameter => typeParameter.Name,
        Parameter parameter => parameter.Name,
        Exception exception => exception.Name,
        Summary or Returns or PropertyValue or Remarks or Example => null,
    };
}
