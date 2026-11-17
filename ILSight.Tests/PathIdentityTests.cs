using System.IO.Compression;
using System.Reflection.PortableExecutable;
using ILSight.Core;

namespace ILSight.Tests;

/// <summary>Paths name nodes by what they stand for: nodes that share a label are each found again, and a
/// rebuild that changes their labels or kinds leaves them found.</summary>
public sealed class PathIdentityTests : IDisposable
{
    private const string Namespace = "PathsSample";

    private readonly TempDir _temp = new();

    public void Dispose()
    {
        _temp.Dispose();
    }

    private static async Task<BrowseNode> RootAsync(string path)
    {
        var assembly = new AssemblyList { Runtime = TestRuntime.Installed }.Open(path);
        return await assembly.ReadRootAsync();
    }

    [Fact]
    public async Task Overloads_told_apart_only_by_the_namespace_of_a_parameter_type_are_each_found_again()
    {
        var root = await RootAsync(Trees.PathsBeforePath);
        var overloadsNode = await root.GetChildAsync(Namespace, "Overloads");
        var children = await overloadsNode.ListChildrenAsync();
        var overloads = children
            .Where(static c => c.Text.StartsWith("Take(", StringComparison.Ordinal))
            .ToArray();

        await AssertSameLabelAndEachFoundAgainAsync(Trees.PathsBeforePath, overloads);
    }

    [Fact]
    public async Task Types_of_one_name_used_from_two_namespaces_are_each_found_again()
    {
        var root = await RootAsync(Trees.PathsBeforePath);
        var referencedTypes = await root.GetChildAsync("References", "PathsLibrary", "Referenced types");
        var children = await referencedTypes.ListChildrenAsync();
        var widgets = children
            .Where(static c => c.Text == "Widget")
            .ToArray();

        await AssertSameLabelAndEachFoundAgainAsync(Trees.PathsBeforePath, widgets);
    }

    [Fact]
    public async Task Methods_of_one_name_two_types_import_from_a_module_are_each_found_again()
    {
        var root = await RootAsync(Trees.PathsBeforePath);
        var pathsNative = await root.GetChildAsync("References", "paths-native");
        var children = await pathsNative.ListChildrenAsync();
        var imports = children
            .Where(static c => c.Text.StartsWith("Answer(", StringComparison.Ordinal))
            .ToArray();

        await AssertSameLabelAndEachFoundAgainAsync(Trees.PathsBeforePath, imports);
    }

    [Fact]
    public async Task An_event_and_its_backing_field_are_each_found_again()
    {
        var root = await RootAsync(Trees.PathsBeforePath);
        var notifier = await root.GetChildAsync(Namespace, "Notifier");
        var children = await notifier.ListChildrenAsync();
        var changed = children
            .Where(static c => c.Text.StartsWith("Changed", StringComparison.Ordinal))
            .ToArray();

        await AssertSameLabelAndEachFoundAgainAsync(Trees.PathsBeforePath, changed);
        Assert.Equal([MemberShape.Field, MemberShape.Event], changed.Select(static c => c.MemberShape).Order());
    }

    [Fact]
    public async Task Two_debug_directory_entries_of_one_type_are_each_found_again()
    {
        var path = _temp.Copy(Trees.PathsBeforePath, "PathsSample.dll");
        RetypeDebugEntry(path, DebugDirectoryEntryType.PdbChecksum, DebugDirectoryEntryType.CodeView);
        var root = await RootAsync(path);
        var debugDirectory = await root.GetChildAsync("Metadata", "Debug Directory");
        var children = await debugDirectory.ListChildrenAsync();
        var entries = children
            .Where(static c => c.Text == nameof(DebugDirectoryEntryType.CodeView))
            .ToArray();

        await AssertSameLabelAndEachFoundAgainAsync(path, entries);
    }

