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

    private static BrowseNode Root(string path)
    {
        return new AssemblyList { Runtime = TestRuntime.Installed }.Open(path).Root;
    }

    [Fact]
    public void Overloads_told_apart_only_by_the_namespace_of_a_parameter_type_are_each_found_again()
    {
        var overloads = Root(Trees.PathsBeforePath).Child(Namespace, "Overloads").Children
            .Where(static c => c.Text.StartsWith("Take(", StringComparison.Ordinal))
            .ToArray();

        AssertSameLabelAndEachFoundAgain(Trees.PathsBeforePath, overloads);
    }

    [Fact]
    public void Types_of_one_name_used_from_two_namespaces_are_each_found_again()
    {
        var widgets = Root(Trees.PathsBeforePath).Child("References", "PathsLibrary", "Referenced types").Children
            .Where(static c => c.Text == "Widget")
            .ToArray();

        AssertSameLabelAndEachFoundAgain(Trees.PathsBeforePath, widgets);
    }

    [Fact]
    public void Methods_of_one_name_two_types_import_from_a_module_are_each_found_again()
    {
        var imports = Root(Trees.PathsBeforePath).Child("References", "paths-native").Children
            .Where(static c => c.Text.StartsWith("Answer(", StringComparison.Ordinal))
            .ToArray();

        AssertSameLabelAndEachFoundAgain(Trees.PathsBeforePath, imports);
    }

    [Fact]
    public void An_event_and_its_backing_field_are_each_found_again()
    {
        var changed = Root(Trees.PathsBeforePath).Child(Namespace, "Notifier").Children
            .Where(static c => c.Text.StartsWith("Changed", StringComparison.Ordinal))
            .ToArray();

        AssertSameLabelAndEachFoundAgain(Trees.PathsBeforePath, changed);
        Assert.Equal([MemberShape.Field, MemberShape.Event], changed.Select(static c => c.MemberShape).Order());
    }

    [Fact]
    public void Two_debug_directory_entries_of_one_type_are_each_found_again()
    {
        var path = _temp.Copy(Trees.PathsBeforePath, "PathsSample.dll");
        RetypeDebugEntry(path, DebugDirectoryEntryType.PdbChecksum, DebugDirectoryEntryType.CodeView);
        var entries = Root(path).Child("Metadata", "Debug Directory").Children
            .Where(static c => c.Text == nameof(DebugDirectoryEntryType.CodeView))
            .ToArray();

        AssertSameLabelAndEachFoundAgain(path, entries);
    }

    [Fact]
    public void A_place_is_found_again_after_a_rebuild_changes_its_label_or_kind()
    {
        var path = _temp.Copy(Trees.PathsBeforePath, "PathsSample.dll");
        var before = Root(path);
        var ns = before.Child(Namespace);
        var box = ns.Child("Box<T>");
        var places = new[]
        {
            box,
            box.Children.Single(static c => c.Text.StartsWith("Put(", StringComparison.Ordinal)),
            ns.Child("Shape"),
            ns.Child("Limits").Children.Single(static c => c.Text.StartsWith("Max", StringComparison.Ordinal)),
            before.Child("Metadata", "Tables").Children.Single(static c => c.Text.StartsWith("02 TypeDef", StringComparison.Ordinal)),
        };
        File.Copy(Trees.PathsAfterPath, path, overwrite: true);

        var after = new AssemblyList { Runtime = TestRuntime.Installed };
        after.Open(path);
        var found = places.Select(p => after.FindNode(BrowseNodes.GetPath(p))).ToArray();

        Assert.All(found, Assert.NotNull);
        var foundNodes = found.OfType<BrowseNode>().ToArray();
        Assert.Equal(places.Select(static p => BrowseNodes.GetPath(p)), foundNodes.Select(static f => BrowseNodes.GetPath(f)));
        Assert.Equal(["Box<TItem>", "Put(TItem) : void"], foundNodes.Take(2).Select(static f => f.Text));
        Assert.Equal(TypeShape.Record, foundNodes[2].TypeShape);
        Assert.Equal(MemberShape.Constant, foundNodes[3].MemberShape);
        Assert.NotEqual(places[4].Text, foundNodes[4].Text);
    }

    [Fact]
    public void A_node_of_a_rebuilt_file_is_the_counterpart_of_the_node_it_was_before()
    {
        var path = _temp.Copy(Trees.PathsBeforePath, "PathsSample.dll");
        var shape = Root(path).Child(Namespace, "Shape");
        File.Copy(Trees.PathsAfterPath, path, overwrite: true);
        var ns = Root(path).Child(Namespace);
        ns.EnsureChildren();

        var counterpart = BrowseSessions.FindLoadedChildLike(TestSession.Shared, ns, shape);

        Assert.NotNull(counterpart);
        Assert.Equal(TypeShape.Record, counterpart.TypeShape);
    }

    [Fact]
    public void A_place_in_a_package_is_found_again_when_its_folders_are_merged_otherwise()
    {
        var path = _temp.Write("Sample.nupkg", []);
        WritePackage(path, withNotes: false);
        var lib = Root(path).Child("lib/net10.0");
        var shape = lib.Children.Single(static c => c.Subject is NodeSubject.Module).Child(Namespace, "Shape");
        WritePackage(path, withNotes: true);

        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var found = new[] { list.FindNode(BrowseNodes.GetPath(lib)), list.FindNode(BrowseNodes.GetPath(shape)) };

        Assert.Equal(["net10.0", "Shape"], found.Select(static f => f?.Text));
        Assert.Equal("lib", found[0]?.Parent?.Text);
    }

    /// <summary>Asserts <paramref name="nodes"/> are two nodes of one label, named apart, and each found
    /// again in another list that opens the file at <paramref name="path"/>.</summary>
    private static void AssertSameLabelAndEachFoundAgain(string path, BrowseNode[] nodes)
    {
        Assert.Equal(2, nodes.Length);
        Assert.Equal(nodes[0].Text, nodes[1].Text);
        Assert.NotEqual(BrowseNodes.GetPath(nodes[0]), BrowseNodes.GetPath(nodes[1]));
        var reopened = new AssemblyList { Runtime = TestRuntime.Installed };
        reopened.Open(path);

        var found = nodes.Select(n => reopened.FindNode(BrowseNodes.GetPath(n))).ToArray();

        Assert.All(found, Assert.NotNull);
        Assert.NotSame(found[0], found[1]);
        Assert.Equal(nodes.Select(static n => BrowseNodes.GetPath(n)), found.OfType<BrowseNode>().Select(static f => BrowseNodes.GetPath(f)));
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
