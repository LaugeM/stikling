using Stikling.Core.Models;
using Stikling.Core.Pots;

namespace Stikling.Core.Tests;

public class PotUseTests
{
    [Fact]
    public void A_pot_nobody_uses_is_all_free()
    {
        var use = Assert.Single(PotUse.List([Nursery(owned: 5)], []));

        Assert.Equal(0, use.InUse);
        Assert.Equal(5, use.Free);
    }

    [Fact]
    public void Plants_pointing_at_a_pot_use_it_up()
    {
        var pot = Nursery(owned: 3);

        var use = Assert.Single(PotUse.List([pot], [In(pot), In(pot)]));

        Assert.Equal(2, use.InUse);
        Assert.Equal(1, use.Free);
    }

    [Fact]
    public void An_outer_pot_counts_as_used_too()
    {
        var ceramic = new Pot { Name = "Grey ceramic", Group = PotGroup.Outer };
        var plant = new Plant { Nickname = "Peace lily", OuterPotId = ceramic.Id };

        var use = Assert.Single(PotUse.List([ceramic], [plant]));

        Assert.Equal(1, use.InUse);
    }

    [Fact]
    public void More_plants_than_pots_leaves_nothing_free_rather_than_minus_one()
    {
        var pot = Nursery(owned: 1);

        var use = Assert.Single(PotUse.List([pot], [In(pot), In(pot), In(pot)]));

        Assert.Equal(3, use.InUse);
        Assert.Equal(0, use.Free);
    }

    [Fact]
    public void A_deleted_plant_lets_go_of_its_pot()
    {
        var pot = Nursery(owned: 2);
        var gone = In(pot);
        gone.DeletedAt = DateTimeOffset.UtcNow;

        var use = Assert.Single(PotUse.List([pot], [In(pot), gone]));

        Assert.Equal(1, use.InUse);
    }

    [Fact]
    public void A_deleted_pot_is_off_the_list()
    {
        var pot = Nursery();
        pot.DeletedAt = DateTimeOffset.UtcNow;

        Assert.Empty(PotUse.List([pot], []));
    }

    [Fact]
    public void The_list_reads_nursery_pots_first_then_by_name_then_smallest_first()
    {
        var ceramic = new Pot { Name = "Grey ceramic", Group = PotGroup.Outer, TopCm = 14 };
        var big = Nursery(); big.TopCm = 13;
        var small = Nursery(); small.TopCm = 8;
        var other = Nursery(); other.Name = "Black nursery pot"; other.TopCm = 10;

        var order = PotUse.List([ceramic, big, small, other], []).Select(use => use.Pot.DisplayName);

        Assert.Equal(
            ["Black nursery pot, 10 cm", "Clear nursery pot, 8 cm", "Clear nursery pot, 13 cm", "Grey ceramic, 14 cm"],
            order);
    }

    [Fact]
    public void The_names_are_the_ones_already_in_use_without_repeats()
    {
        var big = Nursery(); big.TopCm = 13;
        var small = Nursery(); small.TopCm = 8;
        var ceramic = new Pot { Name = "Grey ceramic", Group = PotGroup.Outer };

        Assert.Equal(["Clear nursery pot", "Grey ceramic"], PotUse.Names([big, small, ceramic]));
    }

    private static Pot Nursery(int owned = 1) =>
        new() { Name = "Clear nursery pot", Group = PotGroup.Inner, Owned = owned };

    private static Plant In(Pot pot) => new() { Nickname = "Coleus", InnerPotId = pot.Id };
}
