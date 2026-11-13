using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using ICSharpCode.Decompiler.Metadata;
using ILSight.Core;
using ILSight.Core.Metadata;

namespace ILSight.Tests;

public sealed class MetadataTests
{
    private static async Task<BrowseNode> MetadataAsync(LoadedAssembly assembly)
    {
        return await assembly.ReadRootAsync().GetChildAsync("Metadata");
    }

    private static MetadataFolderInfo FolderOf(BrowseNode node)
    {
        return Assert.IsType<NodeSubject.Metadata>(node.Subject).Value;
    }

    private static async Task<BrowseNode> TableAsync(LoadedAssembly assembly, string name)
    {
        var tables = await MetadataAsync(assembly).GetChildAsync("Tables");
        return await tables.TableAsync(name);
    }

    private static async Task<MetadataGrid> GridAsync(BrowseNode node)
    {
        return Assert.IsType<NodeContent.Table>(await node.ReadContentAsync()).Grid;
    }

    private static async Task<MetadataGrid> TypeDefsAsync()
    {
        return await GridAsync(await TableAsync(Trees.TestAssembly, "TypeDef"));
    }

    [Fact]
    public async Task A_cell_whose_read_fails_shows_a_question_mark_and_why_on_hover()
    {
        using var temp = new TempDir();
        var assembly = new AssemblyList { Runtime = TestRuntime.Installed }.Open(BrokenAssemblies.WriteUnreadableParameterName(temp, Trees.TestAssemblyPath));
        var grid = await GridAsync(await TableAsync(assembly, "Param"));
        var name = grid.Column("Name");

        var text = MetadataGrids.GetText(grid, 0, name);
        var tooltip = MetadataGrids.FindTooltip(grid, 0, name);

        Assert.IsType<MetadataGrid.InMemory>(grid);
        Assert.Equal("?", text);
        Assert.NotNull(tooltip);
        Assert.False(string.IsNullOrWhiteSpace(tooltip.Text));
        Assert.Equal("?", MetadataGrids.GetText(grid, 0, grid.Column("Name")));
        Assert.NotEqual("?", MetadataGrids.GetText(grid, 1, name));
        Assert.NotEqual("?", MetadataGrids.GetText(grid, 0, grid.Column("Sequence")));
    }

    [Fact]
    public async Task An_assembly_lists_its_metadata_and_its_portable_pdb_first()
    {
        var assembly = Trees.TestAssembly;

        var first = (await assembly.ReadRootAsync().ListChildrenAsync()).Take(3).Select(static c => (c.Subject.GetType(), c.Text));

        Assert.Equal(
            [
                (typeof(NodeSubject.Metadata), "Metadata"), (typeof(NodeSubject.Metadata), "Debug Metadata (From portable PDB)"),
                (typeof(NodeSubject.References), "References"),
            ],
            first);
    }

