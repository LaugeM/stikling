using Stikling.Core.Models;
using Stikling.Core.SoilMixes;

namespace Stikling.Core.Tests;

public class SoilMixBatchTests
{
    private static SoilMix Mix(MixUnit unit, params (string Name, decimal? Amount)[] rows) => new()
    {
        Name = "Chunky soil",
        Unit = unit,
        Ingredients = [.. rows.Select(r => new MixIngredient { Name = r.Name, Amount = r.Amount })]
    };

    [Fact]
    public void Parts_are_split_in_proportion_to_the_total()
    {
        var mix = Mix(MixUnit.Parts, ("Potting soil", 2), ("Bark", 1), ("Perlite", 1));

        var batch = mix.Batch(4)!;

        Assert.Equal(["Potting soil", "Bark", "Perlite"], batch.Select(s => s.Name));
        Assert.Equal([2m, 1m, 1m], batch.Select(s => s.Litres));
    }

    [Fact]
    public void Percent_that_does_not_reach_100_is_still_split_as_shares_of_what_is_there()
    {
        var mix = Mix(MixUnit.Percent, ("Potting soil", 50), ("Perlite", 30));

        var batch = mix.Batch(8)!;

        Assert.Equal([5m, 3m], batch.Select(s => s.Litres));
    }

    [Fact]
    public void A_missing_amount_gives_nothing()
    {
        var mix = Mix(MixUnit.Parts, ("Potting soil", 2), ("Bark", null));

        Assert.Null(mix.Batch(4));
        Assert.False(mix.CanMeasureOut);
    }

    [Fact]
    public void A_mix_that_counts_nothing_gives_nothing()
    {
        var mix = Mix(MixUnit.None, ("Potting soil", null), ("Bark", null));

        Assert.Null(mix.Batch(4));
        Assert.False(mix.CanMeasureOut);
    }

    [Fact]
    public void No_volume_or_amounts_of_zero_give_nothing()
    {
        Assert.Null(Mix(MixUnit.Parts, ("Bark", 1)).Batch(0));
        Assert.Null(Mix(MixUnit.Parts, ("Bark", 0), ("Perlite", 0)).Batch(4));
    }

    [Fact]
    public void A_blank_row_is_ignored()
    {
        var mix = Mix(MixUnit.Parts, ("Bark", 1), ("", null), ("Perlite", 1));

        Assert.Equal(2, mix.Batch(2)!.Count);
        Assert.True(mix.CanMeasureOut);
    }

    [Theory]
    [InlineData(2, "2 L")]
    [InlineData(1.25, "1.3 L")]
    [InlineData(0.3333, "333 ml")]
    [InlineData(0.05, "50 ml")]
    public void Volumes_are_rounded_to_what_can_be_measured(double litres, string expected) =>
        Assert.Equal(expected, SoilMix.Volume((decimal)litres));

    [Fact]
    public void Plants_in_a_mix_are_the_ones_in_the_collection_sorted_by_name()
    {
        var mixId = Guid.NewGuid();
        var plants = new[]
        {
            new Plant { Nickname = "Zed", SoilMixId = mixId },
            new Plant { Nickname = "amy", SoilMixId = mixId },
            new Plant { Nickname = "Dead one", SoilMixId = mixId, Status = PlantStatus.Died },
            new Plant { Nickname = "Sold one", SoilMixId = mixId, Status = PlantStatus.Sold },
            new Plant { Nickname = "Deleted", SoilMixId = mixId, DeletedAt = DateTimeOffset.UtcNow },
            new Plant { Nickname = "Elsewhere", SoilMixId = Guid.NewGuid() }
        };

        var found = MixUse.PlantsIn(mixId, plants);

        Assert.Equal(["amy", "Zed"], found.Select(p => p.DisplayName));
    }
}
