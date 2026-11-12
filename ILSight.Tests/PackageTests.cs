using System.IO.Compression;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using ICSharpCode.Decompiler.TypeSystem;
using ILSight.Core;
using ILSight.Core.Analysis;

namespace ILSight.Tests;

public sealed class PackageTests
{
    private const string LibFolder = "lib/net10.0";
    private const string TestAssemblyName = "ILSight.Tests";

    private static string CoreAssemblyPath => typeof(AssemblyList).Assembly.Location;

    [Fact]
    public async Task A_zip_opens_as_a_package_listed_in_place_of_the_assemblies_in_it()
    {
        using var temp = new TempDir();
        var list = new AssemblyList { Runtime = TestRuntime.Installed };

        var package = list.Open(WritePackage(temp));

        Assert.IsType<LoadedPackage>(package);
        Assert.Equal([package], list.Listed);
        Assert.Equal(["ILSight.Core", TestAssemblyName], list.Assemblies.Select(a => a.Name));
        Assert.All(list.Assemblies, a => Assert.Same(package, ContainerOf(a)));
        Assert.False(Assert.IsType<NodeSubject.Package>((await package.ReadRootAsync()).Subject).Value.Package.Package.IsBundle);
        Assert.Equal("Sample.1.0.0.nupkg", (await package.ReadRootAsync()).Text);
        Assert.Equal([LibFolder, "runtimes/win-x64/native", "readme.txt"], (await package.ReadRootAsync().ListChildrenAsync()).Select(c => c.Text));
    }

