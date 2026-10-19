using Stikling.Core.Models;
using Stikling.Core.Propagations;
using Stikling.Core.Pots;

namespace Stikling.Core.Tests;

public class ContainerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    private readonly FakePotRepository pots = new();
    private readonly FakePlantRepository plants = new();
    private readonly FakePhotoRepository photos = new();
    private readonly FakePropagationRepository propagations = new();

    private PotService Service => new(pots, plants, photos, propagations);

    private static Pot Box(string name = "Clear box", int owned = 1, bool deleted = false) =>
        new()
        {
            Group = PotGroup.Standalone,
            ForPropagation = true,
            Name = name,
            Owned = owned,
            DeletedAt = deleted ? Now : null
        };

    private static Propagation In(string? text, Pot? pot = null, PropagationStage stage = PropagationStage.Started, bool deleted = false, int daysAgo = 0) =>
        new()
        {
            Nickname = "Corm",
            Container = text,
            ContainerPotId = pot?.Id,
            Stage = stage,
            UpdatedAt = Now.AddDays(-daysAgo),
            DeletedAt = deleted ? Now : null
        };

    private Pot Add(Pot pot)
    {
        pots.Pots[pot.Id] = pot;
        return pot;
    }

    private Propagation Add(Propagation propagation)
    {
        propagations.Propagations[propagation.Id] = propagation;
        return propagation;
    }

    [Fact]
    public void The_name_based_id_is_stable_and_ignores_case()
    {
        Assert.Equal(ContainerName.IdFor("clear box"), ContainerName.IdFor("Clear Box"));
        Assert.NotEqual(ContainerName.IdFor("clear box"), ContainerName.IdFor("clear jar"));
        Assert.NotEqual(ContainerName.IdFor("clear box"), SellerIdFor("clear box"));
    }

    private static Guid SellerIdFor(string name) => Stikling.Core.Sellers.SellerName.IdFor(name);

    [Fact]
    public void For_finds_the_container_by_its_id()
    {
        var box = Box();

        Assert.Same(box, new ContainerList([box]).For(box.Id, "Something else"));
    }

    [Fact]
    public void For_gives_null_for_a_deleted_container_or_a_pot_that_is_not_one()
    {
        var gone = Box("Gone", deleted: true);
        var nursery = new Pot { Name = "Clear nursery pot" };
        var list = new ContainerList([gone, nursery]);

        Assert.Null(list.For(gone.Id, "Gone"));
        Assert.Null(list.For(nursery.Id, "Clear nursery pot"));
        Assert.Null(list.For(Guid.NewGuid(), null));
    }

    [Fact]
    public void For_finds_a_container_by_name_ignoring_case_and_spacing()
    {
        var box = Box("Clear box");

        Assert.Same(box, new ContainerList([box]).For(null, "  clear   BOX "));
        Assert.Null(new ContainerList([box]).For(null, "Jar"));
        Assert.Null(new ContainerList([box]).For(null, null));
    }

    [Fact]
    public void For_finds_a_renamed_container_by_the_id_its_old_name_makes()
    {
        var renamed = Box("Humidity box");
        renamed.Id = ContainerName.IdFor("Clear box");

        Assert.Same(renamed, new ContainerList([renamed]).For(null, "Clear box"));
    }

    [Fact]
    public void A_pot_that_is_not_a_container_is_not_found_by_name()
    {
        var nursery = new Pot { Name = "Clear box" };

        Assert.Null(new ContainerList([nursery]).For(null, "Clear box"));
    }

    [Fact]
    public void Pot_kind_is_worked_out_from_group_and_ForPropagation()
    {
        Assert.Equal(PotKind.Inner, new Pot { Group = PotGroup.Inner }.Kind);
        Assert.Equal(PotKind.Outer, new Pot { Group = PotGroup.Outer }.Kind);
        Assert.Equal(PotKind.Standalone, new Pot { Group = PotGroup.Standalone }.Kind);
        Assert.Equal(PotKind.Propagation, new Pot { Group = PotGroup.Standalone, ForPropagation = true }.Kind);
    }

    [Fact]
    public void A_container_without_a_name_says_so_in_its_own_words()
    {
        var error = Assert.Single(new Pot { ForPropagation = true }.Validate());

        Assert.Contains("container", error);
    }

    [Fact]
    public void Propagations_going_in_a_container_use_it_up_one_each()
    {
        var box = Box(owned: 5);
        var byId = In("Clear box", box);
        var byName = In("clear box");
        byId.InitialCount = 6;

        var use = Assert.Single(PotUse.List([box], [], [byId, byName]));

        Assert.Equal(2, use.InUse);
        Assert.Equal(3, use.Free);
        Assert.Equal(0, use.Gone);
    }

    [Fact]
    public void Finished_failed_and_deleted_propagations_do_not_count_but_rooted_ones_do()
    {
        var box = Box(owned: 5);

        var use = Assert.Single(PotUse.List([box],
            [],
            [In(null, box, PropagationStage.Done), In(null, box, PropagationStage.Failed), In(null, box, deleted: true), In(null, box, PropagationStage.Rooted)]));

        Assert.Equal(1, use.InUse);
    }

    [Fact]
    public void Plants_never_use_a_container_and_propagations_never_use_a_plant_pot()
    {
        var box = Box();
        var nursery = new Pot { Name = "Clear nursery pot", Owned = 2 };
        var plant = new Plant { Nickname = "Fern", InnerPotId = box.Id };

        var uses = PotUse.List([box, nursery], [plant], [In("Clear nursery pot", nursery)]);

        Assert.All(uses, use => Assert.Equal(0, use.InUse));
    }

    [Fact]
    public void Containers_come_last_in_the_list()
    {
        var uses = PotUse.List([Box(), new Pot { Group = PotGroup.Standalone, Name = "Terracotta" }, new Pot { Name = "Zed nursery" }], []);

        Assert.Equal([PotKind.Inner, PotKind.Standalone, PotKind.Propagation], uses.Select(u => u.Pot.Kind));
    }

    [Fact]
    public void Recent_containers_come_first_by_the_newest_propagation_in_them()
    {
        var old = Box("Old box");
        var fresh = Box("Fresh box");
        var unused = Box("Another box");
        var uses = PotUse.List([old, fresh, unused, new Pot { Name = "Nursery" }], []);

        var order = PotUse.RecentContainers(uses, [In(null, old, daysAgo: 10), In("fresh box", daysAgo: 1), In(null, old, daysAgo: 30)]);

        Assert.Equal(["Fresh box", "Old box", "Another box"], order.Select(u => u.Pot.Name));
    }

    [Fact]
    public async Task Choosing_nothing_gives_no_container()
    {
        Assert.Equal(new ContainerChoice(null, null), await Service.ChooseContainerAsync("   "));
        Assert.Equal(new ContainerChoice(null, null), await Service.ChooseContainerAsync(null, Guid.NewGuid()));
    }

    [Fact]
    public async Task Choosing_keeps_the_current_container_when_its_name_is_what_was_typed()
    {
        var first = Add(Box("Clear box"));
        var second = Add(Box("clear box"));
        first.CreatedAt = Now.AddDays(-5);
        second.CreatedAt = Now;

        var choice = await Service.ChooseContainerAsync("CLEAR box", second.Id);

        Assert.Equal(second.Id, choice.PotId);
    }

    [Fact]
    public async Task Choosing_finds_the_container_by_name_ignoring_case_and_spacing()
    {
        var box = Add(Box("Clear box"));

        var choice = await Service.ChooseContainerAsync("  clear   BOX ");

        Assert.Equal(new ContainerChoice(box.Id, "Clear box"), choice);
        Assert.Single(pots.Pots);
    }

    [Fact]
    public async Task Choosing_a_new_name_makes_a_container_with_the_id_the_name_makes()
    {
        var choice = await Service.ChooseContainerAsync("  Glass   jar ");

        Assert.Equal("Glass jar", choice.Name);
        Assert.Equal(ContainerName.IdFor("Glass jar"), choice.PotId);
        var made = Assert.Single(pots.Pots.Values);
        Assert.True(made.ForPropagation);
        Assert.Equal(PotGroup.Standalone, made.Group);
        Assert.Equal(1, made.Owned);
    }

    [Fact]
    public async Task Choosing_makes_a_random_id_when_the_name_id_belongs_to_a_deleted_pot()
    {
        var gone = Box("Glass jar", deleted: true);
        gone.Id = ContainerName.IdFor("Glass jar");
        Add(gone);

        var choice = await Service.ChooseContainerAsync("Glass jar");

        Assert.NotNull(choice.PotId);
        Assert.NotEqual(gone.Id, choice.PotId);
        Assert.Equal(2, pots.Pots.Count);
    }

    [Fact]
    public async Task Adopting_makes_one_container_per_name_owning_as_many_as_are_going()
    {
        Add(In("Clear box"));
        Add(In("clear  box"));
        Add(In("Clear box", stage: PropagationStage.Done));
        Add(In("Jar", stage: PropagationStage.Failed));

        var made = await Service.AdoptContainersAsync();

        Assert.Equal(2, made);
        var box = pots.Pots.Values.Single(p => ContainerName.Same(p.Name, "Clear box"));
        Assert.Equal(2, box.Owned);
        Assert.Equal(ContainerName.IdFor("Clear box"), box.Id);
        Assert.Equal(1, pots.Pots.Values.Single(p => p.Name == "Jar").Owned);
    }

    [Fact]
    public async Task Adopting_leaves_existing_names_and_deleted_ids_alone()
    {
        Add(Box("Clear box"));
        var gone = Box("Glass jar", deleted: true);
        gone.Id = ContainerName.IdFor("Glass jar");
        Add(gone);
        Add(In("clear box"));
        Add(In("Glass jar"));

        Assert.Equal(0, await Service.AdoptContainersAsync());
        Assert.Equal(2, pots.Pots.Count);
    }

    [Fact]
    public async Task Adopting_saves_no_propagation_and_makes_nothing_the_second_time()
    {
        var one = Add(In("Clear box"));
        Add(In("Linked", Box("Linked")));
        Add(In("Deleted one", deleted: true));
        var savesBefore = propagations.Saves;

        Assert.Equal(1, await Service.AdoptContainersAsync());
        Assert.Equal(0, await Service.AdoptContainersAsync());

        Assert.Equal(savesBefore, propagations.Saves);
        Assert.Null(one.ContainerPotId);
    }

    [Fact]
    public async Task In_use_counts_plants_and_propagations_separately()
    {
        var box = Add(Box());
        Add(In("Clear box", box));
        Add(In("Clear box", box));

        var links = await Service.InUseAsync(box.Id);

        Assert.Equal(new PotLinks(0, 2), links);
    }

    [Fact]
    public void Like_last_time_copies_the_container_id()
    {
        var from = new Propagation { Container = "Clear box", ContainerPotId = Guid.NewGuid() };
        var to = new Propagation();

        LastTime.Apply(from, to);

        Assert.Equal(from.ContainerPotId, to.ContainerPotId);
        Assert.Equal("Clear box", to.Container);
    }
}
