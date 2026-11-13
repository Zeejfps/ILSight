using System.Reflection.Metadata;
using ILSight.Core;

namespace ILSight.Tests;

public sealed class ExportTests : IDisposable
{
    private readonly TempDir _temp = new();

    public ExportTests()
    {
        Directory.CreateDirectory(_temp.Path);
    }

    public void Dispose()
    {
        _temp.Dispose();
    }

    [Fact]
    public async Task A_type_saves_as_the_code_it_shows()
    {
        var calculator = await Trees.TestAssembly.CalculatorAsync();
        var path = _temp.PathOf("Calculator.cs");

        Assert.IsType<TypeSystemWork<string>.Done>(await CodeFiles.SaveAsync(TestSession.Shared, calculator, DecompilationLanguage.CSharp, DecompilerOptions.Default, path));

        var shown = Documents.GetText(await Trees.CalculatorDocumentAsync(DecompilationLanguage.CSharp));
        Assert.Equal(shown, await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task A_namespace_saves_all_its_types()
    {
        var fixtures = await Trees.TestAssembly.FixturesAsync();
        var path = _temp.PathOf("Fixtures.cs");

        Assert.IsType<TypeSystemWork<string>.Done>(await CodeFiles.SaveAsync(TestSession.Shared, fixtures, DecompilationLanguage.CSharp, DecompilerOptions.Default, path));

        var code = await File.ReadAllTextAsync(path);
        Assert.Contains("public class Calculator<T>", code);
        Assert.Contains("class Circle", code);
    }

    [Fact]
    public async Task A_namespace_saves_in_IL_with_its_CSharp_as_plain_comments()
    {
        var path = _temp.PathOf("Fixtures.il");

        Assert.IsType<TypeSystemWork<string>.Done>(await CodeFiles.SaveAsync(TestSession.Shared, await Trees.TestAssembly.FixturesAsync(), DecompilationLanguage.ILWithCSharp, DecompilerOptions.Default, path));

        var code = await File.ReadAllTextAsync(path);
        Assert.Contains("// double grown = Radius;", code);
        Assert.Contains("IL_0001: ldarg.0", code);
    }

    [Fact]
    public async Task An_assembly_saves_its_whole_module_in_IL()
    {
        var path = _temp.PathOf("Tests.il");

        Assert.IsType<TypeSystemWork<string>.Done>(await CodeFiles.SaveAsync(TestSession.Shared, await Trees.TestAssembly.ReadRootAsync(), DecompilationLanguage.IL, DecompilerOptions.Default, path));

        var code = await File.ReadAllTextAsync(path);
        Assert.Contains(".assembly ILSight.Tests", code);
        Assert.Contains("ILSight.Tests.Fixtures.Calculator`1", code);
    }

    [Fact]
    public async Task An_assembly_shows_its_attributes_alone_but_saves_its_whole_module_in_CSharp()
    {
        var root = await Trees.TestAssembly.ReadRootAsync();
        var path = _temp.PathOf("Tests.cs");

        Assert.IsType<TypeSystemWork<string>.Done>(await CodeFiles.SaveAsync(TestSession.Shared, root, DecompilationLanguage.CSharp, DecompilerOptions.Default, path));

        var shown = Documents.GetText(await root.DecompileAsync(DecompilationLanguage.CSharp));
        var saved = await File.ReadAllTextAsync(path);
        Assert.Contains("[assembly: AssemblyProduct(\"ILSight.Tests\")]", shown);
        Assert.DoesNotContain("class Calculator<T>", shown);
        Assert.Contains("[assembly: AssemblyProduct(\"ILSight.Tests\")]", saved);
        Assert.Contains("public class Calculator<T>", saved);
    }

    [Fact]
    public async Task A_namespace_shows_its_types_declarations_but_saves_their_code()
    {
        var fixtures = await Trees.TestAssembly.FixturesAsync();
        var path = _temp.PathOf("Fixtures.cs");

        Assert.IsType<TypeSystemWork<string>.Done>(await CodeFiles.SaveAsync(TestSession.Shared, fixtures, DecompilationLanguage.CSharp, DecompilerOptions.Default, path));

        var shown = Documents.GetText(await fixtures.DecompileAsync(DecompilationLanguage.CSharp));
        var saved = await File.ReadAllTextAsync(path);
        Assert.Contains("internal sealed class Circle", shown);
        Assert.DoesNotContain("double grown = Radius;", shown);
        Assert.Contains("double grown = Radius;", saved);
    }

    [Fact]
    public async Task Code_files_are_offered_under_the_node_name_with_the_language_extension()
    {
        var calculator = await Trees.TestAssembly.CalculatorAsync();

        Assert.Equal("Calculator.cs", calculator.SuggestCodeFileName(DecompilationLanguage.CSharp));
        Assert.Equal("Calculator.il", calculator.SuggestCodeFileName(DecompilationLanguage.IL));
        Assert.Equal("ILSight.Tests.cs", (await Trees.TestAssembly.ReadRootAsync()).SuggestCodeFileName(DecompilationLanguage.CSharp));
        Assert.Equal("ILSight.Tests.pdb", Trees.TestAssembly.PdbFileName);
    }

    [Fact]
    public async Task An_assembly_exports_as_a_project_with_a_file_per_type()
    {
        var projectPath = _temp.PathOf(Path.Combine("Project", "Tests.csproj"));

        var exported = await AssemblyExport.ExportProjectAsync(Trees.TestAssembly, DecompilerOptions.Default, projectPath).GetValueAsync();

        Assert.Equal(projectPath, exported.ProjectPath);
        Assert.Contains("<Project", await File.ReadAllTextAsync(projectPath));
        var folder = Path.GetDirectoryName(projectPath) ?? throw new InvalidOperationException($"The project's path names no folder: '{projectPath}'.");
        var files = Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories);
        Assert.Contains(files, f => Path.GetFileName(f) == "Calculator.cs");
    }

    [Fact]
    public async Task Baml_exports_as_xaml_pages_beside_their_code_behind()
    {
        var assembly = new AssemblyList { Runtime = TestRuntime.Installed }.OpenModule(Trees.SampleAppPath);
        var projectPath = _temp.PathOf(Path.Combine("Sample", "SampleApp.csproj"));

        await AssemblyExport.ExportProjectAsync(assembly, DecompilerOptions.Default, projectPath).GetValueAsync();

        var folder = Path.GetDirectoryName(projectPath) ?? throw new InvalidOperationException($"The project's path names no folder: '{projectPath}'.");
        var project = await File.ReadAllTextAsync(projectPath);
        var window = await File.ReadAllTextAsync(Path.Combine(folder, "SampleApp", "MainWindow.xaml"));
        var codeBehind = await File.ReadAllTextAsync(Path.Combine(folder, "SampleApp", "MainWindow.cs"));
        Assert.Contains("x:Class=\"SampleApp.MainWindow\"", window);
        Assert.Contains($"<Page Include=\"{Path.Combine("SampleApp", "MainWindow.xaml")}\"", project);
        Assert.Contains($"<ApplicationDefinition Include=\"{Path.Combine("SampleApp", "App.xaml")}\"", project);
        Assert.DoesNotContain("baml", project);
        Assert.DoesNotContain("void InitializeComponent()", codeBehind);
    }

    [Fact]
    public async Task A_solution_holds_a_project_of_each_assembly_in_a_folder_of_its_own()
    {
        var solutionPath = _temp.PathOf("All.sln");

        var exported = await AssemblyExport.ExportSolutionAsync(ExportNames.NameProjects([Trees.TestAssembly]), DecompilerOptions.Default, solutionPath).GetValueAsync();

        var project = Assert.Single(exported);
        Assert.Equal(_temp.PathOf(Path.Combine("ILSight.Tests", "ILSight.Tests.csproj")), project.ProjectPath);
        Assert.True(File.Exists(project.ProjectPath));
        Assert.Contains("ILSight.Tests.csproj", await File.ReadAllTextAsync(solutionPath));
    }

    [Fact]
    public async Task A_generated_pdb_embeds_the_decompiled_code()
    {
        var assembly = Trees.TestAssembly;
        var path = _temp.PathOf("Tests.pdb");

        Assert.True(assembly.CanGeneratePdb);
        Assert.IsType<TypeSystemWork<string>.Done>(await AssemblyExport.GeneratePdbAsync(assembly, DecompilerOptions.Default, path));

        using var stream = File.OpenRead(path);
        using var provider = MetadataReaderProvider.FromPortablePdbStream(stream);
        var pdb = provider.GetMetadataReader();
        Assert.NotEmpty(pdb.Documents);
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public async Task A_project_export_reports_its_files_as_it_writes_them()
    {
        var progress = new RecordedProgress();
        var projectPath = _temp.PathOf(Path.Combine("Project", "Tests.csproj"));

        await AssemblyExport.ExportProjectAsync(Trees.TestAssembly, DecompilerOptions.Default, projectPath, progress: progress).GetValueAsync();

        var reports = progress.Reports;
        Assert.Equal(new ExportProgress { Name = "ILSight.Tests", Part = 1, Parts = 1, Fraction = 0f }, reports[0]);
        Assert.Contains(reports, r => r.Fraction == 1f);
    }

    [Fact]
    public async Task A_solution_reports_each_project_as_a_part_of_the_whole()
    {
        var progress = new RecordedProgress();
        var solutionPath = _temp.PathOf("All.sln");

        await AssemblyExport.ExportSolutionAsync(ExportNames.NameProjects([Trees.TestAssembly, Trees.TestAssembly]), DecompilerOptions.Default, solutionPath, progress: progress).GetValueAsync();

        Assert.Contains(progress.Reports, r => r is { Part: 1, Parts: 2 });
        Assert.Contains(progress.Reports, r => r is { Part: 2, Parts: 2, Fraction: 0.5f });
        Assert.All(progress.Reports, r => Assert.InRange(Assert.NotNull(r.Fraction), 0f, 1f));
    }

    [Fact]
    public async Task A_solution_puts_each_project_in_a_folder_named_as_the_project()
    {
        var solutionPath = _temp.PathOf("All.sln");
        SolutionProject[] projects = [new SolutionProject { Assembly = Trees.TestAssembly, Name = "Renamed" }];

        var exported = await AssemblyExport.ExportSolutionAsync(projects, DecompilerOptions.Default, solutionPath).GetValueAsync();

        var project = Assert.Single(exported);
        Assert.Equal(_temp.PathOf(Path.Combine("Renamed", "Renamed.csproj")), project.ProjectPath);
        Assert.Contains(Path.Combine("Renamed", "Renamed.csproj"), await File.ReadAllTextAsync(solutionPath));
    }

    [Fact]
    public void Projects_are_named_after_their_assemblies_with_a_number_after_a_taken_name()
    {
        var projects = ExportNames.NameProjects([Trees.TestAssembly, Trees.TestAssembly]);

        Assert.Equal(["ILSight.Tests", "ILSight.Tests_2"], projects.Select(p => p.Name));
    }

    [Theory]
    [InlineData("Mine", "Mine.sln")]
    [InlineData("Mine.sln", "Mine.sln")]
    [InlineData(" Mine.SLN ", "Mine.sln")]
    public void A_solution_file_is_named_with_one_extension(string typed, string expected)
    {
        Assert.Equal(expected, ExportNames.SolutionFileName(typed));
    }

    [Fact]
    public void Each_assembly_takes_the_share_of_the_export_its_types_make_up()
    {
        var coreLib = new AssemblyList { Runtime = TestRuntime.Installed }.OpenModule(Trees.CoreLibPath);
        var tests = Trees.TestAssembly;

        var bounds = AssemblyExport.PartBounds([tests, coreLib]);

        var testTypes = LoadedAssemblies.GetFile(tests).Metadata.TypeDefinitions.Count;
        var coreLibTypes = LoadedAssemblies.GetFile(coreLib).Metadata.TypeDefinitions.Count;
        Assert.Equal(0f, bounds[0]);
        Assert.Equal((float)testTypes / (testTypes + coreLibTypes), bounds[1], 5);
        Assert.Equal(1f, bounds[2]);
    }

    [Fact]
    public async Task A_cancelled_save_leaves_no_file_behind()
    {
        var path = _temp.PathOf("Tests.cs");
        var root = await Trees.TestAssembly.ReadRootAsync();
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CodeFiles.SaveAsync(TestSession.Shared, root, DecompilationLanguage.CSharp, DecompilerOptions.Default, path, cancel.Token));

        Assert.Empty(Directory.EnumerateFileSystemEntries(_temp.Path));
    }

    [Fact]
    public async Task A_cancelled_project_export_stops()
    {
        var projectPath = _temp.PathOf(Path.Combine("Project", "Tests.csproj"));
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            AssemblyExport.ExportProjectAsync(Trees.TestAssembly, DecompilerOptions.Default, projectPath, cancellationToken: cancel.Token).GetValueAsync());
    }