    [Fact]
    public async Task The_metadata_folder_lists_headers_directories_tables_and_heaps_in_ILSpys_order()
    {
        var assembly = Trees.TestAssembly;

        var children = await MetadataAsync(assembly).ListChildrenAsync();

        Assert.Equal(
            [
                (MetadataGridRole.Header, "DOS Header"), (MetadataGridRole.Header, "COFF Header"), (MetadataGridRole.Header, "Optional Header"),
                (MetadataGridRole.Directory, "Data Directories"), (MetadataGridRole.Directory, "Debug Directory"),
            ],
            children.Take(5).Select(static c => (c.MetadataRole, c.Text)));
        Assert.IsType<NodeSubject.MetadataTables>(children[5].Subject);
        Assert.Equal("Tables", children[5].Text);
        Assert.Equal(["String Heap", "UserString Heap", "Guid Heap", "Blob Heap"], children.Skip(6).Select(c => c.Text[..c.Text.IndexOf(" (")]));
        Assert.All(children.Skip(6), c => Assert.Equal(MetadataGridRole.Heap, c.MetadataRole));
        Assert.Contains(await MetadataAsync(assembly).GetChildAsync("Tables").ListChildrenAsync(), c => c.Text.StartsWith("02 TypeDef (", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Every_table_is_listed_and_an_empty_one_shows_only_that_it_has_no_rows()
    {
        var assembly = Trees.TestAssembly;

        var tables = await MetadataAsync(assembly).GetChildAsync("Tables").ListChildrenAsync();
        var empty = await TableAsync(assembly, "FieldPtr");

        Assert.Equal(Enum.GetValues<TableIndex>().Length, tables.Count);
        Assert.True(Assert.IsType<NodeSubject.MetadataTable>(empty.Subject).Value.IsEmptyTable);
        Assert.Equal("03 FieldPtr (0)", empty.Text);
        Assert.Contains("FieldPtr: 0 rows", Documents.GetText(await empty.DecompileAsync(DecompilationLanguage.CSharp)));
        Assert.False(Assert.IsType<NodeSubject.MetadataTable>((await TableAsync(assembly, "TypeDef")).Subject).Value.IsEmptyTable);
    }

    [Fact]
    public async Task Every_grid_reads_every_cell_tooltip_and_link()
    {
        var assembly = Trees.TestAssembly;
        var grids = 0;

        foreach (var root in (await assembly.ReadRootAsync().ListChildrenAsync()).Where(c => c.Subject is NodeSubject.Metadata))
        {
            grids += await SweepAsync(root);
        }

        Assert.True(grids > 20, $"{grids} grids");

        static async Task<int> SweepAsync(BrowseNode node)
        {
            var count = 0;
            var content = await node.ReadContentAsync();
            if (content is NodeContent.Table { Grid: var grid })
            {
                for (var row = 0; row < grid.RowCount; row++)
                {
                    for (var column = 0; column < grid.Columns.Count; column++)
                    {
                        Assert.NotNull(MetadataGrids.GetText(grid, row, column));
                        MetadataGrids.FindTooltip(grid, row, column);
                        var link = MetadataGrids.FindLink(grid, row, column);
                        if (link is not null)
                        {
                            var target = Assert.IsType<MetadataTarget>(TestSession.Shared.FindMetadataRow(node, link.Value));
                            Assert.InRange(target.Row, 0, (await GridAsync(target.Node)).RowCount - 1);
                        }
                    }
                    MetadataGrids.FindDefinition(grid, row);
                }
                count++;
            }
            else
            {
                Assert.Null(Assert.IsType<NodeContent.Code>(content).Document.Error);
            }
            foreach (var child in await node.ListChildrenAsync())
            {
                count += await SweepAsync(child);
            }

            return count;
        }
    }

    [Fact]
    public async Task The_TypeDef_table_shows_ILSpys_columns_in_hex()
    {
        var grid = await TypeDefsAsync();
        var row = grid.RowNamed("Calculator`1");

        Assert.Equal(["RID", "Token", "Offset", "Attributes", "Name", "Namespace", "BaseType", "FieldList", "MethodList"], grid.Columns.Select(c => c.Name));
        Assert.Equal((row + 1).ToString(), MetadataGrids.GetText(grid, row, grid.Column("RID")));
        Assert.Equal($"02{row + 1:X6}", MetadataGrids.GetText(grid, row, grid.Column("Token")));
        Assert.Equal(Trees.FixturesNamespace, MetadataGrids.GetText(grid, row, grid.Column("Namespace")));
        Assert.True(grid.Columns[grid.Column("BaseType")].IsToken);
        Assert.False(grid.Columns[grid.Column("Name")].IsToken);
    }

    [Fact]
    public async Task A_flags_cell_explains_each_group_of_its_bits()
    {
        var grid = await TypeDefsAsync();

        var tooltip = MetadataGrids.FindTooltip(grid, grid.RowNamed("Calculator`1"), grid.Column("Attributes"));
        Assert.NotNull(tooltip);

        Assert.Equal(["Visibility", "Layout", "ClassSemantics", "StringFormat", "Flags"], tooltip.Groups.Select(g => g.Header));
        var visibility = tooltip.Groups[0];
        Assert.True(visibility.IsChoice);
        Assert.Equal("Public (0001)", Assert.Single(visibility.Flags).Name);
        Assert.Equal("AutoLayout (0000)", Assert.Single(tooltip.Groups[1].Flags).Name);
        var flags = tooltip.Groups[^1];
        Assert.False(flags.IsChoice);
        Assert.Contains(new MetadataFlag { Name = "BeforeFieldInit (100000)", IsSet = true }, flags.Flags);
        Assert.Contains(new MetadataFlag { Name = "Abstract (0080)", IsSet = false }, flags.Flags);
        Assert.Contains(new MetadataFlag { Name = "IsTypeForwarder (200000)", IsSet = false }, flags.Flags);
    }

    [Fact]
    public void A_flags_schema_keeps_bits_that_combine_out_of_its_choice_groups()
    {
        var method = FlagsSchemaInferer.For(typeof(System.Reflection.MethodAttributes));
        var generic = FlagsSchemaInferer.For(typeof(System.Reflection.GenericParameterAttributes));
        var import = FlagsSchemaInferer.For(typeof(System.Reflection.MethodImportAttributes));

        Assert.Equal(["MemberAccess", "VtableLayout"], method.MutexGroups.Select(g => g.Name));
        Assert.Contains(method.IndependentFlags, f => f.Name == "RTSpecialName");
        Assert.Equal(["Variance"], generic.MutexGroups.Select(g => g.Name));
        Assert.Equal("None (0000)", generic.MutexGroups[0].Values[0].Label);
        Assert.Contains(generic.IndependentFlags, f => f.Name == "DefaultConstructorConstraint");
        Assert.Equal(["None (0000)", "Ansi (0002)", "Unicode (0004)", "Auto (0006)"], import.MutexGroups.Single(g => g.Name == "CharSet").Values.Select(v => v.Label));
        Assert.True(FlagsSchemaInferer.IsFlags(typeof(System.Reflection.MethodImplAttributes)));
        Assert.DoesNotContain(FlagsSchemaInferer.For(typeof(System.Reflection.MethodImplAttributes)).IndependentFlags, f => f.Name == "MaxMethodImplVal");
        Assert.False(FlagsSchemaInferer.IsFlags(typeof(System.Reflection.AssemblyHashAlgorithm)));
    }

    [Fact]
    public async Task A_token_cell_links_to_the_row_of_the_table_it_names()
    {
        var assembly = Trees.TestAssembly;
        var types = await TableAsync(assembly, "TypeDef");
        var grid = await GridAsync(types);
        var row = grid.RowNamed("Calculator`1");

        var link = Assert.IsType<MetadataAddress>(MetadataGrids.FindLink(grid, row, grid.Column("BaseType")));
        var target = Assert.IsType<MetadataTarget>(TestSession.Shared.FindMetadataRow(types, link));

        Assert.Same(await TableAsync(assembly, "TypeRef"), target.Node);
        var typeRefs = await GridAsync(target.Node);
        Assert.Equal("Object", MetadataGrids.GetText(typeRefs, target.Row, typeRefs.Column("Name")));
        Assert.Equal(target.Row, MetadataGrids.FindRow(typeRefs, Convert.ToInt32(MetadataGrids.GetText(grid, row, grid.Column("BaseType")), 16)));
    }

    [Fact]
    public async Task A_row_goes_to_the_definition_its_token_names()
    {
        var (list, assembly) = Trees.OpenTestAssembly();
        var types = await GridAsync(await TableAsync(assembly, "TypeDef"));
        var references = await GridAsync(await TableAsync(assembly, "TypeRef"));

        var calculator = MetadataGrids.FindDefinition(types, types.RowNamed("Calculator`1"));
        Assert.NotNull(calculator);
        var obj = MetadataGrids.FindDefinition(references, references.RowNamed("Object"));
        Assert.NotNull(obj);

        Assert.Same(await assembly.CalculatorAsync(), await list.FindNodeAsync(calculator));
        Assert.Equal("Object", (await list.FindNodeAsync(obj))?.Text);
        Assert.Null(MetadataGrids.FindDefinition(await GridAsync(await MetadataAsync(assembly).GetChildAsync("DOS Header")), 0));
    }

    [Fact]
    public async Task Sorting_orders_rows_by_a_column_keeping_ties_in_table_order()
    {
        var grid = await TypeDefsAsync();
        var name = grid.Column("Name");

        var ascending = MetadataGrids.OrderRows(grid, name, descending: false).Select(r => MetadataGrids.GetText(grid, r, name)).ToList();
        var descending = MetadataGrids.OrderRows(grid, name, descending: true).Select(r => MetadataGrids.GetText(grid, r, name)).ToList();

        Assert.Equal(ascending.Order(StringComparer.CurrentCulture), ascending);
        Assert.Equal(ascending.OrderDescending(StringComparer.CurrentCulture), descending);
        var ties = MetadataGrids.OrderRows(grid, name, descending: true).Where(r => MetadataGrids.GetText(grid, r, name) == "<>c").ToList();
        Assert.Equal(ties.Order(), ties);
    }

    [Fact]
    public async Task The_debug_directory_decodes_its_CodeView_entry()
    {
        var assembly = Trees.TestAssembly;
        var directory = await MetadataAsync(assembly).GetChildAsync("Debug Directory");

        var codeView = Documents.GetText(await directory.GetChildAsync("CodeView").DecompileAsync(DecompilationLanguage.CSharp));

        Assert.Contains("// GUID: ", codeView);
        Assert.Contains("// Path: ", codeView);
        var grid = await GridAsync(directory);
        Assert.Contains("CodeView", Enumerable.Range(0, grid.RowCount).Select(r => MetadataGrids.GetText(grid, r, grid.Column("Type"))));
    }

    [Fact]
    public async Task The_debug_directory_shows_the_embedded_pdb_the_assembly_already_loaded()
    {
        var assembly = new AssemblyList { Runtime = TestRuntime.Installed }.Open(Trees.EmbeddedPdbPath);
        var atRoot = FolderOf(await assembly.ReadRootAsync().GetChildAsync("Debug Metadata (Embedded)"));

        var directory = await MetadataAsync(assembly).GetChildAsync("Debug Directory");
        var embedded = FolderOf(await directory.GetChildAsync("Debug Metadata (Embedded)"));

        Assert.Same(atRoot.File, embedded.File);
    }

    [Fact]
    public async Task The_tables_folder_counts_the_rows_of_each_table()
    {
        var assembly = Trees.TestAssembly;

        var text = Documents.GetText(await MetadataAsync(assembly).GetChildAsync("Tables").DecompileAsync(DecompilationLanguage.CSharp));

        Assert.StartsWith("// Tables", text);
        Assert.Contains("// 02 TypeDef: ", text);
    }

    private static string[] NamesOf(MetadataGrid grid, int[] rows)
    {
        return rows.Select(r => MetadataGrids.GetText(grid, r, grid.Column("Name"))).ToArray();
    }

    [Fact]
    public void The_patterns_typed_into_a_filter_are_kept_only_up_to_a_limit()
    {
        for (var pattern = 0; pattern < MetadataGrid.MaxPatterns * 3; pattern++)
        {
            Assert.NotNull(MetadataGrids.FindPattern($"/limit{pattern}/"));
        }

        Assert.InRange(MetadataGrid.CachedPatterns, 1, MetadataGrid.MaxPatterns);
        var limit0 = MetadataGrids.FindPattern("/^limit0$/");
        Assert.NotNull(limit0);
        Assert.Matches(limit0, "LIMIT0");
    }

    private static MetadataColumnFilter?[] Filters(MetadataGrid grid, string column, MetadataColumnFilter filter)
    {
        var filters = new MetadataColumnFilter?[grid.Columns.Count];
        filters[grid.Column(column)] = filter;
        return filters;
    }

    [Fact]
    public async Task A_text_filter_matches_part_of_a_cell_ignoring_case_or_a_regex()
    {
        var grid = await TypeDefsAsync();

        var part = NamesOf(grid, MetadataGrids.FilterRows(grid, null, Filters(grid, "Name", new MetadataColumnFilter.ByText { Text = "calcULATOR" })));
        var regex = NamesOf(grid, MetadataGrids.FilterRows(grid, null, Filters(grid, "Name", new MetadataColumnFilter.ByText { Text = "/^(Circle|Size)$/" })));

        Assert.Contains("Calculator`1", part);
        Assert.All(part, n => Assert.Contains("calculator", n, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(["Circle", "Size"], regex.Order());
    }

    [Fact]
    public async Task A_number_filter_compares_with_an_operator_and_otherwise_matches_the_hex_shown()
    {
        var grid = await TypeDefsAsync();
        var rid = grid.Column("RID");

        var above = MetadataGrids.FilterRows(grid, null, Filters(grid, "RID", new MetadataColumnFilter.ByText { Text = "> 0x3" }));
        var shown = MetadataGrids.FilterRows(grid, null, Filters(grid, "Token", new MetadataColumnFilter.ByText { Text = "02000002" }));

        Assert.All(above, r => Assert.True(int.Parse(MetadataGrids.GetText(grid, r, rid)) > 3));
        Assert.Equal(grid.RowCount - 3, above.Length);
        Assert.Equal([1], shown);
    }

    [Fact]
    public async Task A_flags_filter_picks_values_of_a_group_and_requires_or_excludes_flags()
    {
        var grid = await TypeDefsAsync();
        var schema = grid.Columns[grid.Column("Attributes")].Flags;
        Assert.NotNull(schema);
        var state = FilterState.From(schema);
        var filters = Filters(grid, "Attributes", new MetadataColumnFilter.ByFlags { Flags = state });

        state.SetMutexSelection("Visibility", [(uint)System.Reflection.TypeAttributes.NotPublic]);
        state.SetFlagState("Abstract", TriState.Excluded);
        var names = NamesOf(grid, MetadataGrids.FilterRows(grid, null, filters));

        Assert.Contains("Visibility", schema.MutexGroups.Select(g => g.Name));
        Assert.Contains("Circle", names);
        Assert.DoesNotContain("Calculator`1", names);
        Assert.DoesNotContain("<>c", names);
        Assert.Equal("Visibility = NotPublic ∧ ¬Abstract", FilterStatePresenter.Describe(state));
    }

    [Fact]
    public async Task Setting_the_independent_mode_counts_as_a_change_only_when_it_is_one()
    {
        var grid = await TypeDefsAsync();
        var schema = grid.Columns[grid.Column("Attributes")].Flags;
        Assert.NotNull(schema);
        var state = FilterState.From(schema);
        var before = state.Revision;

        state.UseIndependentMode(state.Mode);
        var unchanged = state.Revision;
        state.UseIndependentMode(MatchMode.Any);

        Assert.Equal(before, unchanged);
        Assert.Equal(before + 1, state.Revision);
        Assert.Equal(MatchMode.Any, state.Mode);
    }

    [Fact]
    public async Task Filters_keep_the_sorted_order()
    {
        var grid = await TypeDefsAsync();
        var order = MetadataGrids.OrderRows(grid, grid.Column("Name"), descending: true);

        var rows = MetadataGrids.FilterRows(grid, order, Filters(grid, "Namespace", new MetadataColumnFilter.ByText { Text = "Fixtures" }));

        Assert.Equal(order.Where(rows.Contains), rows);
    }

    [Fact]
    public void A_flags_filter_round_trips_through_its_saved_form()
    {
        var schema = FlagsSchemaInferer.For(typeof(System.Reflection.TypeAttributes));
        var state = FilterState.From(schema);
        state.SetMutexSelection("Visibility", [1u, 2u]);
        state.SetFlagState("Abstract", TriState.Required);
        state.UseIndependentMode(MatchMode.Any);

        var saved = FilterStatePresenter.SaveFilter(state);
        Assert.NotNull(saved);
        var restored = FilterState.From(schema);
        Assert.NotNull(saved.Groups);
        Assert.NotNull(saved.Flags);
        restored.Restore(saved with { Groups = new Dictionary<string, uint[]>(saved.Groups) { ["Gone"] = [1u] }, Flags = new Dictionary<string, TriState>(saved.Flags) { ["Missing"] = TriState.Required } });

        Assert.Equal(FilterStatePresenter.Describe(state), FilterStatePresenter.Describe(restored));
        Assert.Equal(MatchMode.Any, restored.Mode);
        Assert.Equal(["Abstract"], saved.Flags.Keys);
        Assert.Equal(["Visibility"], saved.Groups.Keys);
        Assert.Null(FilterStatePresenter.SaveFilter(FilterState.From(schema)));
    }

    [Fact]
    public async Task A_header_breaks_the_bits_of_its_flags_down_on_hovering_their_value()
    {
        var assembly = Trees.TestAssembly;
        var coff = await GridAsync(await MetadataAsync(assembly).GetChildAsync("COFF Header"));
        var characteristics = Enumerable.Range(0, coff.RowCount).Single(r => MetadataGrids.GetText(coff, r, coff.Column("Member")) == "Characteristics");

        var tooltip = MetadataGrids.FindTooltip(coff, characteristics, coff.Column("Value"));
        Assert.NotNull(tooltip);
        var bits = Assert.Single(tooltip.Groups).Flags;

        Assert.False(coff.HasDetails);
        Assert.Equal(16, bits.Count);
        Assert.Contains(new MetadataFlag { Name = "File is executable (0002)", IsSet = true }, bits);
        Assert.Null(MetadataGrids.FindTooltip(coff, 0, coff.Column("Value")));
        Assert.False((await GridAsync(await TableAsync(assembly, "TypeDef"))).HasDetails);
    }

    [Fact]
    public async Task Debug_information_shows_its_decoded_value_under_the_selected_row()
    {
        var assembly = Trees.TestAssembly;
        var pdb = await assembly.ReadRootAsync().GetChildAsync("Debug Metadata (From portable PDB)").GetChildAsync("Tables");
        var grid = await GridAsync(await pdb.TableAsync("CustomDebugInformation"));
        var kind = grid.Column("KindString");

        var options = Enumerable.Range(0, grid.RowCount).Single(r => MetadataGrids.GetText(grid, r, kind).StartsWith("Compilation Options"));
        var table = Assert.IsType<MetadataRowDetails.Table>(MetadataGrids.FindDetails(grid, options));

        Assert.True(grid.HasDetails);
        Assert.Equal(["Name", "Value"], table.Headers);
        Assert.Contains(table.Rows, r => r[0] == "language" && r[1] == "C#");
    }

    private static async Task<(int, int)[]> CellsAsync(BrowseNode table, string first, string second)
    {
        var grid = await GridAsync(table);
        return Enumerable.Range(0, grid.RowCount).Select(r => (Hex(grid, r, first), Hex(grid, r, second))).ToArray();
    }

    private static int Hex(MetadataGrid grid, int row, string column)
    {
        return Convert.ToInt32(MetadataGrids.GetText(grid, row, grid.Column(column)), 16);
    }

    private static int Token(EntityHandle handle)
    {
        return MetadataTokens.GetToken(handle);
    }

    [Fact]
    public async Task CoreLibs_relationship_tables_hold_the_rows_the_metadata_reader_decodes()
    {
        var corelib = new AssemblyList { Runtime = TestRuntime.Installed }.OpenModule(Trees.CoreLibPath);
        var md = LoadedAssemblies.GetFile(corelib).Metadata;
        var types = md.TypeDefinitions.Select(t => (Handle: t, Type: md.GetTypeDefinition(t))).ToArray();
        var fields = md.FieldDefinitions.Select(f => (Handle: f, Field: md.GetFieldDefinition(f))).ToArray();

        Assert.Equal(
            types.Where(t => !t.Type.GetDeclaringType().IsNil).Select(t => (Token(t.Handle), Token(t.Type.GetDeclaringType()))).Order(),
            (await CellsAsync(await TableAsync(corelib, "NestedClass"), "NestedClass", "EnclosingClass")).Order());

        Assert.Equal(
            types.SelectMany(t => t.Type.GetInterfaceImplementations().Select(i => (Token(t.Handle), Token(md.GetInterfaceImplementation(i).Interface)))).Order(),
            (await CellsAsync(await TableAsync(corelib, "InterfaceImpl"), "Class", "Interface")).Order());

        Assert.Equal(
            fields.Where(f => f.Field.GetOffset() >= 0).Select(f => (Token(f.Handle), f.Field.GetOffset())),
            await CellsAsync(await TableAsync(corelib, "FieldLayout"), "Field", "FieldOffset"));

        Assert.Equal(
            fields.Where(f => f.Field.GetRelativeVirtualAddress() != 0).Select(f => (Token(f.Handle), f.Field.GetRelativeVirtualAddress())),
            await CellsAsync(await TableAsync(corelib, "FieldRva"), "Field", "RVA"));

        Assert.Equal(
            types.Where(t => t.Type.GetEvents().Count > 0).Select(t => (Token(t.Handle), Token(t.Type.GetEvents().First()))),
            await CellsAsync(await TableAsync(corelib, "EventMap"), "Parent", "EventList"));

        Assert.Equal(
            types.Where(t => t.Type.GetProperties().Count > 0).Select(t => (Token(t.Handle), Token(t.Type.GetProperties().First()))),
            await CellsAsync(await TableAsync(corelib, "PropertyMap"), "Parent", "PropertyList"));

        var layouts = await GridAsync(await TableAsync(corelib, "ClassLayout"));
        Assert.NotEqual(0, layouts.RowCount);
        for (var row = 0; row < layouts.RowCount; row++)
        {
            var layout = md.GetTypeDefinition((TypeDefinitionHandle)MetadataTokens.EntityHandle(Hex(layouts, row, "Parent"))).GetLayout();
            Assert.Equal((int.Parse(MetadataGrids.GetText(layouts, row, layouts.Column("PackingSize"))), int.Parse(MetadataGrids.GetText(layouts, row, layouts.Column("ClassSize")))), (layout.PackingSize, layout.Size));
        }
    }

    [Fact]
    public async Task The_StateMachineMethod_table_pairs_each_MoveNext_with_its_kickoff_method()
    {
        var assembly = Trees.TestAssembly;
        using var stream = File.OpenRead(Path.ChangeExtension(Trees.TestAssemblyPath, ".pdb"));
        var md = MetadataReaderProvider.FromPortablePdbStream(stream).GetMetadataReader();
        var tables = await assembly.ReadRootAsync().GetChildAsync("Debug Metadata (From portable PDB)").GetChildAsync("Tables");

        var rows = await CellsAsync(await tables.TableAsync("StateMachineMethod"), "MoveNextMethod", "KickoffMethod");

        Assert.NotEmpty(rows);
        Assert.Equal(
            md.MethodDebugInformation.Select(h => (MoveNext: h.ToDefinitionHandle(), Kickoff: md.GetMethodDebugInformation(h).GetStateMachineKickoffMethod()))
                .Where(m => !m.Kickoff.IsNil).Select(m => (Token(m.MoveNext), Token(m.Kickoff))),
            rows);
    }

    [Fact]
    public async Task The_edit_and_continue_tables_list_each_logged_change_and_mapped_token()
    {
        using var temp = new TempDir();
        var assembly = new AssemblyList { Runtime = TestRuntime.Installed }.Open(WriteEditAndContinueAssembly(temp));
        var type = MetadataTokens.GetToken(MetadataTokens.TypeDefinitionHandle(1));

        var log = await GridAsync(await TableAsync(assembly, "EncLog"));
        var map = await GridAsync(await TableAsync(assembly, "EncMap"));

        Assert.Equal(["RID", "Token", "Offset", "Handle", "FuncCode"], log.Columns.Select(c => c.Name));
        Assert.Equal(2, log.RowCount);
        Assert.Equal(type, Hex(log, 0, "Handle"));
        Assert.Equal((int)EditAndContinueOperation.AddMethod, Hex(log, 1, "FuncCode"));
        Assert.Equal(nameof(EditAndContinueOperation.AddMethod), MetadataGrids.FindTooltip(log, 1, log.Column("FuncCode"))?.Text);
        Assert.Equal(1, map.RowCount);
        Assert.Equal(type, Hex(map, 0, "Handle"));
        Assert.Equal(new MetadataAddress
        {
            Table = TableIndex.TypeDef,
            Row = 0,
        }, MetadataGrids.FindLink(map, 0, map.Column("Handle")));
        Assert.Equal(new MetadataTarget
        {
            Node = await TableAsync(assembly, "TypeDef"),
            Row = 0,
        }, TestSession.Shared.FindMetadataRow(await TableAsync(assembly, "EncMap"), new MetadataAddress
        {
            Table = TableIndex.TypeDef,
            Row = 0,
        }));
    }

    /// <summary>An assembly whose metadata logs two changes to its one type, as an edit and continue
    /// delta does, and maps that type's row.</summary>
    private static string WriteEditAndContinueAssembly(TempDir temp)
    {
        var metadata = new MetadataBuilder();
        var name = metadata.GetOrAddString("EditAndContinue");
        metadata.AddModule(0, name, metadata.GetOrAddGuid(Guid.NewGuid()), default, default);
        metadata.AddAssembly(name, new Version(1, 0), default, default, 0, AssemblyHashAlgorithm.None);
        var firstField = MetadataTokens.FieldDefinitionHandle(1);
        var firstMethod = MetadataTokens.MethodDefinitionHandle(1);
        var type = metadata.AddTypeDefinition(default, default, metadata.GetOrAddString("<Module>"), default, firstField, firstMethod);
        metadata.AddEncLogEntry(type, EditAndContinueOperation.Default);
        metadata.AddEncLogEntry(type, EditAndContinueOperation.AddMethod);
        metadata.AddEncMapEntry(type);

        var pe = new ManagedPEBuilder(PEHeaderBuilder.CreateLibraryHeader(), new MetadataRootBuilder(metadata), new BlobBuilder());
        var image = new BlobBuilder();
        pe.Serialize(image);
        return temp.Write("EditAndContinue.dll", image.ToArray());
    }

    [Fact]
    public async Task CoreLibs_tables_and_heaps_read()
    {
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var corelib = list.Open(Trees.CoreLibPath);

        foreach (var node in (await MetadataAsync(corelib).GetChildAsync("Tables").ListChildrenAsync()).Concat((await MetadataAsync(corelib).ListChildrenAsync()).Where(c => c.Is(MetadataGridRole.Heap))))
        {
            var content = await node.ReadContentAsync();
            if (content is not NodeContent.Table gridContent)
            {
                continue;
            }

            var grid = gridContent.Grid;

            for (var row = 0; row < Math.Min(grid.RowCount, 50); row++)
            {
                for (var column = 0; column < grid.Columns.Count; column++)
                {
                    MetadataGrids.GetText(grid, row, column);
                    MetadataGrids.FindTooltip(grid, row, column);
                    MetadataGrids.FindLink(grid, row, column);
                }
            }
        }
    }
}
