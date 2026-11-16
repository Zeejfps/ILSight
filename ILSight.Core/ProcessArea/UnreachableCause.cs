using System.Diagnostics;

namespace ILSight.Core.ProcessArea;

/// <summary>Why a process could not be asked for its assemblies: a cause the app words in each language. The exception
/// that said it keeps its English message as the diagnostic.</summary>
public abstract record UnreachableCause
{
    private UnreachableCause()
    {
    }

    /// <summary>The process has no diagnostics endpoint to ask.</summary>
    public sealed record NoEndpoint : UnreachableCause;

    /// <summary>The process's runtime didn't answer in the time it was given.</summary>
    public sealed record TimedOut : UnreachableCause;

    /// <summary>The process's runtime refused what it was asked.</summary>
    public sealed record Refused : UnreachableCause;

    /// <summary>The runtime's answer is none the diagnostics protocol allows.</summary>
    public sealed record AnswerMalformed : UnreachableCause;

    /// <summary>Another failure, known only by its message (the system's, or a bug's), said as written.</summary>
    public sealed record Failure(string Message) : UnreachableCause;

    public TResult Match<TResult>(Func<NoEndpoint, TResult> noEndpoint, Func<TimedOut, TResult> timedOut, Func<Refused, TResult> refused,
        Func<AnswerMalformed, TResult> answerMalformed, Func<Failure, TResult> failure)
    {
        return this switch
        {
            NoEndpoint n => noEndpoint(n),
            TimedOut t => timedOut(t),
            Refused r => refused(r),
            AnswerMalformed a => answerMalformed(a),
            Failure f => failure(f),
            _ => throw new UnreachableException($"UnreachableCause.Match does not handle {GetType().Name}."),
        };
    }
}
