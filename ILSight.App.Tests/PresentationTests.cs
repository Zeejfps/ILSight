using ZGF.Gui.Immediate;
using ILSight.Core;
using ILSight.BuiltInArea;

namespace ILSight.App.Tests;

public sealed class PresentationTests
{
    private static readonly AppTheme Light = Theming.CreateCatalog(BuiltInThemes.Files).GetTheme(Theming.LightId);

    [Fact]
    public void Every_node_kind_has_its_own_icon_font_glyph_and_a_colored_role_in_both_palettes()
    {
        var kinds = Enum.GetValues<NodeIcon>();

        foreach (var kind in kinds)
        {
            var glyph = BuiltInGlyphs.Text(NodeIcons.Glyph(kind, BuiltInGlyphs.Icons));
            Assert.Equal(1, glyph.Length);
            Assert.InRange((int)glyph[0], 0xE000, 0xF8FF);
            Assert.NotEqual(0u, NodeIcons.GetColor(BuiltInThemes.Dark.Icons, kind));
            Assert.NotEqual(0u, NodeIcons.GetColor(Light.Icons, kind));
        }
        Assert.Equal(kinds.Length, kinds.Select(k => NodeIcons.Glyph(k, BuiltInGlyphs.Icons)).Distinct().Count());
    }

    [Fact]
    public async Task Every_node_of_a_tree_draws_the_icon_its_subject_names_and_the_tree_draws_many()
    {
        var root = await new AssemblyList { Runtime = TestRuntime.Installed }.Open(AppStateDriver.FixturePath).ReadRootAsync();
        var pending = new Queue<BrowseNode>([root]);
        var seen = new HashSet<NodeIcon>();

        while (pending.Count > 0)
        {
            var node = pending.Dequeue();
            seen.Add(NodeIcons.Of(node.Subject));
            if (node.Subject is NodeSubject.AssemblyReference)
            {
                continue;
            }

            foreach (var child in await node.ListChildrenAsync())
            {
                pending.Enqueue(child);
            }
        }

        Assert.True(seen.Count > 25, $"{seen.Count} icons");
    }

    [Fact]
    public async Task Two_copies_of_one_assembly_read_apart_by_folder_and_carry_their_files_in_their_tooltips()
    {
        using var temp = new TempDir();
        var copy = temp.Copy(AppStateDriver.FixturePath, Path.Combine("copy", Path.GetFileName(AppStateDriver.FixturePath)));
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var original = list.Open(AppStateDriver.FixturePath);
        var duplicate = list.Open(copy);
        var tree = new BrowseTree { Session = TestSession.Shared };
        tree.TrySyncRoots(list);

        var first = tree.Row(await original.ReadRootAsync());
        var second = tree.Row(await duplicate.ReadRootAsync());

        Assert.NotEqual(first.Label, second.Label);
        Assert.StartsWith(original.Name, first.Label);
        Assert.Equal($"{duplicate.Name} (copy)", second.Label);
        Assert.Equal(first.Secondary, second.Secondary);
        Assert.Equal(second.Label, tree.GetLabel(await duplicate.ReadRootAsync()));
        Assert.Contains(original.FilePath.Text, first.Tooltip);
        Assert.Contains(duplicate.FilePath.Text, second.Tooltip);
        Assert.Contains(original.FullName, first.Tooltip);
    }

    [Fact]
    public async Task An_assembly_listed_once_reads_as_its_name_alone()
    {
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var assembly = list.Open(AppStateDriver.FixturePath);
        var tree = new BrowseTree { Session = TestSession.Shared };
        tree.TrySyncRoots(list);

        Assert.Equal(assembly.Name, tree.Row(await assembly.ReadRootAsync()).Label);
    }

    [Fact]
    public async Task An_assembly_row_shows_its_version_and_framework_as_muted_secondary_text()
    {
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var assembly = list.Open(AppStateDriver.FixturePath);
        var tree = new BrowseTree { Session = TestSession.Shared };
        tree.TrySyncRoots(list);

        var row = tree.Row(await assembly.ReadRootAsync());

        Assert.Equal(assembly.Name, row.Label);
        Assert.Equal($"{assembly.Version} · net10.0", row.Secondary);
        Assert.Contains(assembly.FullName, row.Tooltip);
    }

