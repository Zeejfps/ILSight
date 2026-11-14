namespace ILSight.MainArea;

/// <summary>The refusal note's details, the whole list its line may show only part of: whether they show, a field
/// their flyout takes by <c>ref</c>, and the refusal they were opened for, so a newer refusal shows them closed.</summary>
public sealed class RefusalDetails
{
    public bool IsOpen;

    /// <summary>The refusal the details were last opened for, or null.</summary>
    public Refusal? OpenedFor { get; private set; }

    public void Open(Refusal refusal)
    {
        IsOpen = true;
        OpenedFor = refusal;
    }

    /// <summary>Told the refusal the note shows each frame: details opened for another are closed.</summary>
    public void CloseUnlessFor(Refusal shown)
    {
        if (!ReferenceEquals(OpenedFor, shown))
        {
            IsOpen = false;
        }
    }
}
