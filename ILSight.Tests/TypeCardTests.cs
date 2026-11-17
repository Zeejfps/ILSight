using ILSight.Core;
using ILSight.Core.DiagramArea;

namespace ILSight.Tests;

public sealed class TypeCardTests
{
    private const string ZooNamespace = Trees.FixturesNamespace;

    private static readonly Lazy<(AssemblyList List, LoadedModule Assembly)> s_opened = new(Trees.OpenTestAssembly);

    private static AssemblyList List => s_opened.Value.List;

    private static async Task<CodeReference.Entity> ZooAsync(string type)
    {
        var root = await s_opened.Value.Assembly.ReadRootAsync();
        var node = await root.GetChildAsync(ZooNamespace, type);
        return node.Definition;
    }

    private static async Task<TypeCard> CardAsync(string type)
    {
        var zoo = await ZooAsync(type);
        var card = (await TypeCards.ReadAsync(List, zoo, CancellationToken.None)).GetValue().FoundOrNull;
        Assert.NotNull(card);
        return card;
    }

    private static List<string> NamesIn(TypeCard card, MemberSection kind)
    {
        return card.Sections
            .Single(s => s.Kind == kind).Members
            .Select(static m => m.Reference.Name)
            .ToList();
    }

    private static List<string> NamesOf(IEnumerable<TypeCard> cards)
    {
        return cards
            .Select(static c => c.Name)
            .ToList();
    }

    [Fact]
    public async Task A_card_names_its_type_as_the_tree_does_and_tags_what_it_is()
    {
        var animal = await CardAsync("Animal");

        Assert.Equal("Animal", animal.Name);
        Assert.Equal(ZooNamespace + ".Animal", animal.FullName);
        Assert.Equal(TypeShape.Class, animal.Shape);
        Assert.Equal(TypeStereotype.Abstract, animal.Stereotype);
        Assert.Equal(TypeStereotype.Interface, (await CardAsync("IFeeds")).Stereotype);
        Assert.Equal(TypeStereotype.Static, (await CardAsync("Visits")).Stereotype);
        Assert.Equal(TypeStereotype.None, (await CardAsync("Lion")).Stereotype);
        Assert.Equal(Trees.TestAssemblyPath, animal.AssemblyPath.Text);
    }

    [Fact]
    public async Task Its_own_members_are_listed_by_kind_in_the_tree_s_order_without_accessors_or_backing_fields()
    {
        var animal = await CardAsync("Animal");

        Assert.Equal([MemberSection.Fields, MemberSection.Properties, MemberSection.Methods], animal.Sections.Select(static s => s.Kind));
        Assert.Equal(["Tag", "_age"], NamesIn(animal, MemberSection.Fields));
        Assert.Equal(["Keeper", "Name"], NamesIn(animal, MemberSection.Properties));
        Assert.Contains("Speak", NamesIn(animal, MemberSection.Methods));
        Assert.DoesNotContain("get_Name", NamesIn(animal, MemberSection.Methods));
        Assert.Contains((await CardAsync("Keeper")).Sections, static s => s.Kind == MemberSection.Events);
    }

    [Fact]
    public async Task A_member_reads_as_the_tree_labels_it_with_its_icon_and_accessibility()
    {
        var animal = await CardAsync("Animal");

        var age = animal.Sections[0].Members[1];
        Assert.Equal("_age : int", age.Text);
        Assert.Equal(MemberShape.Field, age.Shape);
        Assert.Equal(NodeAccessibility.Private, age.Accessibility);
        Assert.False(age.IsVisibleOutside);
        Assert.True(animal.Sections[0].Members[0].IsVisibleOutside);
    }

    [Fact]
    public async Task What_a_type_inherits_is_grouped_by_the_base_type_it_comes_from_without_what_it_cannot_reach()
    {
        var lion = await CardAsync("Lion");

        var group = Assert.Single(lion.Inherited);
        Assert.Equal("Animal", group.From);
        var names = group.Members
            .Select(static m => m.Reference.Name)
            .ToList();
        Assert.Equal(["Describe", "Keeper", "Mark", "Name", "Speak", "Tag"], names);
    }

    [Fact]
    public async Task A_type_links_to_what_it_derives_from_implements_and_holds()
    {
        var lion = await CardAsync("Lion");
        var keeper = await CardAsync("Keeper");

        Assert.Contains(new TypeLink.Inherits { From = await ZooAsync("Lion"), To = await ZooAsync("Animal") }, lion.Links);
        Assert.Contains(new TypeLink.Implements { From = await ZooAsync("Lion"), To = await ZooAsync("IFeeds") }, lion.Links);
        Assert.Equal(
        [
            new TypeLink.HoldsMany { From = await ZooAsync("Keeper"), To = await ZooAsync("Animal"), Property = "Animals" },
            new TypeLink.HoldsOne { From = await ZooAsync("Keeper"), To = await ZooAsync("Enclosure"), Property = "Home" },
        ], keeper.Links);
    }

    [Fact]
    public async Task Built_in_types_and_the_bases_every_type_has_are_no_links()
    {
        Assert.Equal([new TypeLink.HoldsOne { From = await ZooAsync("Animal"), To = await ZooAsync("Keeper"), Property = "Keeper" }], (await CardAsync("Animal")).Links);
        Assert.Empty((await CardAsync("Enclosure")).Links);
    }

