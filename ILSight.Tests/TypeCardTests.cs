using ILSight.Core;
using ILSight.Core.Diagrams;

namespace ILSight.Tests;

public sealed class TypeCardTests
{
    private const string ZooNamespace = Trees.FixturesNamespace;

    private static readonly Lazy<(AssemblyList List, LoadedModule Assembly)> s_opened = new(Trees.OpenTestAssembly);

    private static AssemblyList List => s_opened.Value.List;

    private static CodeReference.Entity Zoo(string type)
    {
        return s_opened.Value.Assembly.Root.Child(ZooNamespace, type).Definition;
    }

    private static TypeCard Card(string type)
    {
        var card = TestWaits.Wait(() => TypeCards.ReadAsync(List, Zoo(type), CancellationToken.None)).FoundOrNull;
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
    public void A_card_names_its_type_as_the_tree_does_and_tags_what_it_is()
    {
        var animal = Card("Animal");

        Assert.Equal("Animal", animal.Name);
        Assert.Equal(ZooNamespace + ".Animal", animal.FullName);
        Assert.Equal(TypeShape.Class, animal.Shape);
        Assert.Equal(TypeStereotype.Abstract, animal.Stereotype);
        Assert.Equal(TypeStereotype.Interface, Card("IFeeds").Stereotype);
        Assert.Equal(TypeStereotype.Static, Card("Visits").Stereotype);
        Assert.Equal(TypeStereotype.None, Card("Lion").Stereotype);
        Assert.Equal(Trees.TestAssemblyPath, animal.AssemblyPath.Text);
    }

    [Fact]
    public void Its_own_members_are_listed_by_kind_in_the_tree_s_order_without_accessors_or_backing_fields()
    {
        var animal = Card("Animal");

        Assert.Equal([MemberSection.Fields, MemberSection.Properties, MemberSection.Methods], animal.Sections.Select(static s => s.Kind));
        Assert.Equal(["Tag", "_age"], NamesIn(animal, MemberSection.Fields));
        Assert.Equal(["Keeper", "Name"], NamesIn(animal, MemberSection.Properties));
        Assert.Contains("Speak", NamesIn(animal, MemberSection.Methods));
        Assert.DoesNotContain("get_Name", NamesIn(animal, MemberSection.Methods));
        Assert.Contains(Card("Keeper").Sections, static s => s.Kind == MemberSection.Events);
    }

    [Fact]
    public void A_member_reads_as_the_tree_labels_it_with_its_icon_and_accessibility()
    {
        var animal = Card("Animal");

        var age = animal.Sections[0].Members[1];
        Assert.Equal("_age : int", age.Text);
        Assert.Equal(MemberShape.Field, age.Shape);
        Assert.Equal(NodeAccessibility.Private, age.Accessibility);
        Assert.False(age.IsVisibleOutside);
        Assert.True(animal.Sections[0].Members[0].IsVisibleOutside);
    }

    [Fact]
    public void What_a_type_inherits_is_grouped_by_the_base_type_it_comes_from_without_what_it_cannot_reach()
    {
        var lion = Card("Lion");

        var group = Assert.Single(lion.Inherited);
        Assert.Equal("Animal", group.From);
        var names = group.Members
            .Select(static m => m.Reference.Name)
            .ToList();
        Assert.Equal(["Describe", "Keeper", "Mark", "Name", "Speak", "Tag"], names);
    }

    [Fact]
    public void A_type_links_to_what_it_derives_from_implements_and_holds()
    {
        var lion = Card("Lion");
        var keeper = Card("Keeper");

        Assert.Contains(new TypeLink.Inherits { From = Zoo("Lion"), To = Zoo("Animal") }, lion.Links);
        Assert.Contains(new TypeLink.Implements { From = Zoo("Lion"), To = Zoo("IFeeds") }, lion.Links);
        Assert.Equal(
        [
            new TypeLink.HoldsMany { From = Zoo("Keeper"), To = Zoo("Animal"), Property = "Animals" },
            new TypeLink.HoldsOne { From = Zoo("Keeper"), To = Zoo("Enclosure"), Property = "Home" },
        ], keeper.Links);
    }

    [Fact]
    public void Built_in_types_and_the_bases_every_type_has_are_no_links()
    {
        Assert.Equal([new TypeLink.HoldsOne { From = Zoo("Animal"), To = Zoo("Keeper"), Property = "Keeper" }], Card("Animal").Links);
        Assert.Empty(Card("Enclosure").Links);
    }

    [Fact]
    public void Classes_and_interfaces_can_have_derived_types_and_sealed_types_cannot()
    {
        Assert.True(Card("Animal").CanHaveDerived);
        Assert.True(Card("IFeeds").CanHaveDerived);
        Assert.False(Card("Parrot").CanHaveDerived);
        Assert.False(Card("Enclosure").CanHaveDerived);
    }

    [Fact]
    public async Task A_type_reads_with_the_types_of_its_assembly_it_is_related_to_either_way()
    {
        var read = Assert.IsType<TypeSystemWork<List<TypeCard>>.Done>(await TypeCards.ReadWithNeighboursAsync(List, Zoo("Animal"), CancellationToken.None));
        var cards = read.Value;

        Assert.Equal("Animal", cards[0].Name);
        Assert.Equal(["Keeper", "Lion", "Parrot"], NamesOf(cards.Skip(1)).Order());
    }

    [Fact]
    public async Task The_types_deriving_from_a_class_or_implementing_an_interface_are_found_among_the_open_assemblies()
    {
        var derived = await TypeCards.ReadDerivedAsync(List, Zoo("Animal"), CancellationToken.None);
        var implementing = await TypeCards.ReadDerivedAsync(List, Zoo("IFeeds"), CancellationToken.None);

        Assert.Equal(["Lion", "Parrot"], NamesOf(derived.Found).Order());
        Assert.Equal(["Lion"], NamesOf(implementing.Found));
        Assert.Empty(derived.Unreadable);
    }

    [Fact]
    public async Task The_cards_deriving_from_a_class_across_three_modules_the_middle_one_unreadable_are_the_others_beside_it()
    {
        using var temp = new TempDir();
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var shapes = list.OpenModule(WrittenAssemblies.WriteWithBase(temp, "Shapes", "Shape", "Circle"));
        list.OpenModule(BrokenAssemblies.WriteUnreadableTypeSystem(temp, WrittenAssemblies.WriteDeriving(temp, "Billing", "Shapes", "Shape", "Invoice")));
        list.OpenModule(WrittenAssemblies.WriteDeriving(temp, "Orders", "Shapes", "Shape", "Order"));
        var shape = shapes.Root.Child("Shapes", "Shape").Definition;

        var derived = await TypeCards.ReadDerivedAsync(list, shape, CancellationToken.None);

        Assert.Equal(["Circle", "Order"], NamesOf(derived.Found));
        Assert.Equal("UnreadableTypes.dll", Assert.Single(derived.Unreadable).Module.FilePath.FileName);
        Assert.NotEmpty(derived.Unreadable[0].Why.Message);
    }

    [Fact]
    public async Task Finding_derived_types_stops_when_cancelled()
    {
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => TypeCards.ReadDerivedAsync(List, Zoo("Animal"), cancel.Token));
    }

    [Fact]
    public async Task A_reference_to_a_member_reads_as_no_card()
    {
        var name = s_opened.Value.Assembly.Root.Child(ZooNamespace, "Animal", "Name : string").Definition;

        Assert.IsType<Lookup<TypeCard>.NotFound>(await TypeCards.ReadAsync(List, name, CancellationToken.None));
        Assert.Empty(Assert.IsType<TypeSystemWork<List<TypeCard>>.Done>(await TypeCards.ReadWithNeighboursAsync(List, name, CancellationToken.None)).Value);
    }
}
