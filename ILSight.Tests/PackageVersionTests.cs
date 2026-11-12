using ILSight.Core;

namespace ILSight.Tests;

public sealed class PackageVersionTests
{
    [Theory]
    [InlineData("1.0", "1.0")]
    [InlineData("1.0.0.0", "1.0.0.0")]
    [InlineData("1.0.0-Beta.2", "1.0.0-beta.2")]
    [InlineData("1.0.0-rc+build.5", "1.0.0-rc")]
    public void A_version_NuGet_allows_parses_and_is_filed_in_lower_case_without_its_metadata(string text, string filed)
    {
        Assert.Equal(filed, PackageVersion.Parse(text)?.Filed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("1.0.0.0.0")]
    [InlineData("..")]
    [InlineData("1.0/../..")]
    [InlineData("1.0.0-..")]
    [InlineData("1.0.0\\x")]
    [InlineData("1.0\n")]
    public void A_version_that_could_name_another_folder_does_not_parse(string text)
    {
        Assert.Null(PackageVersion.Parse(text));
    }
}
