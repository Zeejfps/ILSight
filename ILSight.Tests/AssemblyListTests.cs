using ILSight.Core;

namespace ILSight.Tests;

public sealed class AssemblyListTests
{
    [Fact]
    public void Opening_the_same_file_twice_returns_the_same_assembly()
    {
        var list = new AssemblyList { Runtime = TestRuntime.Installed };

        var first = list.OpenModule(Trees.TestAssemblyPath);
        var folder = Path.GetDirectoryName(Trees.TestAssemblyPath) ?? throw new InvalidOperationException($"The test assembly's path names no folder: '{Trees.TestAssemblyPath}'.");
        var second = list.Open(Path.Combine(folder, ".", Path.GetFileName(Trees.TestAssemblyPath)));

        Assert.Same(first, second);
        Assert.Single(list.Assemblies);
        Assert.Equal("ILSight.Tests", first.Name);
        Assert.Equal(".NETCoreApp,Version=v10.0", first.TargetFramework?.Text);
    }

    [Fact]
    public void An_assembly_read_again_takes_its_place_in_the_list_with_what_is_picked_for_it_now()
    {
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var first = list.Open(Trees.TestAssemblyPath);
        var second = list.Open(typeof(AssemblyList).Assembly.Location);
        list.TargetFrameworks.Pick(first.FilePath, Frameworks.Net48);

        var fresh = AssemblyListing.ReadAgain(list, first);
        var listedWhileRead = list.Listed.ToList();
        var replaced = list.TryReplace(first, fresh);

        Assert.Equal([first, second], listedWhileRead);
        Assert.True(replaced);
        Assert.Equal([fresh.Item, second], list.Listed);
        Assert.Equal([fresh.Item, second], list.Assemblies);
        Assert.Equal("net48", Assert.IsType<LoadedModule>(fresh.Item).FrameworkMoniker);
        Assert.Same(fresh.Item, list.FindNode(first.Root.Path)?.Assembly);
    }

    [Fact]
    public void An_assembly_read_again_after_it_was_closed_is_not_listed()
    {
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var assembly = list.Open(Trees.TestAssemblyPath);
        var fresh = AssemblyListing.ReadAgain(list, assembly);
        list.Close(assembly);

        Assert.False(list.TryReplace(assembly, fresh));
        Assert.Empty(list.Listed);
    }

    [Fact]
    public void An_open_cancelled_before_it_lists_the_assembly_leaves_the_list_as_it_was()
    {
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        Assert.Throws<OperationCanceledException>(() => list.Open(Trees.TestAssemblyPath, cancelled.Token));

        Assert.Empty(list.Assemblies);
        Assert.Equal(0, list.Version);
    }

    [Fact]
    public void An_assembly_found_by_work_started_before_a_clear_is_neither_listed_nor_kept_after_it()
    {
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var before = list.Generation;
        list.CloseAll();

        var late = list.TryLoad(Trees.TestAssemblyPath, before);
        Assert.NotNull(late);

        Assert.False(AssemblyListing.TryList(list, late, before));
        Assert.Empty(list.Assemblies);
        Assert.NotSame(late, list.Open(Trees.TestAssemblyPath));
    }

    [Fact]
    public void Open_and_close_update_the_snapshot_paths_version_and_listeners()
    {
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var changes = 0;
        list.Changed += () => changes++;

        var tests = list.Open(Trees.TestAssemblyPath);
        var snapshot = list.Assemblies;
        var core = list.Open(typeof(AssemblyList).Assembly.Location);

        Assert.Single(snapshot);
        Assert.Equal([Trees.TestAssemblyPath, typeof(AssemblyList).Assembly.Location], list.ListFilePaths().Select(static p => p.Text));
        Assert.Equal(2, list.Version);

        list.Close(tests);

        Assert.Equal([core], list.Assemblies);
        Assert.Equal(3, changes);
        Assert.Equal(3, list.Version);
    }

