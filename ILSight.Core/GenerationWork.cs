namespace ILSight.Core;

/// <summary>Work begun in one generation of an <see cref="AssemblyList"/>, as the list hands it out
/// (<see cref="AssemblyList.Work"/>): the <see cref="Generation"/> it began in, and that generation's ending
/// (<see cref="Ending"/>), handed out together under the list's lock so that the work never takes one generation's number
/// with another's ending. The edge links <see cref="Ending"/> into the work's own token and hands the work to what
/// commits it, which takes its generation from here.</summary>
public sealed class GenerationWork
{
    private readonly CancellationTokenSource _ending;

    internal GenerationWork(int generation, CancellationTokenSource ending)
    {
        Generation = generation;
        _ending = ending;
    }

    /// <summary>The generation the work began in: it commits only while this is the list's.</summary>
    public int Generation { get; }

    /// <summary>Cancelled once the generation ends: by the end, after it releases the list's lock, or first by work that
    /// met the end before then (<see cref="EndSuperseded"/>). Asked by the edge directly, beside the token it links it
    /// into, since a linked token is reached only once the cancel has run its callbacks.</summary>
    public CancellationToken Ending => _ending.Token;

    /// <summary>The work's end once its commit was refused because the generation ended: the generation's ending is
    /// cancelled (idempotent: the end that took it out cancels it too) and the cancellation of <see cref="Ending"/> made,
    /// for the work to throw.</summary>
    internal OperationCanceledException EndSuperseded(string what)
    {
        _ending.Cancel();
        return new OperationCanceledException($"The list's generation {Generation} ended while {what}.", Ending);
    }
}
