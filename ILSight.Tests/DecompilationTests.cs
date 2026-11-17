using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using ILSight.Core;

namespace ILSight.Tests;

public sealed class DecompilationTests
{
    [Fact]
    public void Each_CSharp_version_decompiles_as_the_decompiler_version_of_the_same_number()
    {
        Assert.All(Enum.GetValues<CSharpVersion>(), v => Assert.Equal((int)v, (int)Settings.LanguageVersionOf(v)));
    }

    [Fact]
    public async Task CSharp_spans_are_classified()
    {
        var document = await Trees.CalculatorDocumentAsync(DecompilationLanguage.CSharp);

        Assert.Null(document.Error);
        Assert.Equal("Calculator<T>", document.Title);
        var header = document.Lines[document.LineContaining("public class Calculator<T>")];
        Assert.Equal(SpanKind.Keyword, header.SpanAt("public").Kind);
        Assert.Equal(SpanKind.Type, header.SpanAt("Calculator").Kind);
        Assert.True(header.SpanAt("Calculator").IsDefinition);

        var describe = document.Lines[document.LineContaining("return \"Total: \"")];
        Assert.Equal(SpanKind.String, describe.SpanAt("\"Total: \"").Kind);
        Assert.Equal(SpanKind.Number, describe.SpanAt("42").Kind);
        Assert.Equal(SpanKind.Property, describe.SpanAt("Count").Kind);

        var usings = document.Lines[document.LineContaining("using System.Collections.Generic;")];
        Assert.Equal(SpanKind.Namespace, usings.SpanAt("Collections").Kind);

        var add = document.Lines[document.LineContaining("public void Add(T item)")];
        Assert.Equal(SpanKind.Method, add.SpanAt("Add").Kind);
        Assert.Equal(SpanKind.Parameter, add.SpanAt("item").Kind);
        var field = document.Lines[document.LineContaining("private readonly List<T> _items")];
        Assert.Equal(SpanKind.Field, field.SpanAt("_items").Kind);
        Assert.Equal(SpanKind.Event, document.Lines[document.LineContaining("public event")].SpanAt("Changed").Kind);
    }

    [Fact]
    public async Task Spans_cover_every_line_exactly()
    {
        foreach (var language in new[] { DecompilationLanguage.CSharp, DecompilationLanguage.IL })
        {
            var document = await Trees.CalculatorDocumentAsync(language);
            foreach (var line in document.Lines)
            {
                var position = 0;
                foreach (var span in line.Spans)
                {
                    Assert.Equal(position, span.Start);
                    Assert.True(span.Length > 0);
                    position = span.End;
                }
                Assert.Equal(line.Text.Length, position);
                Assert.DoesNotContain('\n', line.Text);
            }
        }
    }

    [Fact]
    public async Task Member_nodes_decompile_on_their_own()
    {
        var assembly = Trees.TestAssembly;

        var calculator = await assembly.CalculatorAsync();
        var describe = await calculator.GetChildAsync("Describe() : string");
        var document = await describe.DecompileAsync(DecompilationLanguage.CSharp);

        Assert.Equal("[Description(\"Summarizes the calculator\")]", document.Lines[document.LineContaining("internal string Describe()") - 1].Text);
        Assert.DoesNotContain(document.Lines.ToArray(), l => l.Text.Contains("class"));
    }

    [Fact]
    public async Task A_positional_record_decompiles_with_its_parameters_in_its_header()
    {
        var assembly = Trees.TestAssembly;

        var fixtures = await assembly.FixturesAsync();
        var point = await fixtures.GetChildAsync("Point");
        var document = await point.DecompileAsync(DecompilationLanguage.CSharp);

        Assert.Null(document.Error);
        Assert.Contains("public record Point(int X, int Y)", Documents.GetText(document));
    }

