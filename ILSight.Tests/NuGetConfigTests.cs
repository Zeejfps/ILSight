using ILSight.Core;

namespace ILSight.Tests;

public sealed class NuGetConfigTests
{
    [Fact]
    public void The_enabled_v3_sources_are_listed_in_order()
    {
        using var temp = new TempDir();
        var path = temp.Write("NuGet.Config", """
            <configuration>
              <packageSources>
                <add key="old" value="https://old.test/v3/index.json" />
                <clear />
                <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
                <add key="v2" value="https://v2.test/api/v2" />
                <add key="local" value="C:\packages" />
                <add key="off" value="https://off.test/v3/index.json" />
                <add key="company" value="https://company.test/nuget/v3/index.json" />
              </packageSources>
              <disabledPackageSources>
                <add key="off" value="true" />
              </disabledPackageSources>
            </configuration>
            """);

        var config = ReadConfig(path, CreateEnvironment(null));

        Assert.Equal(["nuget.org", "company"], config.Sources.Select(s => s.Name));
        Assert.Equal("https://company.test/nuget/v3/index.json", config.Sources[1].Url.Text);
    }

    [Fact]
    public void A_missing_config_is_missing()
    {
        using var temp = new TempDir();

        var read = NuGetConfigs.Read(temp.PathOf("NuGet.Config"), CreateEnvironment(null));

        Assert.IsType<NuGetConfigRead.Missing>(read);
    }

    [Fact]
    public void A_config_that_is_no_xml_is_unreadable_saying_why()
    {
        using var temp = new TempDir();
        var path = temp.Write("NuGet.Config", "not xml");

        var read = NuGetConfigs.Read(path, CreateEnvironment(null));

        Assert.NotEmpty(Assert.IsType<NuGetConfigRead.Unreadable>(read).Why);
    }

    [Fact]
    public void A_config_that_clears_its_sources_lists_none()
    {
        using var temp = new TempDir();
        var path = temp.Write("NuGet.Config", """
            <configuration>
              <packageSources>
                <clear />
              </packageSources>
            </configuration>
            """);

        var config = ReadConfig(path, CreateEnvironment(null));

        Assert.Empty(config.Sources);
    }

    private static NuGetConfig ReadConfig(string path, NuGetEnvironment environment)
    {
        return Assert.IsType<NuGetConfigRead.Read>(NuGetConfigs.Read(path, environment)).Config;
    }

    [Fact]
    public void The_global_packages_folder_the_config_names_is_found_from_the_config_s_folder()
    {
        using var temp = new TempDir();
        var path = WriteConfigNamingRestored(temp);

        var config = ReadConfig(path, CreateEnvironment(null));

        Assert.Equal(temp.PathOf("restored"), config.GlobalPackagesFolder);
    }

    [Fact]
    public void An_absolute_nuget_packages_folder_comes_before_the_config_s()
    {
        using var temp = new TempDir();
        var path = WriteConfigNamingRestored(temp);
        var fromEnvironment = Path.Combine(temp.Path, "env", "..", "elsewhere");

        var config = ReadConfig(path, CreateEnvironment(fromEnvironment));

        Assert.Equal(temp.PathOf("elsewhere"), config.GlobalPackagesFolder);
    }

    [Theory]
    [InlineData("packages")]
    [InlineData(@"%USERPROFILE%\.nuget\packages")]
    [InlineData("")]
    public void A_nuget_packages_folder_that_is_no_absolute_path_is_passed_over_for_the_config_s(string fromEnvironment)
    {
        using var temp = new TempDir();
        var path = WriteConfigNamingRestored(temp);

        var config = ReadConfig(path, CreateEnvironment(fromEnvironment));

        Assert.Equal(temp.PathOf("restored"), config.GlobalPackagesFolder);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("packages")]
    public void With_no_absolute_nuget_packages_folder_and_none_in_the_config_the_folder_is_in_the_home_folder(string? fromEnvironment)
    {
        using var temp = new TempDir();
        var path = temp.Write("NuGet.Config", "<configuration />");

        var config = ReadConfig(path, CreateEnvironment(fromEnvironment));

        Assert.Equal(Path.Combine(Home, ".nuget", "packages"), config.GlobalPackagesFolder);
    }

    [Fact]
    public void The_variables_the_config_s_global_packages_folder_names_are_expanded()
    {
        using var temp = new TempDir();
        var path = temp.Write("NuGet.Config", """
        <configuration>
          <config>
            <add key="globalPackagesFolder" value="%PACKAGES_ROOT%/restored" />
          </config>
        </configuration>
        """);
        var environment = CreateEnvironment(null) with { ExpandVariables = text => text.Replace("%PACKAGES_ROOT%", Home) };

        var config = ReadConfig(path, environment);

        Assert.Equal(Path.GetFullPath(Path.Combine(Home, "restored")), config.GlobalPackagesFolder);
    }

    /// <summary>A home folder that need not exist; nothing is read from it.</summary>
    private static string Home => Path.Combine(Path.GetTempPath(), "ilsight-home");

    /// <summary>An environment with <see cref="Home"/> as the home folder, <paramref name="packagesFolder"/> as
    /// <c>NUGET_PACKAGES</c>, and no variables to expand.</summary>
    private static NuGetEnvironment CreateEnvironment(string? packagesFolder)
    {
        return new NuGetEnvironment { HomeFolder = Home, PackagesFolderVariable = packagesFolder, ExpandVariables = static text => text };
    }

    private static string WriteConfigNamingRestored(TempDir temp)
    {
        return temp.Write("NuGet.Config", """
        <configuration>
          <config>
            <add key="globalPackagesFolder" value="restored" />
          </config>
        </configuration>
        """);
    }
}
