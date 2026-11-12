using System.Diagnostics;

namespace ILSight.Core.Metadata;

/// <summary>What is shown under the selected row of a <see cref="MetadataGrid"/>.</summary>
public abstract record MetadataRowDetails
{
    private MetadataRowDetails()
    {
    }

    /// <summary>A decoded text, with the file extension of the language it is in, if known.</summary>
    public sealed record Text : MetadataRowDetails
    {
        public required string Content { get; init; }

        public required string? Extension { get; init; }
    }

    /// <summary>A decoded list, as a table.</summary>
    public sealed record Table : MetadataRowDetails
    {
        public required IReadOnlyList<string> Headers { get; init; }

        public required IReadOnlyList<IReadOnlyList<string>> Rows { get; init; }
    }

    /// <summary>Why the row's details could not be read: what the library threw reading them.</summary>
    public sealed record Failed(string Reason) : MetadataRowDetails;

    public TResult Match<TResult>(Func<Text, TResult> text, Func<Table, TResult> table, Func<Failed, TResult> failed)
    {
        return this switch
        {
            Text t => text(t),
            Table t => table(t),
            Failed f => failed(f),
            _ => throw new UnreachableException($"MetadataRowDetails.Match does not handle {GetType().Name}."),
        };
    }

    /// <summary><see cref="Match{TResult}"/> handing <paramref name="state"/> to the handlers, so that a
    /// caller drawing every frame can pass static lambdas and allocate nothing.</summary>
    public TResult Match<TState, TResult>(TState state, Func<TState, Text, TResult> text, Func<TState, Table, TResult> table,
        Func<TState, Failed, TResult> failed)
    {
        return this switch
        {
            Text t => text(state, t),
            Table t => table(state, t),
            Failed f => failed(state, f),
            _ => throw new UnreachableException($"MetadataRowDetails.Match does not handle {GetType().Name}."),
        };
    }

    /// <summary>Runs the handler for this case with <paramref name="state"/>, for effects that differ per
    /// case; static lambdas allocate nothing.</summary>
    public void Switch<TState>(TState state, Action<TState, Text> text, Action<TState, Table> table, Action<TState, Failed> failed)
    {
        switch (this)
        {
            case Text t:
                text(state, t);
                break;
            case Table t:
                table(state, t);
                break;
            case Failed f:
                failed(state, f);
                break;
            default:
                throw new UnreachableException($"MetadataRowDetails.Switch does not handle {GetType().Name}.");
        }
    }
}
