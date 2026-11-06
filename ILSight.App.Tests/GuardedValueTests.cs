namespace ILSight.App.Tests;

/// <summary>A value kept valid by its own type stays valid whichever way it was made, and that includes the
/// default value, which skips the constructor.</summary>
public sealed class GuardedValueTests
{
    [Fact]
    public void A_camera_zoom_stays_in_range()
    {
        Assert.Equal(CameraZoom.Max, CameraZoom.Clamp(99f).Value);
        Assert.Equal(CameraZoom.Min, CameraZoom.Clamp(0f).Value);
        Assert.Equal(CameraZoom.Min, CameraZoom.Clamp(-1f).Value);
    }

    [Fact]
    public void A_camera_zoom_nothing_set_or_not_a_number_is_the_own_size()
    {
        Assert.Equal(1f, default(CameraZoom).Value);
        Assert.Equal(1f, CameraZoom.Clamp(float.NaN).Value);
    }

    [Fact]
    public void A_code_font_size_stays_in_range()
    {
        Assert.Equal(CodeFontSize.Max, CodeFontSize.Clamp(900f).Points);
        Assert.Equal(CodeFontSize.Min, CodeFontSize.Clamp(1f).Points);
    }

    [Fact]
    public void A_code_font_size_nothing_set_or_not_a_number_is_in_range()
    {
        Assert.InRange(default(CodeFontSize).Points, CodeFontSize.Min, CodeFontSize.Max);
        Assert.InRange(CodeFontSize.Clamp(float.NaN).Points, CodeFontSize.Min, CodeFontSize.Max);
    }

    [Fact]
    public void A_picture_zoom_nothing_set_is_the_own_size()
    {
        Assert.True(default(PictureZoom).IsOwnSize);
        Assert.Equal(1f, default(PictureZoom).Scale);
    }

    [Fact]
    public void A_picture_zoom_stops_at_either_end()
    {
        var zoomedIn = default(PictureZoom);
        var zoomedOut = default(PictureZoom);
        for (var step = 0; step < 50; step++)
        {
            zoomedIn = zoomedIn.StepIn();
            zoomedOut = zoomedOut.StepOut();
        }

        Assert.Equal(32f, zoomedIn.Scale);
        Assert.False(zoomedIn.CanZoomIn);
        Assert.Equal(0.25f, zoomedOut.Scale);
        Assert.False(zoomedOut.CanZoomOut);
    }

    [Fact]
    public void Picture_zoom_is_no_bigger_than_the_number_it_guards()
    {
        Assert.Equal(sizeof(int), System.Runtime.CompilerServices.Unsafe.SizeOf<PictureZoom>());
        Assert.Equal(sizeof(float), System.Runtime.CompilerServices.Unsafe.SizeOf<CameraZoom>());
        Assert.Equal(sizeof(float), System.Runtime.CompilerServices.Unsafe.SizeOf<CodeFontSize>());
    }
}