    [Fact]
    public async Task A_place_is_found_again_after_a_rebuild_changes_its_label_or_kind()
    {
        var path = _temp.Copy(Trees.PathsBeforePath, "PathsSample.dll");
        var before = await RootAsync(path);
        var ns = await before.GetChildAsync(Namespace);
        var box = await ns.GetChildAsync("Box<T>");
        var children = await box.ListChildrenAsync();
        var shape = await ns.GetChildAsync("Shape");
        var limits = await ns.GetChildAsync("Limits");
        var limitsChildren = await limits.ListChildrenAsync();
        var tablesNode = await before.GetChildAsync("Metadata", "Tables");
        var tables = await tablesNode.ListChildrenAsync();
        var places = new[]
        {
            box,
            children.Single(static c => c.Text.StartsWith("Put(", StringComparison.Ordinal)),
            shape,
            limitsChildren.Single(static c => c.Text.StartsWith("Max", StringComparison.Ordinal)),
            tables.Single(static c => c.Text.StartsWith("02 TypeDef", StringComparison.Ordinal)),
        };
        File.Copy(Trees.PathsAfterPath, path, overwrite: true);

        var after = new AssemblyList { Runtime = TestRuntime.Installed };
        after.Open(path);
        var found = new List<BrowseNode?>();
        foreach (var place in places)
        {
            found.Add(await after.FindNodeAsync(place.Path));
        }

        Assert.All(found, Assert.NotNull);
        var foundNodes = found.OfType<BrowseNode>().ToArray();
        Assert.Equal(places.Select(static p => p.Path), foundNodes.Select(static f => f.Path));
        Assert.Equal(["Box<TItem>", "Put(TItem) : void"], foundNodes.Take(2).Select(static f => f.Text));
        Assert.Equal(TypeShape.Record, foundNodes[2].TypeShape);
        Assert.Equal(MemberShape.Constant, foundNodes[3].MemberShape);
        Assert.NotEqual(places[4].Text, foundNodes[4].Text);
    }

    [Fact]
    public async Task A_node_of_a_rebuilt_file_is_the_counterpart_of_the_node_it_was_before()
    {
        var path = _temp.Copy(Trees.PathsBeforePath, "PathsSample.dll");
        var root = await RootAsync(path);
        var shape = await root.GetChildAsync(Namespace, "Shape");
        File.Copy(Trees.PathsAfterPath, path, overwrite: true);
        var rebuiltRoot = await RootAsync(path);
        var ns = await rebuiltRoot.GetChildAsync(Namespace);
        await ns.EnsureChildrenAsync();

        var counterpart = TestSession.Shared.FindLoadedChildLike(ns, shape);

        Assert.NotNull(counterpart);
        Assert.Equal(TypeShape.Record, counterpart.TypeShape);
    }

    [Fact]
    public async Task A_place_in_a_package_is_found_again_when_its_folders_are_merged_otherwise()
    {
        var path = _temp.Write("Sample.nupkg", []);
        WritePackage(path, withNotes: false);
        var root = await RootAsync(path);
        var lib = await root.GetChildAsync("lib/net10.0");
        var modules = await lib.ListChildrenAsync();
        var shape = await modules.Single(static c => c.Subject is NodeSubject.Module).GetChildAsync(Namespace, "Shape");
        WritePackage(path, withNotes: true);

        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var found = new[] { await list.FindNodeAsync(lib.Path), await list.FindNodeAsync(shape.Path) };

        Assert.Equal(["net10.0", "Shape"], found.Select(static f => f?.Text));
        Assert.Equal("lib", found[0]?.Parent?.Text);
    }

    private static async Task AssertSameLabelAndEachFoundAgainAsync(string path, BrowseNode[] nodes)
    {
        Assert.Equal(2, nodes.Length);
        Assert.Equal(nodes[0].Text, nodes[1].Text);
        Assert.NotEqual(nodes[0].Path, nodes[1].Path);
        var reopened = new AssemblyList { Runtime = TestRuntime.Installed };
        reopened.Open(path);

        var found = new List<BrowseNode?>();
        foreach (var node in nodes)
        {
            found.Add(await reopened.FindNodeAsync(node.Path));
        }

        Assert.All(found, Assert.NotNull);
        Assert.NotSame(found[0], found[1]);
        Assert.Equal(nodes.Select(static n => n.Path), found.OfType<BrowseNode>().Select(static f => f.Path));
    }

    private static void RetypeDebugEntry(string path, DebugDirectoryEntryType from, DebugDirectoryEntryType to)
    {
        const int entrySize = 28;
        const int typeOffset = 12;
        var bytes = File.ReadAllBytes(path);
        using (var reader = new PEReader(new MemoryStream(bytes)))
        {
            var header = reader.PEHeaders.PEHeader;
            Assert.NotNull(header);
            var directory = header.DebugTableDirectory;
            Assert.True(reader.PEHeaders.TryGetDirectoryOffset(directory, out var start));
            var at = reader.ReadDebugDirectory().ToList().FindIndex(e => e.Type == from);
            Assert.True(at >= 0);
            BitConverter.TryWriteBytes(bytes.AsSpan(start + at * entrySize + typeOffset), (int)to);
        }
        File.WriteAllBytes(path, bytes);
    }

    // lib/net10.0 is merged into one folder unless a file stands beside net10.0 in lib.
    private static void WritePackage(string path, bool withNotes)
    {
        File.Delete(path);
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        archive.CreateEntryFromFile(Trees.PathsBeforePath, "lib/net10.0/PathsSample.dll");
        if (!withNotes)
        {
            return;
        }

        using var notes = new StreamWriter(archive.CreateEntry("lib/notes.txt").Open());
        notes.Write("Notes");
    }
}
