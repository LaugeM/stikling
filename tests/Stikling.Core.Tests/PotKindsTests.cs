using Stikling.Core.Models;
using Stikling.Core.Pots;

namespace Stikling.Core.Tests;

public class PotKindsTests
{
    [Theory]
    [InlineData(PotWatering.Wick)]
    [InlineData(PotWatering.Submerged)]
    public void A_self_watering_pot_suggests_leca(PotWatering watering) =>
        Assert.Equal(GrowingMedium.Leca, PotKinds.SuggestedMedium(new Pot { SelfWatering = watering }));

    [Fact]
    public void A_plain_pot_suggests_nothing() =>
        Assert.Null(PotKinds.SuggestedMedium(new Pot()));

    [Fact]
    public void No_pot_suggests_nothing() =>
        Assert.Null(PotKinds.SuggestedMedium(null));

    [Fact]
    public void Picking_a_self_watering_pot_sets_leca()
    {
        var pot = new Pot { SelfWatering = PotWatering.Wick };
        Assert.Equal(GrowingMedium.Leca, PotKinds.MediumAfterPicking(pot, GrowingMedium.Soil, chosenByPerson: false));
    }

    [Fact]
    public void A_medium_the_person_chose_is_kept()
    {
        var pot = new Pot { SelfWatering = PotWatering.Wick };
        Assert.Equal(GrowingMedium.Pon, PotKinds.MediumAfterPicking(pot, GrowingMedium.Pon, chosenByPerson: true));
    }

    [Fact]
    public void A_pot_without_a_suggestion_leaves_the_medium_alone() =>
        Assert.Equal(GrowingMedium.Perlite, PotKinds.MediumAfterPicking(new Pot(), GrowingMedium.Perlite, chosenByPerson: false));
}
