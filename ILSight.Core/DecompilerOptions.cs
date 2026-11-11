using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using ICSharpCode.Decompiler;

namespace ILSight.Core;

/// <summary>The C# version decompiled code is written in: syntax newer than it is not used.</summary>
public enum CSharpVersion
{
    CSharp1 = 1,
    CSharp2 = 2,
    CSharp3 = 3,
    CSharp4 = 4,
    CSharp5 = 5,
    CSharp6 = 6,
    CSharp7 = 7,
    CSharp7_1 = 701,
    CSharp7_2 = 702,
    CSharp7_3 = 703,
    CSharp8 = 800,
    CSharp9 = 900,
    CSharp10 = 1000,
    CSharp11 = 1100,
    CSharp12 = 1200,
    CSharp13 = 1300,
    CSharp14 = 1400,
}

/// <summary>What a <see cref="DecompilerToggle"/> is about.</summary>
public abstract record DecompilerToggleGroup
{
    private DecompilerToggleGroup()
    {
    }

    /// <summary>A C# feature, used only from <see cref="IntroducedIn"/> on.</summary>
    public sealed record Language(CSharpVersion IntroducedIn) : DecompilerToggleGroup;

    public sealed record Other : DecompilerToggleGroup;

    /// <summary>Clean-ups for code the F# compiler emits.</summary>
    public sealed record FSharp : DecompilerToggleGroup;

    /// <summary>How exported projects are laid out; does not change decompiled code.</summary>
    public sealed record ProjectExport : DecompilerToggleGroup;
}

/// <summary>One of the decompiler's on/off choices, named as the decompiler names it; <see cref="All"/>
/// holds every one there is.</summary>
public sealed class DecompilerToggle
{
    public required string Name { get; init; }

    public required DecompilerToggleGroup Group { get; init; }

    public required bool DefaultValue { get; init; }

    /// <summary>Every toggle that changes decompiled code, in the decompiler's order.</summary>
    public static IReadOnlyList<DecompilerToggle> All { get; } = DecompilerToggles.Discover();

    internal static readonly Dictionary<string, DecompilerToggle> ByName = All.ToDictionary(static t => t.Name);

    /// <summary>Whether locals take their names from the assembly's PDB; the one toggle IL reads.</summary>
    public static DecompilerToggle UseDebugSymbols => ByName[nameof(DecompilerSettings.UseDebugSymbols)];

    /// <summary>Whether resolving a reference reads its assembly from a file, rather than only finding
    /// one already listed; applies as assemblies are read.</summary>
    public static DecompilerToggle AutoLoadAssemblyReferences => ByName[nameof(DecompilerSettings.AutoLoadAssemblyReferences)];

    /// <summary>Whether Windows Runtime metadata reads as the .NET types it projects to; applies as
    /// assemblies are read.</summary>
    public static DecompilerToggle ApplyWindowsRuntimeProjections => ByName[nameof(DecompilerSettings.ApplyWindowsRuntimeProjections)];
}

/// <summary>Finding the decompiler's on/off choices, by asking it which it has, and setting them.</summary>
public static class DecompilerToggles
{
    private static readonly Dictionary<string, PropertyInfo> Properties = typeof(DecompilerSettings).GetProperties()
        .Where(static p => p.PropertyType == typeof(bool) && p.CanWrite).ToDictionary(static p => p.Name);

    public static DecompilerToggle? FindToggle(string name)
    {
        return DecompilerToggle.ByName.GetValueOrDefault(name);
    }

    /// <summary>Whether <paramref name="toggle"/> is set away from its default.</summary>
    public static bool IsChanged(DecompilerOptions options, DecompilerToggle toggle)
    {
        return options.Changes.Contains(toggle);
    }

    /// <summary>Whether <paramref name="toggle"/> is on as chosen, whether or not the version allows it.</summary>
    public static bool IsOn(DecompilerOptions options, DecompilerToggle toggle)
    {
        return options.Changes.TryGetValue(toggle, out var value) ? value : toggle.DefaultValue;
    }

    /// <summary>Whether the options' <see cref="DecompilerOptions.LanguageVersion"/> is new enough for
    /// <paramref name="toggle"/>.</summary>
    public static bool Allows(DecompilerOptions options, DecompilerToggle toggle)
    {
        if (toggle.Group is not DecompilerToggleGroup.Language language)
        {
            return true;
        }

        return language.IntroducedIn <= options.LanguageVersion;
    }

    /// <summary>Whether code in <paramref name="language"/> comes out the same with
    /// <paramref name="options"/> as with <paramref name="other"/>.</summary>
    public static bool ProducesSameCode(DecompilerOptions options, DecompilerOptions other, DecompilationLanguage language)
    {
        if (DecompilationLanguages.ShowsCSharp(language))
        {
            return LeaveOutProjectExport(options).Equals(LeaveOutProjectExport(other));
        }

        var symbols = DecompilerToggle.UseDebugSymbols;
        return IsOn(options, symbols) == IsOn(other, symbols);
    }

    /// <summary>The options with every toggle that shapes code back at its default; the project
    /// export toggles, which the export dialog sets, stay as they are.</summary>
    public static DecompilerOptions ResetCodeToggles(DecompilerOptions options)
    {
        var shapingCode = DecompilerToggle.All.Where(static t => t.Group is not DecompilerToggleGroup.ProjectExport);
        return options with { Changes = options.Changes.Without(shapingCode) };
    }