    [Fact]
    public async Task An_older_language_version_avoids_newer_syntax()
    {
        var assembly = Trees.TestAssembly;
        var calculator = await assembly.CalculatorAsync();
        var csharp5 = new DecompilerOptions { LanguageVersion = CSharpVersion.CSharp5 };

        var count = await calculator.GetChildAsync("Count : int");
        var latest = Documents.GetText(await count.DecompileAsync(DecompilationLanguage.CSharp));
        var older = Documents.GetText(await count.DecompileAsync(DecompilationLanguage.CSharp, csharp5));
        var constructor = await calculator.GetChildAsync("Calculator()");
        var ctor = await constructor.DecompileAsync(DecompilationLanguage.CSharp, csharp5);

        Assert.Contains("public int Count => _items.Count;", latest);
        Assert.DoesNotContain("=>", older);
        Assert.Contains("return _items.Count;", older);
        Assert.Null(ctor.Error);
        Assert.Contains(ctor.Lines.ToArray(), l => l.Text.Contains("public Calculator()"));
    }

    [Fact]
    public void Toggles_come_from_the_decompiler_with_the_version_that_brought_each_feature()
    {
        var all = DecompilerToggle.All;

        Assert.Contains(all, t => t is { Name: "SwitchExpressions", Group: DecompilerToggleGroup.Language { IntroducedIn: CSharpVersion.CSharp8 }, DefaultValue: true });
        Assert.Contains(all, t => t is { Name: "AlwaysUseBraces", Group: DecompilerToggleGroup.Other });
        Assert.Contains(all, t => t is { Name: "RemoveDeadCode", Group: DecompilerToggleGroup.FSharp, DefaultValue: false });
        Assert.Contains(all, t => t is { Name: "ShowXmlDocumentation", Group: DecompilerToggleGroup.Other, DefaultValue: true });
        Assert.Contains(all, t => t is { Name: "UseDebugSymbols", Group: DecompilerToggleGroup.Other, DefaultValue: true });
        Assert.Contains(all, t => t is { Name: "AutoLoadAssemblyReferences", Group: DecompilerToggleGroup.Other, DefaultValue: true });
        Assert.DoesNotContain(all, t => t.Name is "DecompileMemberBodies");
    }

    [Fact]
    public async Task Locals_take_their_names_from_the_pdb_until_debug_symbols_are_off()
    {
        var fixtures = await Trees.TestAssembly.FixturesAsync();
        var grow = await fixtures.GetChildAsync("Circle", "Grow(double, int) : double");
        var off = DecompilerToggles.WithToggle(DecompilerOptions.Default, DecompilerToggle.UseDebugSymbols, false);

        var named = Documents.GetText(await grow.DecompileAsync(DecompilationLanguage.CSharp));
        var guessed = Documents.GetText(await grow.DecompileAsync(DecompilationLanguage.CSharp, off));

        Assert.Contains("double grown = Radius;", named);
        Assert.Contains("for (int step = 0; step < times; step++)", named);
        Assert.DoesNotContain("double grown", guessed);
        Assert.DoesNotContain("int step", guessed);
    }

    [Fact]
    public async Task IL_names_locals_from_the_pdb_until_debug_symbols_are_off()
    {
        var fixtures = await Trees.TestAssembly.FixturesAsync();
        var grow = await fixtures.GetChildAsync("Circle", "Grow(double, int) : double");
        var off = DecompilerToggles.WithToggle(DecompilerOptions.Default, DecompilerToggle.UseDebugSymbols, false);

        var named = Documents.GetText(await grow.DecompileAsync(DecompilationLanguage.IL));
        var unnamed = Documents.GetText(await grow.DecompileAsync(DecompilationLanguage.IL, off));

        Assert.Contains("[0] float64 grown,", named);
        Assert.Contains("[1] int32 step,", named);
        Assert.Contains("[0] float64,", unnamed);
    }

    [Fact]
    public void IL_comes_out_the_same_under_any_toggle_but_debug_symbols()
    {
        var braces = DecompilerToggles.FindToggle("AlwaysUseBraces");
        Assert.NotNull(braces);
        var bracesOff = DecompilerToggles.WithToggle(DecompilerOptions.Default, braces, false);
        var symbolsOff = DecompilerToggles.WithToggle(DecompilerOptions.Default, DecompilerToggle.UseDebugSymbols, false);

        Assert.True(DecompilerToggles.ProducesSameCode(DecompilerOptions.Default, bracesOff, DecompilationLanguage.IL));
        Assert.False(DecompilerToggles.ProducesSameCode(DecompilerOptions.Default, bracesOff, DecompilationLanguage.CSharp));
        Assert.False(DecompilerToggles.ProducesSameCode(DecompilerOptions.Default, bracesOff, DecompilationLanguage.ILWithCSharp));
        Assert.False(DecompilerToggles.ProducesSameCode(DecompilerOptions.Default, symbolsOff, DecompilationLanguage.IL));
    }

