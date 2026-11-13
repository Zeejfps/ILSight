using System.Xml;
using System.Xml.Linq;

namespace ILSight.Core;

/// <summary>Reading what a NuGet.Config says.</summary>
public static class NuGetConfigs
{
    private const string PackagesFolderVariable = "NUGET_PACKAGES";

    /// <summary>The config of the user running the app: missing, unreadable, or what it says.</summary>
    public static NuGetConfigRead ReadUser()
    {
        var environment = ReadEnvironment();
        return Read(GetUserConfigPath(environment), environment);
    }

    /// <summary>What a config that says nothing means: nuget.org, and the default packages folder.</summary>
    public static NuGetConfig CreateDefault()
    {
        return new NuGetConfig
        {
            Sources = [NuGetSource.NuGetOrg],
            GlobalPackagesFolder = GetGlobalPackagesFolder(null, "", ReadEnvironment()),
        };
    }

    private static NuGetEnvironment ReadEnvironment()
    {
        return new NuGetEnvironment
        {
            HomeFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            PackagesFolderVariable = Environment.GetEnvironmentVariable(PackagesFolderVariable),
            ExpandVariables = Environment.ExpandEnvironmentVariables,
        };
    }

    private static string GetUserConfigPath(NuGetEnvironment environment)
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NuGet", "NuGet.Config");
        }

        return Path.Combine(environment.HomeFolder, ".nuget", "NuGet", "NuGet.Config");
    }

    /// <summary>The config at <paramref name="path"/>, as it reads in <paramref name="environment"/>: missing when there
    /// is no file there, unreadable (why) when it can't be read or is no XML.</summary>
    public static NuGetConfigRead Read(string path, NuGetEnvironment environment)
    {
        XDocument document;
        try
        {
            document = XDocument.Load(path);
        }
        catch (FileNotFoundException)
        {
            return new NuGetConfigRead.Missing();
        }
        catch (DirectoryNotFoundException)
        {
            return new NuGetConfigRead.Missing();
        }
        catch (IOException e)
        {
            return new NuGetConfigRead.Unreadable { Why = e.Message };
        }
        catch (UnauthorizedAccessException e)
        {
            return new NuGetConfigRead.Unreadable { Why = e.Message };
        }
        catch (XmlException e)
        {
            return new NuGetConfigRead.Unreadable { Why = e.Message };
        }

        var sources = ListSources(document);
        var folder = GetGlobalPackagesFolder(document, path, environment);
        return new NuGetConfigRead.Read { Config = new NuGetConfig { Sources = sources, GlobalPackagesFolder = folder } };
    }

    private static List<NuGetSource> ListSources(XDocument document)
    {
        var sources = new List<NuGetSource>();
        var disabled = ListDisabled(document);
        foreach (var entry in ListSection(document, "packageSources"))
        {
            ApplySourceEntry(entry, disabled, sources);
        }

        return sources;
    }

    private static HashSet<string> ListDisabled(XDocument document)
    {
        var disabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in ListSection(document, "disabledPackageSources"))
        {
            if (IsTrue(entry))
            {
                disabled.Add(GetKey(entry));
            }
        }
        return disabled;
    }

    private static bool IsTrue(XElement entry)
    {
        var value = (string?)entry.Attribute("value");
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static void ApplySourceEntry(XElement entry, HashSet<string> disabled, List<NuGetSource> sources)
    {
        if (entry.Name.LocalName == "clear")
        {
            sources.Clear();
            return;
        }
        if (entry.Name.LocalName != "add")
        {
            return;
        }

        var name = GetKey(entry);
        if (disabled.Contains(name))
        {
            return;
        }

        var source = NuGetSource.FromConfig(name, (string?)entry.Attribute("value") ?? "");
        if (source is null)
        {
            return;
        }

        if (IsNameTaken(sources, name))
        {
            return;
        }

        sources.Add(source);
    }

    // NuGet compares source names whatever their case.
    private static bool IsNameTaken(List<NuGetSource> sources, string name)
    {
        return sources.Any(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetGlobalPackagesFolder(XDocument? document, string configPath, NuGetEnvironment environment)
    {
        // NuGet takes the environment's folder only as an absolute path, and expands no variables in it.
        var fromEnvironment = environment.PackagesFolderVariable;
        if (!string.IsNullOrEmpty(fromEnvironment) && Path.IsPathRooted(fromEnvironment))
        {
            return Path.GetFullPath(fromEnvironment);
        }

        var fromConfig = FindConfigValue(document, "globalPackagesFolder");
        if (!string.IsNullOrEmpty(fromConfig))
        {
            return ResolveFrom(configPath, environment.ExpandVariables(fromConfig));
        }

        return Path.Combine(environment.HomeFolder, ".nuget", "packages");
    }

    private static string ResolveFrom(string configPath, string expanded)
    {
        var configFolder = Path.GetDirectoryName(configPath) ?? "";
        return Path.GetFullPath(Path.Combine(configFolder, expanded));
    }

    private static string? FindConfigValue(XDocument? document, string key)
    {
        if (document is null)
        {
            return null;
        }

        var entry = ListSection(document, "config")
            .FirstOrDefault(e => GetKey(e) == key);
        return (string?)entry?.Attribute("value");
    }

    private static IEnumerable<XElement> ListSection(XDocument document, string name)
    {
        var section = document.Root?.Element(name);
        if (section is null)
        {
            return [];
        }

        return section.Elements();
    }

    private static string GetKey(XElement entry)
    {
        return (string?)entry.Attribute("key") ?? "";
    }
}
