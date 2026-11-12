using System.Diagnostics;
using ILSight.Core;
using ILSight.Core.Metadata;

namespace ILSight.Tests;

public sealed class SavedPathTests : IDisposable
{
    /// <summary>A name for each sort of node the tree has.</summary>
    private static readonly string[] Kinds =
    [
        "Assembly", "ReferencesFolder", "AssemblyReference", "ResourcesFolder", "Resource", "Namespace", "Class", "Interface", "Struct",
        "Enum", "Delegate", "Record", "RecordStruct", "BaseTypes", "BaseType", "Field", "Constant", "Property", "Indexer", "Event", "Method",
        "Constructor", "Operator", "Metadata", "MetadataHeader", "MetadataDirectory", "MetadataTables", "MetadataTable", "MetadataHeap",
        "DerivedTypes", "DerivedType", "ResourcesFile", "ResourceImage", "FailedAssembly", "ForwardedType", "Package", "PackageFolder",
        "Bundle", "ReferencedTypes", "TypeReference", "MethodReference", "FieldReference", "PropertyReference", "EventReference",
        "ModuleReference", "ImportedMethod",
    ];

    private readonly TempDir _temp = new();

    public void Dispose()
    {
        _temp.Dispose();
    }

    /// <summary>A record struct, which the fixtures have none of.</summary>
    private readonly record struct Interval(int Start, int End);

    public static IEnumerable<object[]> SavedKinds()
    {
        return Kinds.Select(static k => new object[] { k });
    }

    [Theory]
    [MemberData(nameof(SavedKinds))]
    public void A_path_to_a_node_of_each_kind_finds_it_again_in_another_list_that_reopened_its_file(string kind)
    {
        var node = Sample(kind);
        var reopened = new AssemblyList { Runtime = TestRuntime.Installed };
        AssemblyListing.Open(reopened, node.Path.FilePath);

        var found = reopened.FindNode(node.Path);

        Assert.NotNull(found);
        Assert.NotSame(node, found);
        Assert.Equal((kind, node.Text, node.Path), (KindOf(found), found.Text, found.Path));
    }

    [Fact]
    public void A_path_to_a_file_that_failed_finds_nothing_in_a_list_that_does_not_list_it()
    {
        var failed = Sample("FailedAssembly");

        Assert.Null(new AssemblyList { Runtime = TestRuntime.Installed }.FindNode(failed.Path));
    }

    /// <summary>A node of <paramref name="kind"/>, in a file left in place for another list to open.</summary>
    private BrowseNode Sample(string kind)
    {
        return kind switch
        {
            "Package" or "PackageFolder" => FirstOf(Open(PackageTests.WritePackage(_temp)), kind),
            "Bundle" => Open(BundleTests.WriteBundle(_temp)).Root,
            "FailedAssembly" => Open(_temp.Write("broken.dll", "no assembly")).Root,
            "ForwardedType" => FirstOf(Open(Path.Combine(RuntimeAssemblies.ListDirectories(TestRuntime.Installed)[0], "System.Runtime.dll")), kind),
            _ => FirstOf(Open(Trees.TestAssemblyPath), kind),
        };
    }

    private static LoadedAssembly Open(string path)
    {
        return new AssemblyList { Runtime = TestRuntime.Installed }.Open(path);
    }

    /// <summary>The name of the kind of <paramref name="node"/>.</summary>
    private static string KindOf(BrowseNode node)
    {
        return node.Subject.Match(
            module: static _ => "Assembly",
            @namespace: static _ => "Namespace",
            failedAssembly: static _ => "FailedAssembly",
            typeDefinition: static type => type.Definition.Shape.ToString(),
            memberDefinition: static member => member.Definition.Shape.ToString(),
            references: static _ => "ReferencesFolder",
            assemblyReference: static _ => "AssemblyReference",
            referencedTypes: static _ => "ReferencedTypes",
            typeReference: static _ => "TypeReference",
            memberReference: static member => member.Value.Shape switch
            {
                MemberShape.Field or MemberShape.Constant => "FieldReference",
                MemberShape.Property or MemberShape.Indexer => "PropertyReference",
                MemberShape.Event => "EventReference",
                MemberShape.Method or MemberShape.Constructor or MemberShape.Operator => "MethodReference",
                _ => throw new UnreachableException(),
            },
            moduleReference: static _ => "ModuleReference",
            importedMethod: static _ => "ImportedMethod",
            forwardedType: static _ => "ForwardedType",
            baseTypes: static _ => "BaseTypes",
            baseType: static _ => "BaseType",
            derivedTypes: static _ => "DerivedTypes",
            derivedType: static _ => "DerivedType",
            resources: static _ => "ResourcesFolder",
            resourceContainer: static _ => "ResourcesFile",
            resourceEntry: static entry => entry.Value.IsImage ? "ResourceImage" : "Resource",
            metadata: static _ => "Metadata",
            metadataTables: static _ => "MetadataTables",
            metadataTable: static table => table.Value.Role switch
            {
                MetadataGridRole.Header => "MetadataHeader",
                MetadataGridRole.Directory => "MetadataDirectory",
                MetadataGridRole.Table => "MetadataTable",
                MetadataGridRole.Heap => "MetadataHeap",
                _ => throw new UnreachableException(),
            },
            package: static package => !package.Value.IsRoot ? "PackageFolder" : package.Value.Package.Package.IsBundle ? "Bundle" : "Package",
            packageFile: static file => file.Value.IsImage ? "ResourceImage" : "Resource");
    }

    /// <summary>The first node of <paramref name="kind"/> breadth first from the root of
    /// <paramref name="assembly"/>, not following a reference into the references of the assembly it
    /// resolves to.</summary>
    private static BrowseNode FirstOf(LoadedAssembly assembly, string kind)
    {
        var queue = new Queue<BrowseNode>([assembly.Root]);
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            var nodeKind = KindOf(node);
            if (nodeKind == kind)
            {
                return node;
            }

            foreach (var child in node.Children)
            {
                if (node.Subject is NodeSubject.AssemblyReference && child.Subject is NodeSubject.AssemblyReference)
                {
                    continue;
                }

                queue.Enqueue(child);
            }
        }
        throw new InvalidOperationException($"{assembly.Name} has no node of kind {kind}.");
    }
}