    // The type system settings choose is a shared read of its own (CJ8), awaited by each unit before its read starts.
    [Fact]
    public async Task The_type_system_settings_choose_is_one_read_every_unit_shares_and_the_modules_own_for_the_defaults()
    {
        var dynamic = DecompilerToggles.FindToggle("Dynamic");
        Assert.NotNull(dynamic);
        var dynamicOff = Settings.Create(DecompilerToggles.WithToggle(DecompilerOptions.Default, dynamic, false));
        using var firstReading = LoadedAssemblies.BeginReading();
        using var secondReading = LoadedAssemblies.BeginReading();

        var own = await LoadedAssemblies.ReadTypeSystemAsync(firstReading, Trees.TestAssembly, CancellationToken.None);
        var first = await LoadedAssemblies.ReadChosenTypeSystemAsync(firstReading, Trees.TestAssembly, dynamicOff, CancellationToken.None);
        var second = await LoadedAssemblies.ReadChosenTypeSystemAsync(secondReading, Trees.TestAssembly, dynamicOff, CancellationToken.None);
        var defaults = await LoadedAssemblies.ReadChosenTypeSystemAsync(secondReading, Trees.TestAssembly, Settings.Create(), CancellationToken.None);

        var ownTypeSystem = Assert.IsType<TypeSystemRead.Read>(own).TypeSystem;
        var chosen = Assert.IsType<TypeSystemRead.Read>(first).TypeSystem;
        Assert.Same(chosen, Assert.IsType<TypeSystemRead.Read>(second).TypeSystem);
        Assert.NotSame(ownTypeSystem, chosen);
        Assert.Same(ownTypeSystem, Assert.IsType<TypeSystemRead.Read>(defaults).TypeSystem);
    }

    [Fact]
    public async Task Code_decompiled_with_settings_that_choose_another_type_system_is_written_in_CSharp_and_in_IL_with_CSharp()
    {
        var dynamic = DecompilerToggles.FindToggle("Dynamic");
        Assert.NotNull(dynamic);
        var dynamicOff = DecompilerToggles.WithToggle(DecompilerOptions.Default, dynamic, false);
        var calculator = await Trees.TestAssembly.CalculatorAsync();

        var csharp = await calculator.DecompileAsync(DecompilationLanguage.CSharp, dynamicOff);
        var ilWithCSharp = await calculator.DecompileAsync(DecompilationLanguage.ILWithCSharp, dynamicOff);

        Assert.Null(csharp.Error);
        Assert.Contains("class Calculator", Documents.GetText(csharp));
        Assert.Null(ilWithCSharp.Error);
        Assert.Contains("_items.Add(item);", Documents.GetText(ilWithCSharp));
    }

    [Fact]
    public async Task IL_with_CSharp_writes_each_statement_above_its_first_instruction_in_CSharps_colors()
    {
        var document = await CircleGrowAsync(DecompilationLanguage.ILWithCSharp);

        var statement = document.LineContaining("// double grown = Radius;");
        var line = document.Lines[statement];
        var instruction = document.Lines[statement + 1];

        Assert.StartsWith("IL_0001: ldarg.0", instruction.Text.Trim());
        Assert.Equal(SpanKind.ILOpcode, document.SpanAt(statement + 1, instruction.Text.IndexOf("ldarg", StringComparison.Ordinal)).Kind);
        Assert.Equal(SpanKind.Keyword, line.SpanAt("double").Kind);
        Assert.Equal(SpanKind.Property, line.SpanAt("Radius").Kind);
        Assert.NotNull(line.SpanAt("Radius").Reference);
        Assert.Null(line.SpanAt("grown").Reference);
    }

