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
        return await new AssemblyList { Runtime = TestRuntime.Installed }.Open(path).ReadRootAsync();
    }

    [Fact]
    public async Task Overloads_told_apart_only_by_the_namespace_of_a_parameter_type_are_each_found_again()
    {
        var overloads = (await RootAsync(Trees.PathsBeforePath).GetChildAsync(Namespace, "Overloads").ListChildrenAsync())
            .Where(static c => c.Text.StartsWith("Take(", StringComparison.Ordinal))
            .ToArray();

        await AssertSameLabelAndEachFoundAgainAsync(Trees.PathsBeforePath, overloads);
    }

    [Fact]
    public async Task Types_of_one_name_used_from_two_namespaces_are_each_found_again()
    {
        var widgets = (await RootAsync(Trees.PathsBeforePath).GetChildAsync("References", "PathsLibrary", "Referenced types").ListChildrenAsync())
            .Where(static c => c.Text == "Widget")
            .ToArray();

        await AssertSameLabelAndEachFoundAgainAsync(Trees.PathsBeforePath, widgets);
    }

    [Fact]
    public async Task Methods_of_one_name_two_types_import_from_a_module_are_each_found_again()
    {
        var imports = (await RootAsync(Trees.PathsBeforePath).GetChildAsync("References", "paths-native").ListChildrenAsync())
            .Where(static c => c.Text.StartsWith("Answer(", StringComparison.Ordinal))
            .ToArray();

        await AssertSameLabelAndEachFoundAgainAsync(Trees.PathsBeforePath, imports);
    }

    [Fact]
    public async Task An_event_and_its_backing_field_are_each_found_again()
    {
        var changed = (await RootAsync(Trees.PathsBeforePath).GetChildAsync(Namespace, "Notifier").ListChildrenAsync())
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
        var entries = (await RootAsync(path).GetChildAsync("Metadata", "Debug Directory").ListChildrenAsync())
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
        var places = new[]
        {
            box,
            (await box.ListChildrenAsync()).Single(static c => c.Text.StartsWith("Put(", StringComparison.Ordinal)),
            await ns.GetChildAsync("Shape"),
            (await ns.GetChildAsync("Limits").ListChildrenAsync()).Single(static c => c.Text.StartsWith("Max", StringComparison.Ordinal)),
            (await before.GetChildAsync("Metadata", "Tables").ListChildrenAsync()).Single(static c => c.Text.StartsWith("02 TypeDef", StringComparison.Ordinal)),
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
        var shape = await RootAsync(path).GetChildAsync(Namespace, "Shape");
        File.Copy(Trees.PathsAfterPath, path, overwrite: true);
        var ns = await RootAsync(path).GetChildAsync(Namespace);
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
        var lib = await RootAsync(path).GetChildAsync("lib/net10.0");
        var modules = await lib.ListChildrenAsync();
        var shape = await modules.Single(static c => c.Subject is NodeSubject.Module).GetChildAsync(Namespace, "Shape");
        WritePackage(path, withNotes: true);

        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var found = new[] { await list.FindNodeAsync(lib.Path), await list.FindNodeAsync(shape.Path) };

        Assert.Equal(["net10.0", "Shape"], found.Select(static f => f?.Text));
        Assert.Equal("lib", found[0]?.Parent?.Text);
    }

    /// <summary>Asserts <paramref name="nodes"/> are two nodes of one label, named apart, and each found
    /// again in another list that opens the file at <paramref name="path"/>.</summary>
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

    /// <summary>Gives the first entry of the debug directory of the file at <paramref name="path"/> of type
    /// <paramref name="from"/> the type <paramref name="to"/>.</summary>
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

    /// <summary>A package holding the fixture library under <c>lib/net10.0</c>, which is merged into one
    /// folder unless <paramref name="withNotes"/> puts a file beside it in <c>lib</c>.</summary>
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
