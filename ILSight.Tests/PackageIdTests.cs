using ILSight.Core;

namespace ILSight.Tests;

public sealed class PackageIdTests
{
    [Theory]
    [InlineData("Newtonsoft.Json")]
    [InlineData("Microsoft.Extensions.DependencyInjection")]
    [InlineData("my_package-2")]
    public void An_id_NuGet_allows_parses(string text)
    {
        Assert.Equal(text, PackageId.Parse(text)?.Text);
    }

    [Theory]
    [InlineData("")]
    [InlineData("..")]
    [InlineData("..\\x")]
    [InlineData("../x")]
    [InlineData("C:\\x")]
    [InlineData("/x")]
    [InlineData("a..b")]
    [InlineData(".a")]
    [InlineData("a.")]
    [InlineData("a\n")]
    public void An_id_that_could_name_another_folder_does_not_parse(string text)
    {
        Assert.Null(PackageId.Parse(text));
    }

    [Fact]
    public void An_id_longer_than_NuGet_allows_does_not_parse()
    {
        Assert.Null(PackageId.Parse(new string('a', 101)));
    }
}