    [Fact]
    public async Task IL_with_CSharp_dims_what_a_line_holds_besides_the_statement()
    {
        var document = await CircleGrowAsync(DecompilationLanguage.ILWithCSharp);

        var line = document.LineContaining("// for (int step = 0;");
        var text = document.Lines[line].Text;

        Assert.Equal(SpanKind.Comment, document.SpanAt(line, text.IndexOf("for (", StringComparison.Ordinal)).Kind);
        Assert.Equal(SpanKind.Keyword, document.SpanAt(line, text.IndexOf("int", StringComparison.Ordinal)).Kind);
        Assert.Equal(SpanKind.Comment, document.SpanAt(line, text.IndexOf("step++", StringComparison.Ordinal)).Kind);
    }

    [Fact]
    public async Task IL_with_CSharp_says_where_an_instruction_has_no_CSharp()
    {
        var document = await CircleGrowAsync(DecompilationLanguage.ILWithCSharp);

        Assert.Contains(document.Lines, l => l.Text.Trim() == "// (no C# code)");
        Assert.DoesNotContain(document.Lines, l => l.Text.Contains("double grown = Radius;", StringComparison.Ordinal) && !l.Text.TrimStart().StartsWith("//"));
    }

    private static async Task<DecompiledDocument> CircleGrowAsync(DecompilationLanguage language)
    {
        var fixtures = await Trees.TestAssembly.FixturesAsync();
        var circle = await fixtures.GetChildAsync("Circle");
        var children = await circle.ListChildrenAsync();
        var grow = children.Single(c => c.Text.StartsWith("Grow", StringComparison.Ordinal));
        return await grow.DecompileAsync(language);
    }

    [Fact]
    public async Task Xml_documentation_sits_above_its_member_as_a_comment()
    {
        var document = await Trees.CalculatorDocumentAsync(DecompilationLanguage.CSharp);

        const string comment = "/// <summary>How many items were added.</summary>";
        var summary = document.Lines[document.LineContaining("public int Count =>") - 1];

        Assert.Equal(comment, summary.Text.Trim());
        Assert.Equal(SpanKind.Comment, summary.SpanAt(comment).Kind);
    }

    [Fact]
    public async Task Xml_documentation_over_several_lines_folds_collapsed_to_its_first_line()
    {
        var document = await Trees.CalculatorDocumentAsync(DecompilationLanguage.CSharp);

        var docs = document.FoldAt("/// <summary>Adds an item");

        Assert.True(docs.DefaultCollapsed);
        Assert.Equal("…", docs.CollapsedText);
        Assert.Contains("<param name=\"item\">", document.Lines[docs.EndLine].Text);
        Assert.Contains("public void Add(T item)", document.Lines[docs.EndLine + 1].Text);
    }

    [Fact]
    public async Task Turning_xml_documentation_off_leaves_it_out()
    {
        var calculator = await Trees.TestAssembly.CalculatorAsync();
        var count = await calculator.GetChildAsync("Count : int");
        var docs = DecompilerToggles.FindToggle("ShowXmlDocumentation");
        Assert.NotNull(docs);
        var off = DecompilerToggles.WithToggle(DecompilerOptions.Default, docs, false);

        Assert.Contains("/// <summary>How many items were added.</summary>", Documents.GetText(await count.DecompileAsync(DecompilationLanguage.CSharp)));
        Assert.DoesNotContain("///", Documents.GetText(await count.DecompileAsync(DecompilationLanguage.CSharp, off)));
    }

    [Fact]
    public async Task Runtime_assemblies_take_their_xml_documentation_from_the_reference_pack()
    {
        var coreLib = new AssemblyList { Runtime = TestRuntime.Installed }.Open(Trees.CoreLibPath);

        var coreLibRoot = await coreLib.ReadRootAsync();
        var emptyNode = await coreLibRoot.GetChildAsync("System", "String", "Empty : string");
        var empty = await emptyNode.DecompileAsync(DecompilationLanguage.CSharp);

        Assert.True(empty.LineContaining("/// <summary>") < empty.LineContaining("public static readonly string Empty;"));
    }

