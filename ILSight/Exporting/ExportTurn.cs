namespace ILSight.Exporting;

/// <summary>How an export's turn in the queue goes: it starts, or it was taken out of the queue
/// before its turn and must not run.</summary>
public abstract record ExportTurn
{
    private protected ExportTurn()
    {
    }
}
