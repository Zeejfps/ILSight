using ILSight.Core;

namespace ILSight.Tests;

public sealed class PackageCacheTests
{
    private static readonly byte[] PackageBytes = [1, 2, 3];
    private static readonly PackageId Tiny = Packages.Id("Tiny");
    private static readonly PackageId Gone = Packages.Id("Gone");
    private static readonly PackageVersion OneZero = Packages.Version("1.0.0");

    [Fact]
    public async Task A_package_not_on_disk_is_downloaded_from_its_lower_case_url_and_kept()
    {
        using var temp = new TempDir();
        var fake = new FakeFeed();
        fake.Answer(FakeFeed.PackagesUrl + "tiny/1.0.0-beta/tiny.1.0.0-beta.nupkg", PackageBytes);
        var cache = new PackageCache { GlobalPackagesFolder = temp.PathOf("global"), DownloadFolder = temp.PathOf("downloads") };

        var path = await NuGetFeeds.GetPackageAsync(cache, NuGetFeeds.CreateFeed(fake.Source, fake), Tiny, Packages.Version("1.0.0-Beta+build.5"));

        Assert.Equal(temp.PathOf(Path.Combine("downloads", "tiny", "1.0.0-beta", "tiny.1.0.0-beta.nupkg")), path);
        Assert.Equal(PackageBytes, await File.ReadAllBytesAsync(path));
        Assert.Equal(path, NuGetFeeds.FindPackage(cache, Tiny, Packages.Version("1.0.0-beta")));
    }

    [Fact]
    public async Task A_package_a_restore_put_in_the_global_folder_is_opened_from_there()
    {
        using var temp = new TempDir();
        var restored = temp.Write(Path.Combine("global", "tiny", "1.0.0", "tiny.1.0.0.nupkg"), PackageBytes);
        var fake = new FakeFeed();
        var cache = new PackageCache { GlobalPackagesFolder = temp.PathOf("global"), DownloadFolder = temp.PathOf("downloads") };

        var path = await NuGetFeeds.GetPackageAsync(cache, NuGetFeeds.CreateFeed(fake.Source, fake), Tiny, OneZero);

        Assert.Equal(restored, path);
        Assert.Empty(fake.Requested);
    }

    [Fact]
    public async Task A_package_the_feed_does_not_have_leaves_nothing_behind()
    {
        using var temp = new TempDir();
        var fake = new FakeFeed();
        var cache = new PackageCache { GlobalPackagesFolder = temp.PathOf("global"), DownloadFolder = temp.PathOf("downloads") };

        await Assert.ThrowsAsync<HttpRequestException>(() => NuGetFeeds.GetPackageAsync(cache, NuGetFeeds.CreateFeed(fake.Source, fake), Gone, OneZero));

        Assert.Null(NuGetFeeds.FindPackage(cache, Gone, OneZero));
        Assert.False(File.Exists(temp.PathOf(Path.Combine("downloads", "gone", "1.0.0", "gone.1.0.0.nupkg.tmp"))));
    }
}