    [Fact]
    public async Task A_toggle_changes_the_code_until_the_version_is_too_old_for_it_and_keeps_its_state()
    {
        var assembly = Trees.TestAssembly;
        var calculator = await assembly.CalculatorAsync();
        var count = await calculator.GetChildAsync("Count : int");
        var bodies = DecompilerToggles.FindToggle("UseExpressionBodyForCalculatedGetterOnlyProperties");
        Assert.NotNull(bodies);
        var off = DecompilerToggles.WithToggle(DecompilerOptions.Default, bodies, false);
        var offOnCSharp5 = off with { LanguageVersion = CSharpVersion.CSharp5 };

        Assert.Contains("return _items.Count;", Documents.GetText(await count.DecompileAsync(DecompilationLanguage.CSharp, off)));
        Assert.False(DecompilerToggles.Allows(offOnCSharp5, bodies));
        Assert.False(DecompilerToggles.IsOn(offOnCSharp5, bodies));
        Assert.True(DecompilerToggles.IsOn(DecompilerOptions.Default, bodies));
        Assert.Equal(DecompilerOptions.Default, DecompilerToggles.WithToggle(off, bodies, true));
        Assert.Equal(off, DecompilerToggles.WithToggle(DecompilerOptions.Default, bodies, false));
    }

    [Fact]
    public async Task IL_shows_opcodes_labels_and_directives()
    {
        var assembly = Trees.TestAssembly;

        var calculator = await assembly.CalculatorAsync();
        var describe = await calculator.GetChildAsync("Describe() : string");
        var document = await describe.DecompileAsync(DecompilationLanguage.IL);

        Assert.Equal(DecompilationLanguage.IL, document.Language);
        var spans = document.SpansOf().ToArray();
        Assert.Contains(spans, s => s.Span.Kind == SpanKind.ILOpcode && s.Text == "ret");
        Assert.Contains(spans, s => s.Span.Kind == SpanKind.ILLabel && s.Text == "IL_0000" && s.Span.IsDefinition);
        Assert.Contains(spans, s => s.Span.Kind == SpanKind.Keyword && s.Text == ".maxstack");
        Assert.Contains(spans, s => s.Span.Kind == SpanKind.String && s.Text == "\"Total: \"");
        Assert.Contains(spans, s => s.Span.Kind == SpanKind.Comment && s.Text.StartsWith("// Method begins"));
        Assert.Contains(spans, s => s.Span.Kind == SpanKind.Method && s.Text == "get_Count" && s.Span.Reference is not null);
    }

    [Fact]
    public async Task The_assembly_node_shows_a_summary_and_assembly_attributes()
    {
        var assembly = Trees.TestAssembly;

        var root = await assembly.ReadRootAsync();
        var csharp = Documents.GetText(await root.DecompileAsync(DecompilationLanguage.CSharp));
        var il = Documents.GetText(await root.DecompileAsync(DecompilationLanguage.IL));

        Assert.Contains($"// {Trees.TestAssemblyPath}", csharp);
        Assert.Contains("[assembly: AssemblyProduct(\"ILSight.Tests\")]", csharp);
        Assert.Contains(".assembly extern System.Runtime", il);
        Assert.Contains(".assembly ILSight.Tests", il);
    }

    [Fact]
    public async Task A_namespace_lists_its_types_as_links_to_them()
    {
        var (list, assembly) = Trees.OpenTestAssembly();
        var fixtures = await assembly.FixturesAsync();

        var document = await fixtures.DecompileAsync(DecompilationLanguage.CSharp);
        var line = document.Lines[document.LineContaining("internal sealed class Circle")];
        var circle = line.SpanAt("Circle");

        Assert.Equal("namespace ILSight.Tests.Fixtures", document.Lines[0].Text);
        Assert.False(circle.IsDefinition);
        var circleNode = await fixtures.GetChildAsync("Circle");
        Assert.Same(circleNode, await list.FindNodeAsync(Assert.IsAssignableFrom<CodeReference.Navigable>(circle.Reference)));
    }