    [Fact]
    public async Task The_folders_the_tree_makes_and_an_unreadable_modules_row_read_in_the_apps_language_their_english_as_before()
    {
        using var temp = new TempDir();
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var shapes = Assert.IsType<LoadedModule>(list.Open(WrittenAssemblies.WriteWithBase(temp, "Shapes", "Shape", "Circle")));
        list.Open(BrokenAssemblies.WriteUnreadableTypeSystem(temp, WrittenAssemblies.WriteDeriving(temp, "Billing", "Shapes", "Shape", "Invoice")));
        var shape = await shapes.ReadRootAsync().GetChildAsync("Shapes", "Shape");
        var derived = await shape.GetChildAsync("Derived Types");
        var unreadable = (await derived.ListChildrenAsync()).Single(static c => c.Subject is NodeSubject.UnreadableModule);
        var references = await shapes.ReadRootAsync().GetChildAsync("References");
        var why = Assert.IsType<NodeSubject.UnreadableModule>(unreadable.Subject).Value.Why.Message;
        var english = new BrowseTree { Session = TestSession.Shared };
        english.SetStyle(UiTheme.Light, Light.Icons, BuiltInGlyphs.Icons, Strings.For(Locale.En));
        var japanese = new BrowseTree { Session = TestSession.Shared };
        var ja = Strings.For(Locale.Ja);
        japanese.SetStyle(UiTheme.Light, Light.Icons, BuiltInGlyphs.Icons, ja);

        Assert.Equal("Derived Types", english.Row(derived).Label);
        Assert.Equal("References", english.Row(references).Label);
        Assert.Equal($"UnreadableTypes.dll: couldn't be read ({why})", english.Row(unreadable).Label);
        Assert.Equal(ja.TreeDerivedTypes, japanese.Row(derived).Label);
        Assert.Equal(ja.TreeReferences, japanese.Row(references).Label);
        Assert.Equal(ja.CauseModuleUnreadableRow(name: "UnreadableTypes.dll", why: why), japanese.Row(unreadable).Label);
        Assert.Equal(ja.TreeDerivedTypes, japanese.Row(derived).Tooltip);
        Assert.Equal(ja.TreeDerivedTypes, NodeText.FullName(ja, derived));
        Assert.NotEqual("Derived Types", ja.TreeDerivedTypes);
    }

    [Fact]
    public async Task Non_public_nodes_carry_an_accessibility_overlay_in_the_palette_color_and_dimmed_text()
    {
        var root = await new AssemblyList { Runtime = TestRuntime.Installed }.Open(AppStateDriver.FixturePath).ReadRootAsync();
        var ns = await root.GetChildAsync(AppStateDriver.FixturesNamespace);
        var privateField = (await ns.GetChildAsync("Outline").ListChildrenAsync()).Single(c => c.Icon == NodeIcon.Field && c.Accessibility == NodeAccessibility.Private);
        var tree = new BrowseTree { Session = TestSession.Shared };
        tree.SetStyle(UiTheme.Light, Light.Icons, BuiltInGlyphs.Icons, Strings.For(Locale.En));

        var field = tree.Row(privateField);
        var hidden = tree.Row(await ns.GetChildAsync("Hidden"));
        var greeter = tree.Row(await ns.GetChildAsync("Greeter"));

        Assert.Equal(BuiltInGlyphs.Icons.OverlayPrivate, field.Icon?.Overlay);
        Assert.Equal(Light.Icons.Restricted, field.Icon?.OverlayColor);
        Assert.Equal(IconTheming.ReferenceId.Text, field.Icon?.Glyph.Family);
        Assert.Equal(UiTheme.Light.TextMuted, field.LabelColor);
        Assert.Equal(BuiltInGlyphs.Icons.OverlayInternal, hidden.Icon?.Overlay);
        var greeterIcon = Assert.NotNull(greeter.Icon);
        Assert.Null(greeterIcon.Overlay);
        Assert.Null(greeter.Secondary);
    }

