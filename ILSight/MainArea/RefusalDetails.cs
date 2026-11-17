namespace ILSight.MainArea;

/// <summary>The refusal note's details, the whole list its line may show only part of: whether they show, a field
/// their flyout takes by <c>ref</c>, and the showing of a refusal they were opened for, so a newer one shows them closed.</summary>
public sealed class RefusalDetails
{
    public bool IsOpen;

    /// <summary>The showing (<see cref="RefusalLine.Showings"/>) the details were last opened for, or null. A showing,
    /// not the refusal: a refusal is a value, and the same one shown again is a newer one.</summary>
    public int? OpenedFor { get; private set; }

    public void Open(int showing)
    {
        IsOpen = true;
        OpenedFor = showing;
    }

    /// <summary>Told the showing the note shows each frame: details opened for another are closed.</summary>
    public void CloseUnlessFor(int showing)
    {
        if (OpenedFor != showing)
        {
            IsOpen = false;
        }
    }
}