    [Fact]
    public async Task Folders_and_resources_decompile_to_summaries()
    {
        var coreLib = new AssemblyList { Runtime = TestRuntime.Installed }.Open(Trees.CoreLibPath);

        var root = await Trees.TestAssembly.ReadRootAsync();
        var referencesNode = await root.GetChildAsync("References");
        var references = await referencesNode.DecompileAsync(DecompilationLanguage.CSharp);
        var coreLibRoot = await coreLib.ReadRootAsync();
        var resources = await coreLibRoot.GetChildAsync("Resources");
        var resourceFiles = await resources.ListChildrenAsync();
        var strings = await resourceFiles.First(r => r.Text.EndsWith(".resources")).DecompileAsync(DecompilationLanguage.CSharp);

        Assert.Contains(references.SpansOf(), s => s.Text.StartsWith("System.Runtime, Version=") && s.Span.Reference is CodeReference.Assembly);
        Assert.Null(strings.Error);
        Assert.Contains(strings.SpansOf(), s => s.Span.Kind == SpanKind.String);
    }

    [Fact]
    public async Task Cancellation_throws_instead_of_returning_a_document()
    {
        var (_, assembly) = Trees.OpenTestAssembly();
        var calculator = await assembly.CalculatorAsync();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => calculator.DecompileAsync(DecompilationLanguage.CSharp, cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            NodeContentLoader.ReadContentAsync(TestSession.Shared, calculator, DecompilationLanguage.IL, DecompilerOptions.Default, EnglishNotes.Instance, NodeQueries.FirstGridRows, cancelled.Token));
    }

    [Fact]
    public async Task Concurrent_decompiles_match_sequential_ones()
    {
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var coreLib = list.Open(Trees.CoreLibPath);
        var coreLibRoot = await coreLib.ReadRootAsync();
        var generic = await coreLibRoot.GetChildAsync("System.Collections.Generic");
        var children = await generic.ListChildrenAsync();
        var types = children.Take(12).ToArray();
        var languages = new[] { DecompilationLanguage.CSharp, DecompilationLanguage.IL };

        var sequential = new List<string>();
        foreach (var type in types)
        {
            foreach (var language in languages)
            {
                var document = await type.DecompileAsync(language);
                sequential.Add(Documents.GetText(document));
            }
        }

        var concurrent = (await Task.WhenAll(types.SelectMany(t => languages.Select(l => NodeContentLoader.ReadContentAsync(TestSession.Shared, t, l, DecompilerOptions.Default, EnglishNotes.Instance, NodeQueries.FirstGridRows).GetValueAsync()))).WithinPatienceAsync())
            .Select(c => Assert.IsType<NodeContent.Code>(c).Document).ToArray();

        Assert.Equal(sequential, concurrent.Select(d => Documents.GetText(d)));
        Assert.DoesNotContain(concurrent, d => d.Error is not null);
    }

    [Fact]
    public async Task Failures_become_an_error_document_saying_the_cause_by_its_message_the_exception_kept_apart()
    {
        using var temp = new TempDir();
        var corrupt = temp.Write("corrupt.dll", WithBrokenAttribute(await File.ReadAllBytesAsync(Trees.TestAssemblyPath), "Describe"));
        var assembly = new AssemblyList { Runtime = TestRuntime.Installed }.Open(corrupt);
        var calculator = await assembly.CalculatorAsync();
        var describe = await calculator.GetChildAsync("Describe() : string");

        var il = await describe.DecompileAsync(DecompilationLanguage.IL);
        var csharp = await describe.DecompileAsync(DecompilationLanguage.CSharp);

        Assert.NotNull(il.Error);
        Assert.Equal("Describe() : string", il.Title);
        Assert.StartsWith("// Error decompiling", il.Lines[0].Text);
        Assert.IsType<BadImageFormatException>(il.Error);
        Assert.Equal($"// Cause: {il.Error.Message}", il.Lines[1].Text);
        Assert.DoesNotContain(nameof(BadImageFormatException), Documents.GetText(il));
        Assert.DoesNotContain("   at ", Documents.GetText(il));
        Assert.Null(csharp.Error);
        Assert.Contains("Could not decode attribute arguments", Documents.GetText(csharp));
    }