    [Fact]
    public void A_zip_entry_named_with_backslashes_is_listed_with_slashes_and_reads_back()
    {
        using var temp = new TempDir();
        var path = temp.Write("Slashes.zip", []);
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Update))
        {
            WriteText(archive, "docs\\guide.txt", "Guide");
            WriteText(archive, "./readme.txt", "Read me");
        }

        var package = AssemblyPackages.TryRead(AssemblyPath.Of(path));
        Assert.NotNull(package);

        var guide = AssemblyPackages.FindFile(package, "docs/guide.txt");
        Assert.NotNull(guide);
        Assert.Equal("Guide", Encoding.UTF8.GetString(AssemblyPackages.ReadFile(package, guide)));
        var readme = AssemblyPackages.FindFile(package, "readme.txt");
        Assert.NotNull(readme);
        Assert.Equal("Read me", Encoding.UTF8.GetString(AssemblyPackages.ReadFile(package, readme)));
    }

    [Theory]
    [InlineData("../evil.txt")]
    [InlineData("..\\evil.txt")]
    [InlineData("docs/../../evil.txt")]
    [InlineData("/evil.txt")]
    [InlineData("\\evil.txt")]
    [InlineData("C:/evil.txt")]
    [InlineData("C:evil.txt")]
    public void A_zip_entry_whose_name_leads_out_of_the_package_is_left_out(string name)
    {
        using var temp = new TempDir();
        var path = temp.Write("Escape.zip", []);
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Update))
        {
            WriteText(archive, name, "Evil");
            WriteText(archive, "readme.txt", "Read me");
        }

        var package = AssemblyPackages.TryRead(AssemblyPath.Of(path));
        Assert.NotNull(package);

        Assert.Equal(["readme.txt"], AssemblyPackages.ListFiles(package).Select(static f => f.FullName));
        Assert.Empty(package.Root.Folders);
    }

    [Fact]
    public async Task A_reference_into_a_package_file_itself_finds_nothing_rather_than_failing()
    {
        using var temp = new TempDir();
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var path = WritePackage(temp);
        var type = CodeReferences.CreateEntity(EntityKind.Type, "Sample", AssemblyPath.Of(path), MetadataTokens.TypeDefinitionHandle(2));

        Assert.Null(await list.FindNodeAsync(type));
        Assert.Null(await list.FindMetadataRowAsync(type));
        Assert.IsType<Lookup<CodeTooltip>.NotFound>(await AssemblyListing.DescribeReferenceAsync(list, type, CancellationToken.None));
        Assert.IsType<Lookup<string>.NotFound>(await AssemblyListing.FindApiReferencePageAsync(list, type, CancellationToken.None));
        Assert.IsType<Lookup<ITypeDefinition>.NotFound>(await AssemblyListing.FindTypeDefinitionAsync(list, type, CancellationToken.None));
        Assert.Null(await list.FindMetadataRowAsync(CodeReferences.CreateAssembly("Sample", AssemblyPath.Of(path))));
    }

    [Fact]
    public void Closing_a_package_reached_through_one_assembly_forgets_the_picks_of_the_others_in_it()
    {
        using var temp = new TempDir();
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var package = list.Open(WritePackage(temp));
        var sibling = package.FilePath.Combine(Path.Combine("lib", "net10.0", "ILSight.Core.dll"));
        list.TargetFrameworks.Pick(sibling, Frameworks.Net48);
        list.CloseAll();
        var assembly = AssemblyListing.TryLoad(list, package.FilePath.Combine(Path.Combine("lib", "net10.0", "ILSight.Tests.dll")));
        Assert.NotNull(assembly);

        list.Close(assembly);

        Assert.Empty(list.TargetFrameworks.All);
    }

    [Fact]
    public async Task A_package_read_again_for_an_assembly_in_it_takes_its_place_with_its_assemblies_read_again()
    {
        using var temp = new TempDir();
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var package = list.Open(WritePackage(temp));
        var old = list.Assemblies.ToList();

        var read = AssemblyListing.ReadAgain(list, old[1]);
        var fresh = read.Item;
        var replaced = list.TryReplace(package, read);

        Assert.True(replaced);
        Assert.Equal([fresh], list.Listed);
        Assert.Equal(read.ReadOutOf, list.Assemblies);
        Assert.All(list.Assemblies, a => Assert.Same(fresh, ContainerOf(a)));
        Assert.Empty(list.Assemblies.Intersect(old));
        var root = await TestAssemblyRootAsync(await fresh.ReadRootAsync().GetChildAsync(LibFolder));
        Assert.Same(list.Assemblies[1], root.Assembly);
        Assert.Same(root, await list.FindNodeAsync((await old[1].ReadRootAsync()).Path));
    }

    [Fact]
    public async Task An_assembly_in_a_package_is_the_root_of_its_tree_under_its_folder_and_known_by_a_path_through_the_package()
    {
        using var temp = new TempDir();
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var package = list.Open(WritePackage(temp));

        var folder = await package.ReadRootAsync().GetChildAsync(LibFolder);
        var root = await TestAssemblyRootAsync(folder);
        var assembly = root.Assembly;

        Assert.Equal(package.FilePath.Combine(Path.Combine("lib", "net10.0", "ILSight.Tests.dll")), assembly.FilePath);
        Assert.Same(package, ContainerOf(assembly));
        Assert.Same(root, await assembly.ReadRootAsync());
        Assert.Same(folder, root.Parent);
        Assert.Equal(TypeShape.Class, (await assembly.CalculatorAsync()).TypeShape);
    }

    [Fact]
    public async Task An_assembly_read_out_of_a_package_finds_its_root_before_the_folder_is_opened()
    {
        using var temp = new TempDir();
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var package = list.Open(WritePackage(temp));
        var path = package.FilePath.Combine(Path.Combine("lib", "net10.0", "ILSight.Tests.dll"));

        var assembly = AssemblyListing.TryLoad(list, path);
        Assert.NotNull(assembly);

        Assert.Same(await package.ReadRootAsync().GetChildAsync(LibFolder), (await assembly.ReadRootAsync()).Parent);
        Assert.Same(await assembly.ReadRootAsync(), await TestAssemblyRootAsync(await package.ReadRootAsync().GetChildAsync(LibFolder)));
    }

    [Fact]
    public async Task A_path_to_a_node_in_a_package_starts_at_the_package_and_finds_the_node_again_in_another_list()
    {
        using var temp = new TempDir();
        var packagePath = WritePackage(temp);
        var first = new AssemblyList { Runtime = TestRuntime.Installed };
        var lib = await first.Open(packagePath).ReadRootAsync().GetChildAsync(LibFolder);
        var calculator = await (await TestAssemblyRootAsync(lib)).Assembly.CalculatorAsync();

        var path = calculator.Path;
        var second = new AssemblyList { Runtime = TestRuntime.Installed };
        var found = await second.FindNodeAsync(path);

        Assert.Equal(packagePath, path.FilePath.Text);
        Assert.Equal(new NodePathSegment.EmbeddedModule($"{LibFolder}/{TestAssemblyName}.dll"), path.Steps[0].Segment);
        Assert.NotNull(found);
        Assert.Equal(calculator.Text, found.Text);
        Assert.IsType<LoadedPackage>(Assert.Single(second.Listed));
    }

    [Fact]
    public async Task References_resolve_to_the_assembly_beside_it_in_the_package()
    {
        using var temp = new TempDir();
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var package = list.Open(WritePackage(temp));
        var assembly = (await TestAssemblyRootAsync(await package.ReadRootAsync().GetChildAsync(LibFolder))).Assembly;

        var core = await assembly.ReadRootAsync().GetChildAsync("References", "ILSight.Core");
        var resolved = await list.FindNodeAsync(core.Destination);

        Assert.NotNull(resolved);
        Assert.Equal(package.FilePath.Combine(Path.Combine("lib", "net10.0", "ILSight.Core.dll")), resolved.Assembly.FilePath);
        Assert.Same(package, ContainerOf(resolved.Assembly));
    }

    [Fact]
    public async Task Following_a_reference_into_a_package_lists_the_package_and_not_the_assembly()
    {
        using var temp = new TempDir();
        var packagePath = WritePackage(temp);
        var lib = await new AssemblyList { Runtime = TestRuntime.Installed }.Open(packagePath).ReadRootAsync().GetChildAsync(LibFolder);
        var calculator = await (await TestAssemblyRootAsync(lib)).Assembly.CalculatorAsync();

        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var found = await list.FindNodeAsync(calculator.Definition);

        Assert.NotNull(found);
        Assert.Equal(calculator.Text, found.Text);
        var listed = Assert.Single(list.Listed);
        Assert.Equal(packagePath, listed.FilePath.Text);
        Assert.Same(listed, ContainerOf(found.Assembly));
    }

    [Fact]
    public async Task Analyzing_a_member_of_an_assembly_in_a_package_finds_its_users_there()
    {
        using var temp = new TempDir();
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var package = list.Open(WritePackage(temp));
        var lib = await package.ReadRootAsync().GetChildAsync(LibFolder);
        var add = await (await TestAssemblyRootAsync(lib)).Assembly.CalculatorAsync().GetChildAsync("Add(T) : void");
        var symbol = add.Analysis;
        Assert.NotNull(symbol);
        var usedBy = symbol.Kinds.Single(k => Analyzers.GetHeader(k) == "Used By");

        var callers = Analyzers.RunAsync(list, symbol, usedBy)
            .ToBlockingEnumerable()
            .SelectMany(static b => b)
            .Select(static f => Assert.IsType<AnalysisFind.Found>(f).Symbol)
            .ToList();

        var plus = Assert.Single(callers);
        Assert.StartsWith("operator +", plus.Label);
    }

    [Fact]
    public async Task A_package_named_like_a_referenced_assembly_is_not_taken_for_it()
    {
        using var temp = new TempDir();
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        list.Open(WriteTextPackage(temp, "ILSight.Core.zip"));
        var assembly = list.Open(Trees.TestAssemblyPath);

        var core = await assembly.ReadRootAsync().GetChildAsync("References", "ILSight.Core");
        var resolved = await list.FindNodeAsync(core.Destination);

        Assert.Equal(CoreAssemblyPath, resolved?.Assembly.FilePath.Text, ignoreCase: true);
    }

    [Fact]
    public async Task A_file_of_a_package_that_is_no_assembly_shows_its_contents()
    {
        using var temp = new TempDir();
        var package = Assert.IsType<LoadedPackage>(new AssemblyList { Runtime = TestRuntime.Installed }.Open(WritePackage(temp)));

        var readme = await package.ReadRootAsync().GetChildAsync("readme.txt");
        var native = await package.ReadRootAsync().GetChildAsync("runtimes/win-x64/native", "native.dll");

        Assert.Equal("Read me", Encoding.UTF8.GetString(BrowseNodes.ReadResource(readme)));
        Assert.IsType<NodeSubject.PackageFile>(native.Subject);
        Assert.False(native.IsImage);
        Assert.Contains("Read me", Documents.GetText(await readme.DecompileAsync(DecompilationLanguage.CSharp)));
    }

    [Theory]
    [InlineData(DecompilationLanguage.CSharp)]
    [InlineData(DecompilationLanguage.IL)]
    public async Task A_package_and_its_folders_list_what_they_hold(DecompilationLanguage language)
    {
        using var temp = new TempDir();
        var package = Assert.IsType<LoadedPackage>(new AssemblyList { Runtime = TestRuntime.Installed }.Open(WritePackage(temp)));

        var root = await package.ReadRootAsync().DecompileAsync(language);
        var folder = await package.ReadRootAsync().GetChildAsync(LibFolder).DecompileAsync(language);

        Assert.Null(root.Error);
        Assert.Contains($"{LibFolder}/", Documents.GetText(root));
        Assert.Contains("readme.txt (7 bytes)", Documents.GetText(root));
        Assert.Contains(TestAssemblyName, Documents.GetText(folder));
    }

    [Fact]
    public async Task Closing_a_package_forgets_the_assemblies_read_out_of_it()
    {
        using var temp = new TempDir();
        var packagePath = WritePackage(temp);
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var before = (await TestAssemblyRootAsync(await list.Open(packagePath).ReadRootAsync().GetChildAsync(LibFolder))).Assembly;

        list.Close(list.Listed[0]);
        Assert.Empty(list.Assemblies);
        var after = (await TestAssemblyRootAsync(await list.Open(packagePath).ReadRootAsync().GetChildAsync(LibFolder))).Assembly;

        Assert.NotSame(before, after);
    }

    [Fact]
    public async Task Searching_finds_types_in_the_assemblies_of_an_open_package()
    {
        using var temp = new TempDir();
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var package = list.Open(WritePackage(temp));

        var results = new List<SearchResult>();
        await foreach (var batch in list.SearchAsync("Calculator", SearchKinds.Types))
        {
            results.AddRange(batch.Select(static f => Assert.IsType<SearchFind.Hit>(f).Result));
        }

        var calculator = Assert.Single(results, r => r.Text == "Calculator<T>");
        Assert.Same(package, ContainerOf(calculator.Node.Assembly));
        Assert.Same(await package.ReadRootAsync().GetChildAsync(LibFolder), (await calculator.Node.Assembly.ReadRootAsync()).Parent);
    }

    [Fact]
    public async Task An_assembly_in_a_package_takes_its_symbols_from_the_pdb_beside_it_there()
    {
        using var temp = new TempDir();
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var package = list.Open(WritePackage(temp));
        var folder = await package.ReadRootAsync().GetChildAsync(LibFolder);
        var assembly = Assert.IsType<LoadedModule>((await TestAssemblyRootAsync(folder)).Assembly);
        var core = list.Assemblies.Single(a => a.Name == "ILSight.Core");

        var symbols = LoadedAssemblies.EnsureDebugSymbols(assembly);

        Assert.Equal(Path.ChangeExtension(assembly.FilePath.Text, ".pdb"), symbols?.SourceFileName);
        Assert.Null(LoadedAssemblies.EnsureDebugSymbols(core));
    }

    [Fact]
    public async Task Extracting_a_folder_writes_its_files_at_their_paths_from_it()
    {
        using var temp = new TempDir();
        var package = Assert.IsType<LoadedPackage>(new AssemblyList { Runtime = TestRuntime.Installed }.Open(WritePackage(temp)));
        var target = temp.PathOf("out");

        var extracted = PackageExtraction.ExtractFolder(package.Package, FolderOf(await package.ReadRootAsync().GetChildAsync(LibFolder)), target);

        var dll = Path.Combine(target, "ILSight.Tests.dll");
        Assert.Empty(extracted.Failures);
        Assert.Equal(3, extracted.Written.Count);
        Assert.Contains(dll, extracted.Written);
        Assert.Equal(await File.ReadAllBytesAsync(Trees.TestAssemblyPath), await File.ReadAllBytesAsync(dll));
    }

    [Fact]
    public async Task Extracting_a_package_keeps_its_folders()
    {
        using var temp = new TempDir();
        var package = Assert.IsType<LoadedPackage>(new AssemblyList { Runtime = TestRuntime.Installed }.Open(WritePackage(temp)));
        var target = temp.PathOf("out");

        PackageExtraction.ExtractFolder(package.Package, FolderOf(await package.ReadRootAsync()), target);

        Assert.Equal("Read me", await File.ReadAllTextAsync(Path.Combine(target, "readme.txt")));
        Assert.True(File.Exists(Path.Combine(target, "runtimes", "win-x64", "native", "native.dll")));
        Assert.True(File.Exists(Path.Combine(target, "lib", "net10.0", "ILSight.Core.dll")));
    }

    [Fact]
    public async Task A_file_whose_path_leads_out_of_the_folder_is_not_extracted()
    {
        using var temp = new TempDir();
        var path = temp.Write("Escape.zip", []);
        await using (var archive = await ZipFile.OpenAsync(path, ZipArchiveMode.Update))
        {
            WriteText(archive, "../escaped.txt", "Out");
            WriteText(archive, "kept.txt", "In");
        }
        var package = Assert.IsType<LoadedPackage>(new AssemblyList { Runtime = TestRuntime.Installed }.Open(path));
        var target = temp.PathOf("out");

        var extracted = PackageExtraction.ExtractFolder(package.Package, FolderOf(await package.ReadRootAsync()), target);

        Assert.Empty(extracted.Failures);
        Assert.False(File.Exists(temp.PathOf("escaped.txt")));
        Assert.Equal([Path.Combine(target, "kept.txt")], extracted.Written);
    }

    [Fact]
    public void A_file_that_is_neither_an_assembly_nor_a_zip_is_listed_as_failed()
    {
        using var temp = new TempDir();
        var list = new AssemblyList { Runtime = TestRuntime.Installed };

        var failed = list.Open(temp.Write("notes.dll", "not an assembly"));

        Assert.IsType<FailedAssembly>(failed);
        Assert.IsNotType<LoadedPackage>(failed);
    }

    private static async Task<BrowseNode> TestAssemblyRootAsync(BrowseNode folder)
    {
        return (await folder.ListChildrenAsync()).Single(IsTestAssembly);
    }

    private static bool IsTestAssembly(BrowseNode node)
    {
        if (node.Subject is not NodeSubject.Module)
        {
            return false;
        }

        return node.Assembly.Name == TestAssemblyName;
    }

    /// <summary>A NuGet package of the test assembly, with its PDB, and the assembly it references,
    /// without one, beside a native library and a text file.</summary>
    /// <summary>The package <paramref name="assembly"/> was read out of; fails the test when it was read from no package.</summary>
    private static LoadedPackage ContainerOf(LoadedAssembly assembly)
    {
        return Assert.IsType<PackagePlace>(Assert.IsType<LoadedModule>(assembly).Place).Container;
    }

    private static PackageFolder FolderOf(BrowseNode node)
    {
        return Assert.IsType<PackageFolderPlace>(node.FolderInPackage).Folder;
    }

    internal static string WritePackage(TempDir temp)
    {
        var path = temp.Write("Sample.1.0.0.nupkg", []);
        using var archive = ZipFile.Open(path, ZipArchiveMode.Update);
        archive.CreateEntryFromFile(Trees.TestAssemblyPath, $"{LibFolder}/ILSight.Tests.dll");
        archive.CreateEntryFromFile(Path.ChangeExtension(Trees.TestAssemblyPath, ".pdb"), $"{LibFolder}/ILSight.Tests.pdb");
        archive.CreateEntryFromFile(CoreAssemblyPath, $"{LibFolder}/ILSight.Core.dll");
        WriteText(archive, "runtimes/win-x64/native/native.dll", "not an assembly");
        WriteText(archive, "readme.txt", "Read me");
        return path;
    }

    /// <summary>A zip holding nothing but a text file.</summary>
    private static string WriteTextPackage(TempDir temp, string name)
    {
        var path = temp.Write(name, []);
        using var archive = ZipFile.Open(path, ZipArchiveMode.Update);
        WriteText(archive, "readme.txt", "Read me");
        return path;
    }

    private static void WriteText(ZipArchive archive, string name, string text)
    {
        var entry = archive.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(text);
    }
}