    [Fact]
    public async Task Types_and_members_show_their_metadata_tokens_in_the_chosen_format_while_assemblies_keep_their_identity()
    {
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var assembly = list.Open(AppStateDriver.FixturePath);
        var greeter = await assembly.ReadRootAsync().GetChildAsync(AppStateDriver.FixturesNamespace, "Greeter");
        var member = (await greeter.ListChildrenAsync()).First(c => c.Token is not null);
        var tree = new BrowseTree { Session = TestSession.Shared };
        tree.TrySyncRoots(list);
        var identity = tree.Row(await assembly.ReadRootAsync()).Secondary;

        Assert.Null(tree.Row(greeter).Secondary);
        tree.ShowTokens(TokenFormat.Hex);
        Assert.Equal($"@{greeter.Token:X8}", tree.Row(greeter).Secondary);
        Assert.StartsWith("@02", tree.Row(greeter).Secondary);
        Assert.Equal($"@{member.Token:X8}", tree.Row(member).Secondary);
        Assert.Equal(identity, tree.Row(await assembly.ReadRootAsync()).Secondary);
        tree.ShowTokens(TokenFormat.Decimal);
        Assert.Equal($"@{greeter.Token}", tree.Row(greeter).Secondary);
    }

    [Fact]
    public async Task A_forwarded_type_row_shows_the_forward_icon_and_the_assembly_it_is_forwarded_to()
    {
        var runtimeFolder = Path.GetDirectoryName(typeof(object).Assembly.Location) ?? throw new InvalidOperationException($"CoreLib's path names no folder: '{typeof(object).Assembly.Location}'.");
        var runtime = Path.Combine(runtimeFolder, "System.Runtime.dll");
        var facade = new AssemblyList { Runtime = TestRuntime.Installed }.Open(runtime);
        var forwarded = (await facade.ReadRootAsync().GetChildAsync("System").ListChildrenAsync()).First(c => c.Text == "Object");
        var tree = new BrowseTree { Session = TestSession.Shared };

        var row = tree.Row(forwarded);

        Assert.Equal("Object", row.Label);
        Assert.Equal(BuiltInGlyphs.Icons.NodeForwardedType, row.Icon?.Glyph);
        Assert.Equal("→ System.Private.CoreLib", row.Secondary);
        Assert.Equal("System.Object", row.Tooltip);
    }

    [Fact]
    public async Task A_type_used_from_a_reference_shows_the_type_reference_icon_and_its_namespace()
    {
        var assembly = new AssemblyList { Runtime = TestRuntime.Installed }.Open(AppStateDriver.FixturePath);
        var usedTypes = new List<BrowseNode>();
        foreach (var reference in await assembly.ReadRootAsync().GetChildAsync("References").ListChildrenAsync())
        {
            var children = await reference.ListChildrenAsync();
            foreach (var types in children.Where(c => c.Icon == NodeIcon.ReferencedTypes))
            {
                usedTypes.AddRange(await types.ListChildrenAsync());
            }
        }

        var used = usedTypes.First(c => c.Text == "List<>");
        var tree = new BrowseTree { Session = TestSession.Shared };

        var row = tree.Row(used);

        Assert.Equal(BuiltInGlyphs.Icons.NodeTypeReference, row.Icon?.Glyph);
        Assert.Equal("System.Collections.Generic", row.Secondary);
        Assert.Equal("System.Collections.Generic.List<>", row.Tooltip);
    }

    [Fact]
    public async Task A_non_public_type_says_its_accessibility_in_its_tooltip()
    {
        var root = await new AssemblyList { Runtime = TestRuntime.Installed }.Open(AppStateDriver.FixturePath).ReadRootAsync();
        var hidden = await root.GetChildAsync(AppStateDriver.FixturesNamespace, "Hidden");
        var tree = new BrowseTree { Session = TestSession.Shared };

        var row = tree.Row(hidden);

        Assert.False(hidden.IsPublicApi);
        Assert.Equal(hidden.FullName + "\ninternal", row.Tooltip);
    }

    [Fact]
    public void An_assembly_tab_is_titled_by_the_assembly_name_with_its_identity_in_the_tooltip()
    {
        var d = new AppStateDriver();
        var assembly = d.OpenFixtures();

        var tab = d.AppState.ListSession.Navigation.ActiveTab;
        Assert.NotNull(tab);

        Assert.Equal(assembly.Name, tab.Title);
        Assert.Contains(assembly.FullName, tab.TooltipIn(Strings.For(Locale.En)));
        Assert.Contains(assembly.FilePath.Text, tab.TooltipIn(Strings.For(Locale.En)));
    }
}
