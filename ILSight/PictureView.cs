using ILSight.Core.Resources;

namespace ILSight;

/// <summary>How a tab shows its picture: how far it is zoomed, and whether its markup shows instead.
/// Each new picture starts over at its own size, showing the picture.</summary>
public sealed record PictureView
{
    public const int OwnSizeStep = 2;

    private static readonly float[] Steps = [0.25f, 0.5f, 1f, 2f, 4f, 8f, 16f, 32f];

    /// <summary>The picture being followed.</summary>
    public ResourceImage? Shown { get; set; }

    /// <summary>How far it is zoomed, an index into the steps, which <see cref="PictureViews"/> keeps in range.</summary>
    public int ZoomStep { get; set; } = OwnSizeStep;

    /// <summary>Whether the tab shows the picture's markup instead of the picture.</summary>
    public bool ShowsSource { get; set; }

    public float Scale => Steps[ZoomStep];

    public int Percent => (int)(Scale * 100f);

    public bool CanZoomIn => ZoomStep < Steps.Length - 1;

    public bool CanZoomOut => ZoomStep > 0;

    public bool IsOwnSize => ZoomStep == OwnSizeStep;
}

public static class PictureViews
{
    public static void ZoomIn(PictureView view)
    {
        if (!view.CanZoomIn)
            return;
        view.ZoomStep++;
    }

    public static void ZoomOut(PictureView view)
    {
        if (!view.CanZoomOut)
            return;
        view.ZoomStep--;
    }

    public static void ResetZoom(PictureView view)
    {
        view.ZoomStep = PictureView.OwnSizeStep;
    }

    /// <summary>Starts over when <paramref name="image"/>, the picture the tab shows or null, is
    /// another than the last, and lets go of one the tab no longer shows.</summary>
    public static void Follow(PictureView view, ResourceImage? image)
    {
        if (ReferenceEquals(image, view.Shown))
            return;
        view.Shown = image;
        ResetZoom(view);
        view.ShowsSource = false;
    }

    /// <summary>Switches between the picture and its markup.</summary>
    public static void ToggleSource(PictureView view)
    {
        view.ShowsSource = !view.ShowsSource;
    }
}
