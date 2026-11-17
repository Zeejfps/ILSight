namespace ILSight.Core.ProcessArea;

/// <summary>Why a process could not be asked for its assemblies: a cause the app words in each language. The exception
/// that said it keeps its English message as the diagnostic.</summary>
public union UnreachableCause(UnreachableCause.NoEndpoint, UnreachableCause.TimedOut, UnreachableCause.Refused,
    UnreachableCause.AnswerMalformed, UnreachableCause.Failure)
{
    /// <summary>The process has no diagnostics endpoint to ask.</summary>
    public sealed record NoEndpoint;

    /// <summary>The process's runtime didn't answer in the time it was given.</summary>
    public sealed record TimedOut;

    /// <summary>The process's runtime refused what it was asked.</summary>
    public sealed record Refused;

    /// <summary>The runtime's answer is none the diagnostics protocol allows.</summary>
    public sealed record AnswerMalformed;

    /// <summary>Another failure, known only by its message (the system's, or a bug's), said as written.</summary>
    public sealed record Failure(string Message);
}
