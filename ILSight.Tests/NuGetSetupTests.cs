using ILSight.Core;

namespace ILSight.Tests;

public sealed class NuGetSetupTests
{
    [Fact]
    public void A_typed_url_is_a_source_named_after_its_host()
    {
        var source = NuGetSource.FromUrl(" https://pkgs.test/v3/index.json ");

        Assert.Equal(("pkgs.test", "https://pkgs.test/v3/index.json"), (source?.Name, source?.Url.Text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("nuget.org")]
    [InlineData("C:\\packages")]
    [InlineData("ftp://pkgs.test/index.json")]
    public void Text_that_is_no_web_url_is_no_source(string typed)
    {
        Assert.Null(NuGetSource.FromUrl(typed));
    }

    [Fact]
    public void Sources_at_one_url_typed_in_other_letters_are_the_same_feed()
    {
        var lower = NuGetSource.FromUrl("https://pkgs.test/v3/index.json");
        var upper = NuGetSource.FromUrl("https://PKGS.test/V3/Index.json");
        var other = NuGetSource.FromUrl("https://pkgs.test/v2/index.json");

        Assert.NotNull(lower);
        Assert.NotNull(upper);
        Assert.NotNull(other);
        Assert.Equal(lower, upper);
        Assert.Equal(lower.Url.GetHashCode(), upper.Url.GetHashCode());
        Assert.NotEqual(lower, other);
    }

    [Fact]
    public void A_client_makes_a_feed_once_per_url_whatever_its_letters()
    {
        var typed = NuGetSource.FromUrl("https://PKGS.test/v3/index.json");
        var listed = NuGetSource.FromConfig("company", "https://pkgs.test/v3/index.json");
        Assert.NotNull(typed);
        Assert.NotNull(listed);
        var client = new NuGetClient(new FakeFeed());

        var feed = client.EnsureFeed(typed);

        Assert.Same(feed, client.EnsureFeed(listed));
        Assert.Same(typed, feed.Source);
    }

    [Fact]
    public void A_source_the_config_lists_is_named_as_the_config_names_it()
    {
        using var temp = new TempDir();
        var typed = NuGetSource.FromUrl("https://PKGS.test/v3/index.json");
        var listed = NuGetSource.FromConfig("company", "https://pkgs.test/v3/index.json");
        var other = NuGetSource.FromUrl("https://other.test/v3/index.json");
        Assert.NotNull(typed);
        Assert.NotNull(listed);
        Assert.NotNull(other);
        var setup = new NuGetSetup { Sources = [listed], Cache = new PackageCache { GlobalPackagesFolder = temp.PathOf("global"), DownloadFolder = temp.PathOf("downloads") } };

        Assert.Same(listed, NuGetFeeds.GetListedSource(setup, typed));
        Assert.Same(other, NuGetFeeds.GetListedSource(setup, other));
    }
}