    [Fact]
    public void A_closed_assembly_reopens_as_a_new_object()
    {
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var first = list.Open(Trees.TestAssemblyPath);

        list.Close(first);
        var second = list.Open(Trees.TestAssemblyPath);

        Assert.NotSame(first, second);
        Assert.Equal([second], list.Assemblies);
    }

    [Fact]
    public void Files_that_cannot_be_read_stay_listed_as_failed()
    {
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        using var temp = new TempDir();
        var text = temp.Write("text.dll", "not a PE file");

        var notPe = list.Open(text);
        var missing = list.Open(text + ".missing");

        Assert.IsType<FailedAssembly>(notPe);
        Assert.IsType<FailedAssembly>(missing);
        Assert.Equal([notPe, missing], list.Listed);
        Assert.Equal([text, text + ".missing"], list.ListFilePaths().Select(static p => p.Text));
        Assert.Empty(list.Assemblies);
        Assert.Same(notPe, list.Open(text));
    }

    [Fact]
    public void A_failed_file_shows_why_it_failed_and_has_no_children()
    {
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        using var temp = new TempDir();
        var text = temp.Write("text.dll", "not a PE file");

        var root = list.Open(text).Root;
        var document = root.Decompile(DecompilationLanguage.CSharp);

        Assert.IsType<NodeSubject.FailedAssembly>(root.Subject);
        Assert.Equal("text.dll", root.Text);
        Assert.Empty(root.Children);
        Assert.NotNull(document.Error);
        Assert.Contains($"Cannot open '{text}'", Documents.GetText(document));
    }

    [Fact]
    public void A_failed_file_that_loads_when_opened_again_takes_its_place()
    {
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        using var temp = new TempDir();
        var path = temp.Write("late.dll", "not a PE file yet");
        var failed = list.Open(path);
        var other = list.Open(typeof(AssemblyList).Assembly.Location);
        File.Copy(Trees.TestAssemblyPath, path, overwrite: true);

        var loaded = list.Open(path);

        Assert.IsType<FailedAssembly>(failed);
        Assert.IsNotType<FailedAssembly>(loaded);
        Assert.Equal([loaded, other], list.Listed);
        Assert.Equal([loaded, other], list.Assemblies);
    }

    [Fact]
    public void Closing_a_failed_file_takes_it_off_the_list()
    {
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        using var temp = new TempDir();
        var failed = list.Open(temp.PathOf("missing.dll"));

        list.Close(failed);

        Assert.Empty(list.Listed);
    }

    [Fact]
    public void A_node_path_to_a_failed_file_finds_its_root()
    {
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        using var temp = new TempDir();
        var failed = list.Open(temp.PathOf("missing.dll"));

        Assert.Same(failed.Root, list.FindNode(failed.Root.Path));
    }

