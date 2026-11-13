namespace ILSight.MainArea;

/// <summary>Whether the About dialog is open. The dialog widget takes <see cref="IsOpen"/> by <c>ref</c>, so that its
/// close box closes it.</summary>
public sealed class AboutDialog
{
    public bool IsOpen;

    public void Open()
    {
        IsOpen = true;
    }

    public void Close()
    {
        IsOpen = false;
    }
}
