using System.Diagnostics;

namespace ILSight.Core.Metadata;

/// <summary>What is shown under the selected row of a <see cref="MetadataGrid"/>.</summary>
public abstract record MetadataRowDetails
{
    private MetadataRowDetails()
    {
    }

    /// <summary>A decoded text, with the file extension of the language it is in, if known.</summary>
    public sealed record Text(string Content, string? Extension) : MetadataRowDetails;

    /// <summary>A decoded list, as a table.</summary>
    public sealed record Table(IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows) : MetadataRowDetails;

    public TResult Match<TResult>(Func<Text, TResult> text, Func<Table, TResult> table)
    {
        return this switch
        {
            Text t => text(t),
            Table t => table(t),
            _ => throw new UnreachableException($"MetadataRowDetails.Match does not handle {GetType().Name}."),
        };
    }

    /// <summary><see cref="Match{TResult}"/> handing <paramref name="state"/> to the handlers, so that a
    /// caller drawing every frame can pass static lambdas and allocate nothing.</summary>
    public TResult Match<TState, TResult>(TState state, Func<TState, Text, TResult> text, Func<TState, Table, TResult> table)
    {
        return this switch
        {
            Text t => text(state, t),
            Table t => table(state, t),
            _ => throw new UnreachableException($"MetadataRowDetails.Match does not handle {GetType().Name}."),
        };
    }

    /// <summary>Runs the handler for this case with <paramref name="state"/>, for effects that differ per
    /// case; static lambdas allocate nothing.</summary>
    public void Switch<TState>(TState state, Action<TState, Text> text, Action<TState, Table> table)
    {
        switch (this)
        {
            case Text t:
                text(state, t);
                break;
            case Table t:
                table(state, t);
                break;
            default:
                throw new UnreachableException($"MetadataRowDetails.Switch does not handle {GetType().Name}.");
        }
    }
}