    [Fact]
    public async Task A_reference_whose_file_is_there_but_unreadable_is_read_by_name_only_said_once_and_fails_a_lookup_that_needs_it()
    {
        using var temp = new TempDir();
        var sample = temp.Copy(Trees.PathsBeforePath, "PathsSample.dll");
        temp.Write("PathsLibrary.dll", "no assembly");
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var assembly = list.Open(sample);
        var root = await assembly.ReadRootAsync();
        var overloads = await root.GetChildAsync("PathsSample", "Overloads");

        var document = await overloads.DecompileAsync(DecompilationLanguage.CSharp);
        var again = await overloads.DecompileAsync(DecompilationLanguage.IL);

        Assert.Null(document.Error);
        Assert.StartsWith("// Warning: the reference PathsLibrary was read by name only", document.Lines[0].Text);
        Assert.Contains("PathsLibrary.dll could not be read", document.Lines[0].Text);
        Assert.EndsWith("Reload PathsSample.dll once it can be read.", document.Lines[0].Text);
        Assert.Contains("Take(", Documents.GetText(document));
        var unread = Assert.Single(document.UnreadReferences);
        Assert.Equal("PathsLibrary", unread.Name);
        Assert.IsType<BadImageFormatException>(unread.Error.InnerException);
        Assert.Same(unread, Assert.Single(again.UnreadReferences));
        Assert.True(unread.TryTakeReport());
        Assert.False(unread.TryTakeReport());

        var widget = new CodeReference.Entity { Name = "Widget", Kind = EntityKind.Type, FilePath = AssemblyPath.Of(sample), Target = GetTypeReference(sample, "Widget") };
        var described = await AssemblyListing.DescribeReferenceAsync(list, widget, CancellationToken.None);
        var failed = Assert.IsType<Outcome<Lookup<CodeTooltip>>.Failed>(described);
        Assert.Same(unread.Error, failed.Error.InnerException);
    }

    [Fact]
    public async Task A_reference_not_found_is_named_in_the_code_as_not_found_and_reported_as_nothing()
    {
        using var temp = new TempDir();
        var sample = temp.Copy(Trees.PathsBeforePath, "PathsSample.dll");
        var assembly = new AssemblyList { Runtime = TestRuntime.Installed }.Open(sample);
        var root = await assembly.ReadRootAsync();
        var overloads = await root.GetChildAsync("PathsSample", "Overloads");

        var document = await overloads.DecompileAsync(DecompilationLanguage.CSharp);

        Assert.Null(document.Error);
        Assert.Contains(document.Lines, l => l.Text == "// Warning: the reference PathsLibrary was read by name only: it was not found.");
        Assert.Empty(document.UnreadReferences);
    }

    private static EntityHandle GetTypeReference(string path, string name)
    {
        using var pe = new PEReader(File.OpenRead(path));
        var metadata = pe.GetMetadataReader();
        return metadata.TypeReferences.First(h => metadata.GetString(metadata.GetTypeReference(h).Name) == name);
    }

    [Fact]
    public async Task Every_node_without_code_is_summarized_in_both_languages()
    {
        using var temp = new TempDir();
        var package = temp.Write("Sample.1.0.0.nupkg", []);
        await using (var archive = await ZipFile.OpenAsync(package, ZipArchiveMode.Update))
        {
            await archive.CreateEntryFromFileAsync(Trees.TestAssemblyPath, "lib/net10.0/ILSight.Tests.dll");
            await using var writer = new StreamWriter(await archive.CreateEntry("readme.txt").OpenAsync());
            await writer.WriteAsync("Read me");
        }
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var roots = new List<BrowseNode>();
        foreach (var path in new[] { Trees.TestAssemblyPath, Trees.SampleAppPath, package })
        {
            var assembly = list.Open(path);
            var root = await assembly.ReadRootAsync();
            roots.Add(root);
        }

        var failed = new List<string>();
        foreach (var node in await ListSummarizedAsync(roots))
        {
            foreach (var language in new[] { DecompilationLanguage.CSharp, DecompilationLanguage.IL })
            {
                var document = await node.DecompileAsync(language);
                if (document.Error is not null)
                {
                    failed.Add($"{node.GetType().Name} {node.Text}");
                }
            }
        }

        Assert.Empty(failed);
    }