    [Fact]
    public void Runtime_assemblies_list_the_managed_framework_assemblies()
    {
        var all = RuntimeFolders.ScanAssemblies(TestRuntime.Installed.Directories);

        Assert.Equal(Path.GetDirectoryName(Trees.CoreLibPath), RuntimeAssemblies.ListDirectories(TestRuntime.Installed)[0]);
        Assert.Contains(all, a => a.Name == "System.Private.CoreLib" && a.Framework == "Microsoft.NETCore.App");
        Assert.Contains(all, a => a.Name == "System.Runtime");
        Assert.DoesNotContain(all, a => a.Name.Equals("coreclr", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(all.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).Select(a => a.Name), all.Select(a => a.Name));
    }

    [Fact]
    public void A_native_build_finds_the_newest_installed_runtime_of_its_major_version()
    {
        using var temp = new TempDir();
        var root = temp.Path;
        string Runtime(string version)
        {
            return Path.GetDirectoryName(temp.Write(Path.Combine("shared", "Microsoft.NETCore.App", version, "System.Private.CoreLib.dll"), "")) ?? throw new InvalidOperationException("A file written to the temp folder is in a folder.");
        }

        Runtime("9.0.11");
        var newest = Runtime("10.0.2");
        Runtime("10.0.1");
        Runtime("11.0.0-preview.1");
        Directory.CreateDirectory(temp.PathOf(Path.Combine("shared", "Microsoft.NETCore.App", "10.0.9")));

        Assert.Equal(newest, RuntimeFolders.FindInstalledRuntime([Path.Combine(root, "missing"), root], 10));
        Assert.Equal(Path.Combine(root, "shared", "Microsoft.NETCore.App", "11.0.0-preview.1"), RuntimeFolders.FindInstalledRuntime([root], 12));
    }

    [Fact]
    public void Presets_offer_the_newest_patch_of_each_installed_framework_version_without_its_native_libraries()
    {
        using var temp = new TempDir();
        string Install(string framework, string version, params string[] files)
        {
            var directory = temp.PathOf(Path.Combine("shared", framework, version));
            Directory.CreateDirectory(directory);
            foreach (var file in files)
            {
                temp.Write(Path.Combine(directory, file), "");
            }

            return directory;
        }
        Install("Microsoft.NETCore.App", "9.0.3", "System.Runtime.dll");
        var net9 = Install("Microsoft.NETCore.App", "9.0.11", "System.Runtime.dll", "mscorlib.dll", "coreclr.dll", "System.IO.Compression.Native.dll");
        var net10 = Install("Microsoft.NETCore.App", "10.0.1", "System.Runtime.dll");
        Install("Microsoft.NETCore.App", "8.0.0", "coreclr.dll");
        var desktop = Install("Microsoft.WindowsDesktop.App", "9.0.3", "WindowsBase.dll", "PenImc_cor3.dll");

        var presets = AssemblyListPresets.FindRuntimePresets(temp.Path);

        Assert.Equal(["Microsoft.NETCore.App 10.0.1", "Microsoft.NETCore.App 9.0.11", "Microsoft.WindowsDesktop.App 9.0.3"], presets.Select(p => p.Name));
        Assert.Equal([Path.Combine(net10, "System.Runtime.dll")], presets[0].Paths.Select(static p => p.Text));
        Assert.Equal([Path.Combine(net9, "mscorlib.dll"), Path.Combine(net9, "System.Runtime.dll")], presets[1].Paths.Select(static p => p.Text));
        Assert.Equal([Path.Combine(desktop, "WindowsBase.dll")], presets[2].Paths.Select(static p => p.Text));
    }

    [Fact]
    public void Presets_offer_the_running_runtimes_feature_version()
    {
        var running = Environment.Version;
        var name = $"Microsoft.NETCore.App {running.Major}.{running.Minor}.";

        var preset = AssemblyListPresets.FindInstalled().FirstOrDefault(p => p.Name.StartsWith(name, StringComparison.Ordinal));

        Assert.NotNull(preset);
        Assert.Contains(preset.Paths, p => p.FileName == "System.Runtime.dll");
    }

    [Fact]
    public void References_resolve_from_the_runtime_directory_and_next_to_the_assembly()
    {
        var (list, assembly) = Trees.OpenTestAssembly();
        var references = assembly.Root.Child("References");

        var runtime = list.FindNode(references.Child("System.Runtime").Destination);
        var sibling = list.FindNode(references.Child("ILSight.Core").Destination);

        Assert.NotNull(runtime);
        Assert.Equal(Path.Combine(RuntimeAssemblies.ListDirectories(TestRuntime.Installed)[0], "System.Runtime.dll"), runtime.Assembly.FilePath.Text, ignoreCase: true);
        Assert.Equal(typeof(AssemblyList).Assembly.Location, sibling?.Assembly.FilePath.Text, ignoreCase: true);
        Assert.Same(runtime.Assembly.Root, runtime);
        Assert.Contains(runtime.Assembly, list.Assemblies);
    }

    [Fact]
    public void Listing_the_references_lists_those_found_beside_the_assembly_and_in_the_runtime()
    {
        var (list, assembly) = Trees.OpenTestAssembly();
        var changes = 0;
        list.Changed += () => changes++;

        var listed = AssemblyListing.ListReferences(list, assembly);

        var names = listed.Select(a => a.Name).ToList();
        Assert.Contains("System.Runtime", names);
        Assert.Contains("ILSight.Core", names);
        Assert.All(listed, a => Assert.Contains(a, list.Assemblies));
        Assert.Equal(listed.Count + 1, list.Listed.Count);
        Assert.Equal(listed.Count, changes);
    }

    [Fact]
    public void Sorting_orders_the_list_by_name_failed_files_among_them()
    {
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        using var temp = new TempDir();
        var coreLib = list.Open(Trees.CoreLibPath);
        var missing = list.Open(temp.PathOf("Missing.dll"));
        var tests = list.Open(Trees.TestAssemblyPath);
        var version = list.Version;

        list.SortListed();

        Assert.Equal([tests, missing, coreLib], list.Listed);
        Assert.Equal([tests, coreLib], list.Assemblies);
        Assert.NotEqual(version, list.Version);
        version = list.Version;
        list.SortListed();
        Assert.Equal(version, list.Version);
    }

    [Fact]
    public async Task A_reader_sees_what_is_listed_and_what_loaded_of_one_moment()
    {
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        using var stop = new CancellationTokenSource();
        var reader = Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                var now = list.Now;
                Assert.Equal(now.Listed.Length, now.Loaded.Length);
            }
        });

        for (var round = 0; round < 30; round++)
        {
            list.Close(list.Open(Trees.TestAssemblyPath));
        }

        stop.Cancel();

        await reader;
        Assert.Empty(list.Listed);
    }

    [Fact]
    public async Task Concurrent_opens_and_reads_agree_on_one_assembly_per_file()
    {
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var paths = new[] { Trees.TestAssemblyPath, typeof(AssemblyList).Assembly.Location, Trees.CoreLibPath };

        var opened = await Task.WhenAll(Enumerable.Range(0, 24).Select(i => Task.Run(() =>
        {
            var assembly = list.Open(paths[i % paths.Length]);
            _ = list.Assemblies.Count;
            _ = assembly.Root.Children.Count;
            return assembly;
        })));

        Assert.Equal(3, list.Assemblies.Count);
        foreach (var group in opened.GroupBy(a => a.FilePath))
        {
            Assert.All(group, a => Assert.Same(group.First(), a));
        }
    }

    [Fact]
    public async Task What_takes_reading_is_read_once_whichever_thread_asks_first()
    {
        var assembly = new AssemblyList { Runtime = TestRuntime.Installed }.OpenModule(Trees.TestAssemblyPath);

        var read = Enumerable.Range(0, 16).AsParallel().WithDegreeOfParallelism(8)
            .Select(_ => (TypeSystem: LoadedAssemblies.ReadTypeSystemAsync(assembly, CancellationToken.None), Usage: LoadedAssemblies.EnsureReferenceUsage(assembly), Symbols: LoadedAssemblies.EnsureDebugSymbols(assembly)))
            .ToList();
        var typeSystems = await Task.WhenAll(read.Select(static r => r.TypeSystem));

        Assert.Single(typeSystems.Select(static t => Assert.IsType<TypeSystemRead.Read>(t).TypeSystem).Distinct());
        Assert.Single(read.Select(r => r.Usage).Distinct());
        Assert.Single(read.Select(r => r.Symbols).Distinct());
    }

    [Fact]
    public void Asking_a_package_made_some_other_way_for_its_assemblies_fails_loudly()
    {
        var bundle = new AssemblyList { Runtime = TestRuntime.Installed }.Open(Trees.TestAssemblyPath);
        var package = new LoadedPackage
        {
            List = bundle.List,
            FilePath = bundle.FilePath,
            Name = bundle.Name,
            FullName = bundle.FullName,
            Version = null,
            NameAndVersion = bundle.Name,
            Package = new AssemblyPackage { Files = [], Read = static _ => [], FilePath = bundle.FilePath, IsBundle = false, Root = new PackageFolder { Name = "", FullName = "" } },
        };

        Assert.Throws<InvalidOperationException>(() => LoadedAssemblies.EnsureAssemblies(package));
    }
}
