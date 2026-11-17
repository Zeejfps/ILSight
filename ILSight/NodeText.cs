using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using ILSight.Core;

namespace ILSight;

/// <summary>How a node reads in the UI: a short name for rows and tabs, its full identity for
/// tooltips and its metadata token, built once per node.</summary>
public static class NodeText
{
    private const string TokenPrefix = "@";
    private const string HexTokenFormat = "X8";
    private const string ForwardedToPrefix = "→ ";
    private const string Separator = " · ";

    private static readonly ConditionalWeakTable<BrowseNode, string> Tooltips = new();
    private static readonly ConditionalWeakTable<BrowseNode, string> Secondaries = new();
    private static readonly ConditionalWeakTable<BrowseNode, string> HexTokens = new();
    private static readonly ConditionalWeakTable<BrowseNode, string> DecimalTokens = new();
    private static readonly ConditionalWeakTable<LoadedAssembly, string> AssemblyLabels = new();

    /// <summary>The node's own name: an assembly's simple name, any other node's label, on one line as every name the
    /// app places in a line of its text (<see cref="OneLineText.Name"/>).</summary>
    public static string Name(BrowseNode node)
    {
        if (node.Subject is NodeSubject.Module module)
        {
            return OneLineText.Name(module.Value.Name);
        }

        return OneLineText.Name(node.Text);
    }

    /// <summary>The node's name as a person reads it in <paramref name="t"/>'s language: a folder the tree makes
    /// (References, Base Types, Resources, ...) in its words, and the row of a module the library couldn't read as the
    /// cause's row says it (<c>cause.module_unreadable.row</c>, the module and the why from the core); any other node's
    /// own name (<see cref="Name"/>), as the metadata names it.</summary>
    public static string Label(Strings t, BrowseNode node)
    {
        return WriteFixedLabel(t, node) ?? Name(node);
    }

    // The labels the app words for nodes the core names by what they are; null for a node named by what it reads.
    private static string? WriteFixedLabel(Strings t, BrowseNode node)
    {
        return node.Subject switch
        {
            NodeSubject.Module or NodeSubject.Namespace or NodeSubject.FailedAssembly or NodeSubject.TypeDefinition
                or NodeSubject.MemberDefinition or NodeSubject.AssemblyReference or NodeSubject.TypeReference or NodeSubject.MemberReference
                or NodeSubject.ModuleReference or NodeSubject.ImportedMethod or NodeSubject.ForwardedType or NodeSubject.BaseType
                or NodeSubject.DerivedType or NodeSubject.ResourceContainer or NodeSubject.ResourceEntry or NodeSubject.Metadata
                or NodeSubject.MetadataTable or NodeSubject.Package or NodeSubject.PackageFile => null,
            NodeSubject.References => t.TreeReferences,
            NodeSubject.ReferencedTypes => t.TreeReferencedTypes,
            NodeSubject.BaseTypes => t.TreeBaseTypes,
            NodeSubject.DerivedTypes => t.TreeDerivedTypes,
            NodeSubject.UnreadableModule unreadable => t.CauseModuleUnreadableRow(name: OneLineText.Name(unreadable.Value.Module.FilePath.FileName),
                why: OneLineText.Message(unreadable.Value.Why.Message)),
            NodeSubject.Resources => t.TreeResources,
            NodeSubject.MetadataTables => t.TreeTables,
        };
    }

    /// <summary>The node's full name as the status bar shows it, in <paramref name="t"/>'s language for a node whose
    /// name the app words (<see cref="Label"/>).</summary>
    public static string FullName(Strings t, BrowseNode node)
    {
        return WriteFixedLabel(t, node) ?? OneLineText.Name(node.FullName);
    }

    /// <summary>A namespace's name as its row shows it nested in another: its last segment, e.g.
    /// <c>Generic</c> for <c>System.Collections.Generic</c>.</summary>
    public static string NestedName(BrowseNode node)
    {
        return OneLineText.Name(NamespaceNames.LastSegment(node.Text));
    }

    /// <summary>The node's full identity: for an assembly its display name, framework and file; for
    /// an entity its full name and, unless public, its accessibility; for a node whose name the app words, that name
    /// in <paramref name="t"/>'s language (<see cref="Label"/>); for an assembly that couldn't be opened, why, in
    /// <paramref name="t"/>'s language.</summary>
    public static string Tooltip(Strings t, BrowseNode node)
    {
        if (node.Subject is NodeSubject.FailedAssembly failed)
        {
            return FailureTexts.DescribeCause(t, failed.Value.Error);
        }

        return WriteFixedLabel(t, node) ?? Tooltips.GetValue(node, Describe);
    }

