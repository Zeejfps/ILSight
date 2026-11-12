namespace ILSight;

/// <summary>Given for one reload by <see cref="Reload.Start"/>.</summary>
public sealed class ReloadTicket
{
    public required int Id { get; init; }

    public required CancellationTokenSource Work { get; init; }

    public CancellationToken Token => Work.Token;
}
