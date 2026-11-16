using System.Diagnostics;
using ILSight.Core;
using ILSight.Core.MetadataArea;

namespace ILSight.Tests;

public sealed class SavedPathTests : IDisposable
{
    // Every sort of node the tree has but the row naming a module a search for derived types could not read, which a
    // list holding only the base's file doesn't reach (BrowseTreeTests).
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

    // The fixtures have no record struct.
    private readonly record struct Interval
    {
        public required int Start { get; init; }

        public required int End { get; init; }
    }

    public static IEnumerable<object[]> SavedKinds()
    {
        return Kinds.Select(static k => new object[] { k });
    }

    [Theory]
    [MemberData(nameof(SavedKinds))]
    public async Task A_path_to_a_node_of_each_kind_finds_it_again_in_another_list_that_reopened_its_file(string kind)
    {
        var node = await SampleAsync(kind);
        var reopened = new AssemblyList { Runtime = TestRuntime.Installed };
        AssemblyListing.Open(reopened, node.Path.FilePath);

        var found = await reopened.FindNodeAsync(node.Path);

        Assert.NotNull(found);
        Assert.NotSame(node, found);
        Assert.Equal((kind, node.Text, node.Path), (KindOf(found), found.Text, found.Path));
    }

    [Fact]
    public async Task A_path_to_a_file_there_that_cannot_be_read_and_is_not_listed_is_a_failed_lookup_saying_why()
    {
        var failed = await SampleAsync("FailedAssembly");

        var found = await NodeLookup.FindAsync(new AssemblyList { Runtime = TestRuntime.Installed }, TestSession.Shared, failed.Path, CancellationToken.None);

        var why = Assert.IsType<AssemblyLoadException>(Assert.IsType<Outcome<Lookup<BrowseNode>>.Failed>(found).Error);
        Assert.IsType<BadImageFormatException>(why.InnerException);
    }

    [Fact]
    public async Task A_path_to_a_file_gone_finds_nothing()
    {
        var failed = await SampleAsync("FailedAssembly");
        File.Delete(failed.Path.FilePath.Text);

        Assert.Null(await new AssemblyList { Runtime = TestRuntime.Installed }.FindNodeAsync(failed.Path));
    }

    // The file is left in place for another list to open.
    private async Task<BrowseNode> SampleAsync(string kind)
    {
        switch (kind)
        {
            case "Package" or "PackageFolder":
            {
                var packagePath = PackageTests.WritePackage(_temp);
                var package = Open(packagePath);
                return await FirstOfAsync(package, kind);
            }
            case "Bundle":
            {
                var bundlePath = BundleTests.WriteBundle(_temp);
                var bundle = Open(bundlePath);
                return await bundle.ReadRootAsync();
            }
            case "FailedAssembly":
            {
                var brokenPath = _temp.Write("broken.dll", "no assembly");
                var broken = Open(brokenPath);
                return await broken.ReadRootAsync();
            }
            case "ForwardedType":
            {
                var runtime = Open(Path.Combine(RuntimeAssemblies.ListDirectories(TestRuntime.Installed)[0], "System.Runtime.dll"));
                return await FirstOfAsync(runtime, kind);
            }
            default:
            {
                var assembly = Open(Trees.TestAssemblyPath);
                return await FirstOfAsync(assembly, kind);
            }
        }
    }

    private static LoadedAssembly Open(string path)
    {
        return new AssemblyList { Runtime = TestRuntime.Installed }.Open(path);
    }

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
            unreadableModule: static _ => "UnreadableModule",
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

    private static async Task<BrowseNode> FirstOfAsync(LoadedAssembly assembly, string kind)
    {
        var queue = new Queue<BrowseNode>([await assembly.ReadRootAsync()]);
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            var nodeKind = KindOf(node);
            if (nodeKind == kind)
            {
                return node;
            }

            foreach (var child in await node.ListChildrenAsync())
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
