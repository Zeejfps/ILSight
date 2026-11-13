using ILSight.Core;

namespace ILSight.ExportArea;

/// <summary>How an export's turn in the queue goes: it starts, or it was taken out of the queue
/// before its turn and must not run.</summary>
public abstract record ExportTurn
{
    private ExportTurn()
    {
    }

    /// <summary>What an export works with once its turn comes: the token that cancels it and where it
    /// reports how far it has got. It ends its own turn with <see cref="EndTurn"/>.</summary>
    public sealed record Started : ExportTurn
    {
        private readonly Action _endTurn;

        internal Started(ExportRun run, IProgress<ExportProgress> progress, Action endTurn, CancellationToken token)
        {
            Run = run;
            Token = token;
            Progress = progress;
            _endTurn = endTurn;
        }

        public ExportRun Run { get; }

        public CancellationToken Token { get; }

        public IProgress<ExportProgress> Progress { get; }

        /// <summary>Ends this export's turn, when it is still the running one, and starts the next
        /// waiting export. Ending it again does nothing.</summary>
        public void EndTurn()
        {
            _endTurn();
        }
    }

    /// <summary>The export was taken out of the queue before its turn.</summary>
    public sealed record Withdrawn : ExportTurn;
}
