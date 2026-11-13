using System.IO.Compression;
using System.Text;
using ILSight.Core;

namespace ILSight.Tests;

public sealed class BundleTests
{
    private const string BundleName = "App.exe";
    private const string TestAssemblyName = "ILSight.Tests";

    // The SHA-256 of ".net core bundle", which the host carries right after the offset of the manifest.
    private static readonly byte[] Signature =
    [
        0x8b, 0x12, 0x02, 0xb9, 0x6a, 0x61, 0x20, 0x38,
        0x72, 0x7b, 0x93, 0x02, 0x14, 0xd7, 0xa0, 0x32,
        0x13, 0xf5, 0xb9, 0xe6, 0xef, 0xae, 0x33, 0x18,
        0xee, 0x3b, 0x2d, 0xce, 0x24, 0xb3, 0x6a, 0xae,
    ];

    private static string CoreAssemblyPath => typeof(AssemblyList).Assembly.Location;

    [Fact]
    public async Task A_single_file_bundle_opens_as_a_package_of_its_files()
    {
        using var temp = new TempDir();
        var list = new AssemblyList { Runtime = TestRuntime.Installed };

        var bundle = list.Open(WriteBundle(temp));

        Assert.IsType<LoadedPackage>(bundle);
        Assert.True(Assert.IsType<NodeSubject.Package>((await bundle.ReadRootAsync()).Subject).Value.Package.Package.IsBundle);
        Assert.Equal(BundleName, (await bundle.ReadRootAsync()).Text);
        Assert.Equal([typeof(NodeSubject.PackageFile), typeof(NodeSubject.Module), typeof(NodeSubject.Module)],
            (await bundle.ReadRootAsync().ListChildrenAsync()).Select(static c => c.Subject.GetType()));
        Assert.Equal("App.deps.json", (await bundle.ReadRootAsync().ListChildrenAsync())[0].Text);
        Assert.Equal(["ILSight.Core", TestAssemblyName], list.Assemblies.Select(a => a.Name));
    }

    [Fact]
    public async Task A_compressed_assembly_in_a_bundle_decompiles_and_finds_the_assembly_beside_it()
    {
        using var temp = new TempDir();
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var bundle = list.Open(WriteBundle(temp));
        var assembly = list.Assemblies.Single(a => a.Name == TestAssemblyName);

        var calculator = await assembly.CalculatorAsync().DecompileAsync(DecompilationLanguage.CSharp);
        var core = await list.FindNodeAsync((await assembly.ReadRootAsync().GetChildAsync("References", "ILSight.Core")).Destination);

        Assert.Null(calculator.Error);
        Assert.Contains("class Calculator<T>", Documents.GetText(calculator));
        Assert.Equal(bundle.FilePath.Combine("ILSight.Core.dll"), core?.Assembly.FilePath);
    }

    [Fact]
    public async Task A_file_of_a_bundle_that_is_no_assembly_shows_its_contents()
    {
        using var temp = new TempDir();
        var bundle = new AssemblyList { Runtime = TestRuntime.Installed }.Open(WriteBundle(temp));

        var deps = await bundle.ReadRootAsync().GetChildAsync("App.deps.json");

        Assert.Equal("{}", Encoding.UTF8.GetString(BrowseNodes.ReadResource(deps)));
    }

    [Fact]
    public async Task An_entry_the_manifest_places_past_the_end_of_the_file_is_not_read()
    {
        using var temp = new TempDir();
        var path = WriteBundle(temp, ("Broken.dll", [1, 2, 3], false, 1_000_000));
        var bundle = Assert.IsType<LoadedPackage>(new AssemblyList { Runtime = TestRuntime.Installed }.Open(path));

        var broken = await bundle.ReadRootAsync().GetChildAsync("Broken.dll");

        Assert.IsType<NodeSubject.PackageFile>(broken.Subject);
        Assert.Throws<InvalidDataException>(() => BrowseNodes.ReadResource(broken));
    }