    /// <summary>The options with the project export toggles at their defaults, as they only shape
    /// exported projects, not code.</summary>
    private static DecompilerOptions LeaveOutProjectExport(DecompilerOptions options)
    {
        var shapingProjects = DecompilerToggle.All.Where(static t => t.Group is DecompilerToggleGroup.ProjectExport);
        return options with { Changes = options.Changes.Without(shapingProjects) };
    }

    public static DecompilerOptions SetToggle(DecompilerOptions options, DecompilerToggle toggle, bool value)
    {
        return SetToggle(options, [toggle], value);
    }

    public static DecompilerOptions SetToggle(DecompilerOptions options, IEnumerable<DecompilerToggle> toggles, bool value)
    {
        return options with { Changes = options.Changes.With(toggles, value) };
    }

    internal static void ApplyOptions(DecompilerOptions options, DecompilerSettings settings)
    {
        foreach (var (toggle, value) in options.Changes)
        {
            if (Allows(options, toggle))
            {
                ApplyToggle(toggle, settings, value);
            }
        }
    }

    internal static void ApplyToggle(DecompilerToggle toggle, DecompilerSettings settings, bool value)
    {
        Properties[toggle.Name].SetValue(settings, value);
    }

    internal static List<DecompilerToggle> Discover()
    {
        var defaults = new DecompilerSettings();
        var toggles = new List<DecompilerToggle>();
        foreach (var property in typeof(DecompilerSettings).GetProperties())
        {
            if (property.PropertyType != typeof(bool) || !property.CanWrite)
            {
                continue;
            }

            var browsable = property.GetCustomAttribute<BrowsableAttribute>();
            if (browsable is not null && !browsable.Browsable)
            {
                continue;
            }

            var category = property.GetCustomAttribute<CategoryAttribute>()?.Category;
            if (category is null)
            {
                continue;
            }

            var defaultValue = property.GetValue(defaults);
            if (defaultValue is not bool value)
            {
                throw new UnreachableException($"{property.Name}, a bool property, holds no bool.");
            }

            var group = FindGroup(category);
            if (group is not null)
            {
                toggles.Add(new DecompilerToggle { Name = property.Name, Group = group, DefaultValue = value });
            }
        }
        return toggles;
    }

    /// <summary>The group the decompiler's <paramref name="category"/> stands for, or null for one whose
    /// toggles aren't offered.</summary>
    private static DecompilerToggleGroup? FindGroup(string category)
    {
        var version = FindIntroduced(category);
        if (version is not null)
        {
            return new DecompilerToggleGroup.Language(version.Value);
        }

        return category switch
        {
            "DecompilerSettings.Other" => new DecompilerToggleGroup.Other(),
            "DecompilerSettings.FSpecificOptions" => new DecompilerToggleGroup.FSharp(),
            "DecompilerSettings.ProjectExport" => new DecompilerToggleGroup.ProjectExport(),
            _ => null,
        };
    }

    /// <summary>The version a category such as <c>C# 7.3 / VS 2017.7</c> names.</summary>
    private static CSharpVersion? FindIntroduced(string category)
    {
        if (!category.StartsWith("C# ", StringComparison.Ordinal))
        {
            return null;
        }

        var end = category.IndexOf(' ', 3);
        var parts = category[3..(end < 0 ? category.Length : end)].Split('.');
        if (!int.TryParse(parts[0], out var major))
        {
            return null;
        }

        var minor = parts.Length > 1 && int.TryParse(parts[1], out var m) ? m : 0;
        var value = major >= 8 ? major * 100 + minor : minor == 0 ? major : major * 100 + minor;
        return Enum.IsDefined(typeof(CSharpVersion), value) ? (CSharpVersion)value : null;
    }
}

/// <summary>The choices that change how C# is decompiled. It holds only the toggles set away from
/// their defaults, and keeps them while <see cref="LanguageVersion"/> is too old for them, so they come
/// back with a newer version.</summary>
public sealed record DecompilerOptions
{
    public static DecompilerOptions Default { get; } = new();

    /// <summary>The C# version written.</summary>
    public CSharpVersion LanguageVersion { get; init; } = CSharpVersion.CSharp14;

    /// <summary>The toggles set away from their defaults, by name, and what each is set to.</summary>
    public OnOffChanges Changes { get; internal init; }

    /// <summary>Whether any toggle is set away from its default.</summary>
    public bool HasChanges => Changes.Count > 0;

    /// <summary>Whether <paramref name="other"/> makes the same choices; the same options are equal at once,
    /// without walking the toggles.</summary>
    public bool Equals(DecompilerOptions? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is null)
        {
            return false;
        }

        if (LanguageVersion != other.LanguageVersion)
        {
            return false;
        }

        if (Changes.Count != other.Changes.Count)
        {
            return false;
        }

        foreach (var (toggle, value) in Changes)
        {
            if (!other.Changes.TryGetValue(toggle, out var otherValue) || otherValue != value)
            {
                return false;
            }
        }
        return true;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(LanguageVersion, Changes.Count);
    }
}
