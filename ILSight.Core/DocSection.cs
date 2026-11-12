using System.Diagnostics;

namespace ILSight.Core;

/// <summary>One part of an entity's documentation, by what it says: a parameter, type parameter or exception
/// part is about the one it names.</summary>
public abstract record DocSection
{
    private DocSection()
    {
    }

    public required IReadOnlyList<DocBlock> Blocks { get; init; }

    /// <summary>Which part this is; the parts are shown in its order.</summary>
    public abstract DocSectionKind Kind { get; }

    /// <summary>The parameter, type parameter or exception the part is about; null for a part about no one
    /// thing, or when the documentation names none.</summary>
    public virtual DocRun? Label => null;

    public sealed record Summary : DocSection
    {
        public override DocSectionKind Kind => DocSectionKind.Summary;
    }

    public sealed record TypeParameter : DocSection
    {
        /// <summary>The type parameter's name; null when the documentation gives none.</summary>
        public required DocRun? Name { get; init; }

        public override DocSectionKind Kind => DocSectionKind.TypeParameter;

        public override DocRun? Label => Name;
    }

    public sealed record Parameter : DocSection
    {
        /// <summary>The parameter's name; null when the documentation gives none.</summary>
        public required DocRun? Name { get; init; }

        public override DocSectionKind Kind => DocSectionKind.Parameter;

        public override DocRun? Label => Name;
    }

    public sealed record Returns : DocSection
    {
        public override DocSectionKind Kind => DocSectionKind.Returns;
    }

    public sealed record Value : DocSection
    {
        public override DocSectionKind Kind => DocSectionKind.Value;
    }

    public sealed record Exception : DocSection
    {
        /// <summary>The exception's type, by its short name; null when the documentation names none.</summary>
        public required DocRun? Name { get; init; }

        public override DocSectionKind Kind => DocSectionKind.Exception;

        public override DocRun? Label => Name;
    }

    public sealed record Remarks : DocSection
    {
        public override DocSectionKind Kind => DocSectionKind.Remarks;
    }

    public sealed record Example : DocSection
    {
        public override DocSectionKind Kind => DocSectionKind.Example;
    }

    public TResult Match<TResult>(Func<Summary, TResult> summary, Func<TypeParameter, TResult> typeParameter, Func<Parameter, TResult> parameter,
        Func<Returns, TResult> returns, Func<Value, TResult> value, Func<Exception, TResult> exception, Func<Remarks, TResult> remarks,
        Func<Example, TResult> example)
    {
        return this switch
        {
            Summary s => summary(s),
            TypeParameter t => typeParameter(t),
            Parameter p => parameter(p),
            Returns r => returns(r),
            Value v => value(v),
            Exception e => exception(e),
            Remarks r => remarks(r),
            Example e => example(e),
            _ => throw new UnreachableException($"DocSection.Match does not handle {GetType().Name}."),
        };
    }
}