    [Fact]
    public async Task Extracting_a_bundle_lists_an_entry_it_cannot_read_and_writes_the_others()
    {
        using var temp = new TempDir();
        var path = WriteBundle(temp, ("Broken.dll", [1, 2, 3], false, 1_000_000), ("App.deps.json", Encoding.UTF8.GetBytes("{}"), false, 0));
        var bundle = Assert.IsType<LoadedPackage>(new AssemblyList { Runtime = TestRuntime.Installed }.Open(path));
        var target = temp.PathOf("out");

        var extracted = PackageExtraction.ExtractFolder(bundle.Package, FolderOf(await bundle.ReadRootAsync()), target);

        var failure = Assert.IsType<ExtractFailure.NotWritten>(Assert.Single(extracted.Failures));
        Assert.Equal("Broken.dll", failure.File.Name);
        Assert.Contains("past the end of the file", failure.Error.Message);
        Assert.Equal([Path.Combine(target, "App.deps.json")], extracted.Written);
    }

    [Fact]
    public void A_native_executable_that_is_no_bundle_fails_to_load()
    {
        using var temp = new TempDir();
        var path = temp.Write("Host.exe", Encoding.ASCII.GetBytes("MZ this is no bundle and no assembly"));

        var failed = new AssemblyList { Runtime = TestRuntime.Installed }.Open(path);

        Assert.IsType<FailedAssembly>(failed);
    }

    private static PackageFolder FolderOf(BrowseNode node)
    {
        return Assert.IsType<PackageFolderPlace>(node.FolderInPackage).Folder;
    }

    internal static string WriteBundle(TempDir temp)
    {
        return WriteBundle(temp,
            ("ILSight.Tests.dll", File.ReadAllBytes(Trees.TestAssemblyPath), true, 0),
            ("ILSight.Core.dll", File.ReadAllBytes(CoreAssemblyPath), false, 0),
            ("App.deps.json", Encoding.UTF8.GetBytes("{}"), false, 0));
    }

    // As the SDK lays a bundle out: the host, which carries the manifest's offset and the signature; each file; then
    // a version 6 manifest.
    private static string WriteBundle(TempDir temp, params (string Path, byte[] Data, bool Compress, long ClaimedSize)[] files)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8);
        writer.Write(new byte[16]);
        var offsetSlot = stream.Position;
        writer.Write(0L);
        writer.Write(Signature);
        var entries = new List<(long Offset, long Size, long CompressedSize, string Path)>();
        foreach (var file in files)
        {
            entries.Add(WriteFile(writer, file));
        }

        var manifestOffset = stream.Position;
        WriteManifest(writer, entries);
        stream.Position = offsetSlot;
        writer.Write(manifestOffset);
        writer.Flush();
        return temp.Write(BundleName, stream.ToArray());
    }

    private static (long Offset, long Size, long CompressedSize, string Path) WriteFile(BinaryWriter writer, (string Path, byte[] Data, bool Compress, long ClaimedSize) file)
    {
        var offset = writer.BaseStream.Position;
        var stored = file.Compress ? Deflate(file.Data) : file.Data;
        writer.Write(stored);
        var size = file.ClaimedSize != 0 ? file.ClaimedSize : file.Data.Length;
        var compressedSize = file.Compress ? stored.Length : 0;
        return (offset, size, compressedSize, file.Path);
    }

    private static byte[] Deflate(byte[] data)
    {
        using var compressed = new MemoryStream();
        using (var deflate = new DeflateStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            deflate.Write(data);
        }

        return compressed.ToArray();
    }

    private static void WriteManifest(BinaryWriter writer, List<(long Offset, long Size, long CompressedSize, string Path)> entries)
    {
        writer.Write(6u);
        writer.Write(0u);
        writer.Write(entries.Count);
        writer.Write("bundle-id");
        writer.Write(0L);
        writer.Write(0L);
        writer.Write(0L);
        writer.Write(0L);
        writer.Write(0UL);
        foreach (var entry in entries)
        {
            writer.Write(entry.Offset);
            writer.Write(entry.Size);
            writer.Write(entry.CompressedSize);
            writer.Write((byte)0);
            writer.Write(entry.Path);
        }
    }
}