    private static readonly Guid EmbeddedSource = new("0E8A571B-6926-466E-B4AD-8AB04611F5FE");

    private static int CountEmbeddedSources(string pdbPath)
    {
        using var stream = File.OpenRead(pdbPath);
        using var provider = MetadataReaderProvider.FromPortablePdbStream(stream);
        var pdb = provider.GetMetadataReader();
        return pdb.CustomDebugInformation
            .Select(h => pdb.GetCustomDebugInformation(h))
            .Count(i => pdb.GetGuid(i.Kind) == EmbeddedSource);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_project_gets_a_pdb_beside_it_with_or_without_the_sources_in_it(bool embed)
    {
        var projectPath = _temp.PathOf(Path.Combine("Project", "Tests.csproj"));

        var exported = await AssemblyExport.ExportProjectAsync(Trees.TestAssembly, DecompilerOptions.Default, projectPath, new ProjectExtras
        {
            Pdb = true,
            EmbedSourcesInPdb = embed,
        }).GetValueAsync();

        var pdb = Assert.IsType<ProjectPdb.Written>(exported.Pdb);
        Assert.Equal(_temp.PathOf(Path.Combine("Project", "ILSight.Tests.pdb")), pdb.Path);
        Assert.Equal(embed, CountEmbeddedSources(pdb.Path) > 0);
    }

    [Fact]
    public async Task A_strong_name_key_is_copied_into_the_project_and_signs_it()
    {
        var key = _temp.Write("keys/signing.snk", [1, 2, 3]);
        var projectPath = _temp.PathOf(Path.Combine("Project", "Tests.csproj"));

        await AssemblyExport.ExportProjectAsync(Trees.TestAssembly, DecompilerOptions.Default, projectPath, new ProjectExtras
        {
            StrongNameKeyFile = key,
        }).GetValueAsync();

        Assert.True(File.Exists(_temp.PathOf(Path.Combine("Project", "signing.snk"))));
        Assert.Contains("signing.snk", await File.ReadAllTextAsync(projectPath));
    }

    [Fact]
    public void Resetting_the_code_toggles_leaves_how_projects_are_exported()
    {
        var nested = DecompilerToggles.FindToggle("UseNestedDirectoriesForNamespaces");
        Assert.NotNull(nested);
        var deadCode = DecompilerToggles.FindToggle("RemoveDeadCode");
        Assert.NotNull(deadCode);
        var changed = DecompilerToggles.SetToggle(DecompilerToggles.SetToggle(DecompilerOptions.Default, nested, !nested.DefaultValue), deadCode, !deadCode.DefaultValue);

        var reset = DecompilerToggles.ResetCodeToggles(changed);

        Assert.Equal(!nested.DefaultValue, DecompilerToggles.IsOn(reset, nested));
        Assert.Equal(deadCode.DefaultValue, DecompilerToggles.IsOn(reset, deadCode));
    }

    [Fact]
    public void The_project_export_toggles_are_offered_in_their_own_group()
    {
        var toggles = DecompilerToggle.All
            .Where(t => t.Group is DecompilerToggleGroup.ProjectExport)
            .Select(t => t.Name);

        Assert.Equal(["UseSdkStyleProjectFormat", "UseNestedDirectoriesForNamespaces"], toggles);
    }

    [Fact]
    public void The_project_export_toggles_leave_decompiled_code_as_it_was()
    {
        var nested = DecompilerToggles.FindToggle("UseNestedDirectoriesForNamespaces");
        Assert.NotNull(nested);
        var changed = DecompilerToggles.SetToggle(DecompilerOptions.Default, nested, !nested.DefaultValue);

        Assert.True(DecompilerToggles.ProducesSameCode(changed, DecompilerOptions.Default, DecompilationLanguage.CSharp));
    }
}