    [Fact]
    public async Task Classes_and_interfaces_can_have_derived_types_and_sealed_types_cannot()
    {
        Assert.True((await CardAsync("Animal")).CanHaveDerived);
        Assert.True((await CardAsync("IFeeds")).CanHaveDerived);
        Assert.False((await CardAsync("Parrot")).CanHaveDerived);
        Assert.False((await CardAsync("Enclosure")).CanHaveDerived);
    }

    [Fact]
    public async Task A_type_reads_with_the_types_of_its_assembly_it_is_related_to_either_way()
    {
        var animal = await ZooAsync("Animal");
        var read = Assert.IsType<TypeSystemWork<List<TypeCard>>.Done>((await TypeCards.ReadWithNeighboursAsync(List, animal, CancellationToken.None)).GetValue());
        var cards = read.Value;

        Assert.Equal("Animal", cards[0].Name);
        Assert.Equal(["Keeper", "Lion", "Parrot"], NamesOf(cards.Skip(1)).Order());
    }

    [Fact]
    public async Task The_types_deriving_from_a_class_or_implementing_an_interface_are_found_among_the_open_assemblies()
    {
        var animal = await ZooAsync("Animal");
        var derived = (await TypeCards.ReadDerivedAsync(List, animal, CancellationToken.None)).GetValue();
        var iFeeds = await ZooAsync("IFeeds");
        var implementing = (await TypeCards.ReadDerivedAsync(List, iFeeds, CancellationToken.None)).GetValue();

        Assert.Equal(["Lion", "Parrot"], NamesOf(derived.Found).Order());
        Assert.Equal(["Lion"], NamesOf(implementing.Found));
        Assert.Empty(derived.Unreadable);
    }

    [Fact]
    public async Task The_cards_deriving_from_a_class_across_three_modules_the_middle_one_unreadable_are_the_others_beside_it()
    {
        using var temp = new TempDir();
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var shapesPath = WrittenAssemblies.WriteWithBase(temp, "Shapes", "Shape", "Circle");
        var shapes = list.OpenModule(shapesPath);
        var billingPath = WrittenAssemblies.WriteDeriving(temp, "Billing", "Shapes", "Shape", "Invoice");
        var unreadableBillingPath = BrokenAssemblies.WriteUnreadableTypeSystem(temp, billingPath);
        list.OpenModule(unreadableBillingPath);
        var ordersPath = WrittenAssemblies.WriteDeriving(temp, "Orders", "Shapes", "Shape", "Order");
        list.OpenModule(ordersPath);
        var shapesRoot = await shapes.ReadRootAsync();
        var shapeNode = await shapesRoot.GetChildAsync("Shapes", "Shape");
        var shape = shapeNode.Definition;

        var derived = (await TypeCards.ReadDerivedAsync(list, shape, CancellationToken.None)).GetValue();

        Assert.Equal(["Circle", "Order"], NamesOf(derived.Found));
        Assert.Equal("UnreadableTypes.dll", Assert.Single(derived.Unreadable).Module.FilePath.FileName);
        Assert.NotEmpty(derived.Unreadable[0].Why.Message);
    }

    [Fact]
    public async Task Finding_derived_types_stops_when_cancelled()
    {
        var animal = await ZooAsync("Animal");
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => TypeCards.ReadDerivedAsync(List, animal, cancel.Token));
    }

    [Fact]
    public async Task A_reference_to_a_member_reads_as_no_card()
    {
        var root = await s_opened.Value.Assembly.ReadRootAsync();
        var nameNode = await root.GetChildAsync(ZooNamespace, "Animal", "Name : string");
        var name = nameNode.Definition;

        Assert.IsType<Lookup<TypeCard>.NotFound>((await TypeCards.ReadAsync(List, name, CancellationToken.None)).GetValue());
        Assert.Empty(Assert.IsType<TypeSystemWork<List<TypeCard>>.Done>((await TypeCards.ReadWithNeighboursAsync(List, name, CancellationToken.None)).GetValue()).Value);
    }

    [Fact]
    public async Task A_type_whose_member_the_library_cannot_read_is_its_cards_failure_saying_why()
    {
        using var temp = new TempDir();
        var grow = BrokenAssemblies.GetMethod(Trees.TestAssemblyPath, "Circle", "Grow");
        var unreadablePath = BrokenAssemblies.WriteUnreadableMethodName(temp, Trees.TestAssemblyPath, grow);
        var path = AssemblyPath.Of(unreadablePath);
        var circle = CodeReferences.CreateEntity(EntityKind.Type, "Circle", path, BrokenAssemblies.GetTypeHandle(Trees.TestAssemblyPath, "Circle"));
        var list = new AssemblyList { Runtime = TestRuntime.Installed };

        var card = await TypeCards.ReadAsync(list, circle, CancellationToken.None);
        var neighbours = await TypeCards.ReadWithNeighboursAsync(list, circle, CancellationToken.None);

        Assert.IsType<BadImageFormatException>(UnionAsserts.GetCase<Outcome<Lookup<TypeCard>>.Failed>(card).Error);
        Assert.IsType<BadImageFormatException>(UnionAsserts.GetCase<Outcome<TypeSystemWork<List<TypeCard>>>.Failed>(neighbours).Error);
    }
}
