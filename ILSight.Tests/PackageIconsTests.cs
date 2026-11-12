using ILSight.Core;

namespace ILSight.Tests;

public sealed class PackageIconsTests
{
    private const string IconUrl = "https://feed.test/icon";

    private static WebUrl Icon => WebUrl.Parse(IconUrl) ?? throw new InvalidOperationException($"Not a URL: '{IconUrl}'");

    [Fact]
    public async Task An_icon_is_fetched_and_drawn_to_fit_the_side_asked_for()
    {
        var fake = new FakeFeed();
        fake.Answer(IconUrl, """<svg xmlns="http://www.w3.org/2000/svg" width="256" height="128"><rect width="256" height="128" fill="red"/></svg>""");
        var icons = NuGetFeeds.CreateIcons(fake);

        var icon = await NuGetFeeds.GetIconAsync(icons, Icon, 64);

        Assert.Equal(64, icon.Width);
        Assert.Equal(32, icon.Height);
    }

    [Fact]
    public async Task What_is_no_picture_cannot_be_an_icon()
    {
        var fake = new FakeFeed();
        fake.Answer(IconUrl, "<html>not found</html>");
        var icons = NuGetFeeds.CreateIcons(fake);

        await Assert.ThrowsAsync<InvalidDataException>(() => NuGetFeeds.GetIconAsync(icons, Icon, 64));
    }

    [Fact]
    public void A_picture_shrinks_by_averaging_what_each_pixel_covers_and_clear_pixels_lend_no_color()
    {
        byte[] rgba =
        [
            255, 0, 0, 255, 0, 0, 255, 255, 0, 255, 0, 0, 0, 255, 0, 0,
            255, 0, 0, 255, 0, 0, 255, 255, 0, 255, 0, 0, 0, 255, 0, 0,
        ];
        var image = new ILSight.Core.Resources.ResourceImage { Width = 4, Height = 2, Rgba = rgba, Format = "PNG" };

        var shrunk = ILSight.Core.Resources.ResourceImages.FitInto(image, 2);

        Assert.Equal(2, shrunk.Width);
        Assert.Equal(1, shrunk.Height);
        Assert.Equal([127, 0, 127, 255, 0, 0, 0, 0], shrunk.Rgba);
    }
}
