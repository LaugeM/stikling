using Stikling.Core.Models;

namespace Stikling.Core.Tests;

public class PotTests
{
    [Fact]
    public void Validate_wants_a_name() =>
        Assert.Contains("Give the pot a name, like \"Clear nursery pot\".", new Pot().Validate());

    [Fact]
    public void Validate_wants_at_least_one_of_it() =>
        Assert.Contains("You have to have at least one of it.", Nursery(owned: 0).Validate());

    [Fact]
    public void Validate_rejects_a_measurement_of_nothing()
    {
        var pot = Nursery();
        pot.HeightCm = 0;

        Assert.Contains("A measurement has to be more than 0.", pot.Validate());
    }

    [Fact]
    public void Validate_rejects_a_bottom_wider_than_the_top()
    {
        var pot = Nursery();
        pot.TopCm = 10;
        pot.BottomCm = 12;

        Assert.Contains("The bottom can't be wider than the top.", pot.Validate());
    }

    [Fact]
    public void A_pot_with_only_a_name_is_enough() => Assert.Empty(Nursery().Validate());

    [Fact]
    public void DisplayName_puts_the_size_after_the_name()
    {
        var pot = Nursery();
        pot.TopCm = 13;

        Assert.Equal("Clear nursery pot, 13 cm", pot.DisplayName);
    }

    [Fact]
    public void DisplayName_is_just_the_name_when_there_is_no_size() =>
        Assert.Equal("Clear nursery pot", Nursery().DisplayName);

    [Fact]
    public void A_size_keeps_its_decimal_but_loses_the_zeros()
    {
        Assert.Equal("13", Pot.Size(13m));
        Assert.Equal("5.8", Pot.Size(5.8m));
        Assert.Equal("4.2", Pot.Size(4.20m));
    }

    [Fact]
    public void A_pot_without_a_rim_cannot_hang()
    {
        Assert.False(Nursery().HasRim);

        var rimmed = Nursery();
        rimmed.RimMm = 7;

        Assert.True(rimmed.HasRim);
    }

    [Fact]
    public void A_pot_that_is_not_self_watering_has_no_wick_or_submerged() =>
        Assert.Null(Nursery().SelfWatering);

    private static Pot Nursery(int owned = 1) =>
        new() { Name = "Clear nursery pot", Group = PotGroup.Inner, Owned = owned };
}