    private static bool IsModule(BrowseNode node)
    {
        return node.Subject is NodeSubject.Module;
    }

    private static bool IsLeafToSummarize(BrowseNode node)
    {
        return node.Subject is NodeSubject.MemberDefinition or NodeSubject.BaseType or NodeSubject.DerivedType or NodeSubject.AssemblyReference;
    }

    private static async Task<List<BrowseNode>> ListSummarizedAsync(IEnumerable<BrowseNode> roots)
    {
        var nodes = new List<BrowseNode>();
        var pending = new Stack<BrowseNode>(roots);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            var entity = BrowseNodes.FindEntity(node);
            if (entity is null && !IsModule(node))
            {
                nodes.Add(node);
            }

            // Members, base and derived types and references lead into what they name.
            if (IsLeafToSummarize(node))
            {
                continue;
            }

            foreach (var child in await node.ListChildrenAsync())
            {
                pending.Push(child);
            }
        }
        return nodes;
    }

    private static byte[] WithBrokenAttribute(byte[] image, string methodName)
    {
        using var pe = new PEReader(new MemoryStream(image));
        var reader = pe.GetMetadataReader();
        var method = reader.MethodDefinitions.First(h => reader.GetString(reader.GetMethodDefinition(h).Name) == methodName);
        var attribute = reader.GetMethodDefinition(method).GetCustomAttributes().Single();
        var rowSize = reader.GetTableRowSize(TableIndex.CustomAttribute);
        var blobHeapSize = reader.GetHeapSize(HeapIndex.Blob);
        var blobIndexSize = blobHeapSize < 0x10000 ? 2 : 4;
        var offset = pe.PEHeaders.MetadataStartOffset + reader.GetTableMetadataOffset(TableIndex.CustomAttribute) +
                     rowSize * (MetadataTokens.GetRowNumber(attribute) - 1) + rowSize - blobIndexSize;
        image.AsSpan(offset, blobIndexSize).Fill(0xFF);
        return image;
    }

    [Fact]
    public void Default_changes_hold_nothing_and_a_toggle_set_back_to_its_default_goes()
    {
        var bodies = DecompilerToggles.FindToggle("UseExpressionBodyForCalculatedGetterOnlyProperties");
        Assert.NotNull(bodies);

        OnOffChanges none = default;
        var set = none.With([bodies], !bodies.DefaultValue);
        var back = set.With([bodies], bodies.DefaultValue);

        Assert.Equal(0, none.Count);
        Assert.False(none.Contains(bodies));
        Assert.True(set.Contains(bodies));
        Assert.Equal(0, back.Count);
    }

    [Fact]
    public void Options_copied_with_with_keep_holding_only_what_differs()
    {
        var bodies = DecompilerToggles.FindToggle("UseExpressionBodyForCalculatedGetterOnlyProperties");
        Assert.NotNull(bodies);
        var set = DecompilerToggles.WithToggle(DecompilerOptions.Default, bodies, !bodies.DefaultValue);

        var copy = set with { Changes = set.Changes.With([bodies], bodies.DefaultValue) };

        Assert.False(copy.HasChanges);
        Assert.Equal(DecompilerOptions.Default, copy);
    }

    [Fact]
    public async Task A_module_the_library_cannot_read_shows_why_as_its_document()
    {
        using var temp = new TempDir();
        var unreadablePath = BrokenAssemblies.WriteUnreadableTypeSystem(temp, Trees.TestAssemblyPath);
        var module = new AssemblyList { Runtime = TestRuntime.Installed }.OpenModule(unreadablePath);

        var root = await module.ReadRootAsync();
        var document = await root.DecompileAsync(DecompilationLanguage.CSharp);

        Assert.NotNull(document.Error);
        Assert.StartsWith("// Error decompiling", document.Lines[0].Text);
    }
}