    /// <summary>What a row shows after the node's name in muted text: an assembly's version and
    /// target framework, e.g. <c>10.0.0.0 · net10.0</c>, the framework left to <see cref="PickedFramework"/>
    /// when one was picked, the assembly a forwarded type is forwarded to, e.g. <c>→ System.Runtime</c>,
    /// the namespace of a type used from a reference, or the type declaring a method imported from a
    /// module. Null for other nodes.</summary>
    public static string? Secondary(BrowseNode node)
    {
        var subject = node.Subject;
        if (subject is NodeSubject.ForwardedType)
        {
            return Secondaries.GetValue(node, WriteForwardedTo);
        }

        if (subject is NodeSubject.TypeReference type)
        {
            return EmptyToNull(OneLineText.Name(type.Value.Namespace));
        }

        if (subject is NodeSubject.ImportedMethod import)
        {
            return OneLineText.Name(import.Value.DeclaringTypeName);
        }

        if (subject is NodeSubject.Module)
        {
            return EmptyToNull(Secondaries.GetValue(node, WriteIdentity));
        }

        return null;
    }

    private static string WriteForwardedTo(BrowseNode node)
    {
        if (node.Subject is not NodeSubject.ForwardedType forwarded)
        {
            throw new UnreachableException();
        }

        return ForwardedToPrefix + OneLineText.Name(forwarded.Value.Destination);
    }

    private static string WriteIdentity(BrowseNode node)
    {
        if (node.Subject is not NodeSubject.Module module)
        {
            throw new UnreachableException();
        }

        return Identity(module.Value);
    }

    private static string? EmptyToNull(string text)
    {
        if (text.Length == 0)
        {
            return null;
        }

        return text;
    }

    /// <summary>The framework picked for an assembly to resolve its references against, as a moniker,
    /// which its row shows bold after <see cref="Secondary"/>; null for any other node, and for an
    /// assembly that resolves against the framework it was built for.</summary>
    public static string? PickedFramework(BrowseNode node)
    {
        if (node.Subject is not NodeSubject.Module module)
        {
            return null;
        }

        if (!module.Value.IsTargetFrameworkPicked)
        {
            return null;
        }

        return MonikerOf(module.Value);
    }

    /// <summary>An assembly as a column of results names it: its name and version, and for one read out
    /// of a package, the folder holding it there, e.g. <c>Newtonsoft.Json (13.0.0.0) · lib/net6.0</c>, each on the column's
    /// one line (<see cref="OneLineText.Name"/>).</summary>
    public static string AssemblyLabel(LoadedAssembly assembly)
    {
        return AssemblyLabels.GetValue(assembly, WriteAssemblyLabel);
    }

    private static string WriteAssemblyLabel(LoadedAssembly assembly)
    {
        if (assembly is not LoadedModule module || module.Place == null)
        {
            return OneLineText.Name(assembly.NameAndVersion);
        }

        return $"{OneLineText.Name(assembly.NameAndVersion)}{Separator}{OneLineText.Name(module.Place.File.Folder)}";
    }

    /// <summary>The node's metadata token as <paramref name="format"/> writes it, e.g. <c>@06000001</c>
    /// in hex. Null when the node has none or tokens are hidden.</summary>
    public static string? Token(BrowseNode node, TokenFormat format)
    {
        var token = node.Token;
        if (token is null)
        {
            return null;
        }

        if (format == TokenFormat.Hex)
        {
            return HexTokens.GetValue(node, WriteHexToken);
        }

        if (format == TokenFormat.Decimal)
        {
            return DecimalTokens.GetValue(node, WriteDecimalToken);
        }

        return null;
    }

    private static string WriteHexToken(BrowseNode node)
    {
        return TokenPrefix + DefinedToken(node).ToString(HexTokenFormat, CultureInfo.InvariantCulture);
    }

    private static string WriteDecimalToken(BrowseNode node)
    {
        return TokenPrefix + DefinedToken(node).ToString(CultureInfo.InvariantCulture);
    }

    private static int DefinedToken(BrowseNode node)
    {
        return node.Token ?? throw new UnreachableException();
    }

    // The framework the assembly was built for as its row shows it, on its one line; null when it names none.
    private static string? MonikerOf(LoadedModule assembly)
    {
        var moniker = assembly.FrameworkMoniker;
        if (moniker is null)
        {
            return null;
        }

        return OneLineText.Name(moniker);
    }

    private static string Identity(LoadedModule assembly)
    {
        var version = assembly.Version?.ToString();
        // A picked framework its row shows bold on its own, so then only the version and the separator before it.
        if (!assembly.IsTargetFrameworkPicked)
        {
            return string.Join(Separator, new[] { version, MonikerOf(assembly) }.OfType<string>());
        }

        if (version is null)
        {
            return "";
        }

        return version + Separator.TrimEnd();
    }

    private static string Describe(BrowseNode node)
    {
        if (node.Subject is NodeSubject.Module module)
        {
            var assembly = module.Value;
            return string.Join("\n", new[] { assembly.FullName, assembly.FrameworkName, assembly.FilePath.Text }.OfType<string>());
        }
        var name = node.FullName;
        var accessibility = Accessibility(node.Visibility.Accessibility);
        return accessibility is not null ? $"{name}\n{accessibility}" : name;
    }

    private static string? Accessibility(NodeAccessibility accessibility)
    {
        return accessibility switch
        {
            NodeAccessibility.Protected => "protected",
            NodeAccessibility.ProtectedInternal => "protected internal",
            NodeAccessibility.Internal => "internal",
            NodeAccessibility.PrivateProtected => "private protected",
            NodeAccessibility.Private => "private",
            _ => null,
        };
    }
}
