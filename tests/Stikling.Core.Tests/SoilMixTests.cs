using Stikling.Core.Models;
using Stikling.Core.SoilMixes;

namespace Stikling.Core.Tests;

public class SoilMixTests
{
    // What counts as a mix

    [Fact]
    public void A_mix_with_no_amounts_at_all_is_valid()
    {
        var mix = Chunky();

        Assert.Empty(mix.Validate());
        Assert.Equal(MixUnit.None, mix.Unit);
        Assert.Equal("Potting soil, Bark, LECA, Perlite", mix.Recipe);
    }

    [Fact]
    public void Validate_wants_a_name() =>
        Assert.Contains("Give the mix a name.", new SoilMix { Ingredients = [Some("Perlite")] }.Validate());

    [Fact]
    public void Validate_wants_at_least_one_ingredient() =>
        Assert.Contains("Add at least one ingredient.", new SoilMix { Name = "Chunky soil" }.Validate());

    [Fact]
    public void A_row_with_no_name_does_not_count_as_an_ingredient()
    {
        var mix = new SoilMix { Name = "Chunky soil", Ingredients = [Some("  ")] };

        Assert.Contains("Add at least one ingredient.", mix.Validate());
    }

    [Fact]
    public void Validate_rejects_an_amount_below_zero()
    {
        var mix = Chunky();
        mix.Unit = MixUnit.Parts;
        mix.Ingredients[0].Amount = -1;

        Assert.Contains("An amount can't be less than 0.", mix.Validate());
    }

    // Tidying up on the way in

    [Fact]
    public void Blank_rows_are_dropped_when_the_mix_is_saved()
    {
        var mix = Chunky();
        mix.Ingredients.Add(Some(""));

        mix.Tidy();

        Assert.Equal(4, mix.Ingredients.Count);
    }

    [Fact]
    public void Amounts_are_cleared_when_the_mix_counts_nothing()
    {
        var mix = Chunky();
        mix.Unit = MixUnit.Parts;
        mix.Ingredients[0].Amount = 3;
        mix.Unit = MixUnit.None;

        mix.Tidy();

        Assert.All(mix.Ingredients, i => Assert.Null(i.Amount));
    }

    // Amounts

    [Fact]
    public void Parts_read_as_a_multiplier()
    {
        var mix = Chunky();
        mix.Unit = MixUnit.Parts;
        mix.Ingredients[0].Amount = 2;

        Assert.StartsWith("2 × Potting soil", mix.Recipe);
    }

    [Fact]
    public void Percentages_are_added_up()
    {
        var mix = Percent(50, 30, 20);

        Assert.Equal(100, mix.Total);
        Assert.True(mix.PercentAddsUp);
    }

    [Fact]
    public void A_percent_total_that_is_not_100_is_still_a_valid_mix()
    {
        var mix = Percent(50, 30);

        Assert.False(mix.PercentAddsUp);
        Assert.Empty(mix.Validate());
    }

    [Fact]
    public void Thirds_add_up_to_exactly_100()
    {
        var mix = Percent(33.3m, 33.3m, 33.4m);

        Assert.True(mix.PercentAddsUp);
    }

    [Fact]
    public void A_mix_that_counts_nothing_is_never_short_of_100() =>
        Assert.True(Chunky().PercentAddsUp);

    // The order is the content

    [Fact]
    public void Moving_an_ingredient_up_leaves_the_others_in_order()
    {
        var mix = Chunky();

        mix.MoveUp(2);

        Assert.Equal(["Potting soil", "LECA", "Bark", "Perlite"], mix.Ingredients.Select(i => i.Name));
    }

    [Fact]
    public void Moving_one_down_swaps_it_with_the_next()
    {
        var mix = Chunky();

        mix.MoveDown(0);

        Assert.Equal(["Bark", "Potting soil", "LECA", "Perlite"], mix.Ingredients.Select(i => i.Name));
    }

    [Fact]
    public void The_first_cannot_move_up_and_the_last_cannot_move_down()
    {
        var mix = Chunky();

        mix.MoveUp(0);
        mix.MoveDown(3);

        Assert.Equal(["Potting soil", "Bark", "LECA", "Perlite"], mix.Ingredients.Select(i => i.Name));
    }

    // Copies

    [Fact]
    public void A_copy_does_not_share_the_ingredient_list()
    {
        var mix = Chunky();

        var copy = mix.Copy();
        copy.Ingredients[0].Name = "Coco coir";
        copy.Ingredients.Add(Some("Sand"));

        Assert.Equal("Potting soil", mix.Ingredients[0].Name);
        Assert.Equal(4, mix.Ingredients.Count);
    }

    [Fact]
    public void Saving_as_a_new_mix_leaves_the_original_alone()
    {
        var mix = Chunky();
        mix.RetiredOn = new DateOnly(2026, 1, 1);

        var copy = mix.CopyAsNew("Chunky soil (Sep 2026)");

        Assert.NotEqual(mix.Id, copy.Id);
        Assert.Equal("Chunky soil (Sep 2026)", copy.Name);
        Assert.Equal("Chunky soil", mix.Name);
        Assert.Null(copy.RetiredOn);
        Assert.Equal(mix.Ingredients.Count, copy.Ingredients.Count);
    }

    // Retiring

    [Fact]
    public void A_retired_mix_says_so()
    {
        var mix = Chunky();
        Assert.False(mix.IsRetired);

        mix.RetiredOn = new DateOnly(2026, 9, 20);

        Assert.True(mix.IsRetired);
    }

    [Fact]
    public void Retired_mixes_come_last_but_stay_on_the_list()
    {
        var retired = Chunky();
        retired.Name = "Old chunky soil";
        retired.RetiredOn = new DateOnly(2026, 1, 1);

        var order = MixUse.List([retired, Chunky()], []).Select(use => use.Mix.Name);

        Assert.Equal(["Chunky soil", "Old chunky soil"], order);
    }

    [Fact]
    public void Plants_in_a_mix_are_counted()
    {
        var mix = Chunky();
        var gone = new Plant { Nickname = "Gone", SoilMixId = mix.Id, DeletedAt = DateTimeOffset.UtcNow };
        var plants = new[] { new Plant { Nickname = "Basil", SoilMixId = mix.Id }, new Plant { Nickname = "Fig" }, gone };

        Assert.Equal(1, Assert.Single(MixUse.List([mix], plants)).Plants);
    }

    // Suggestions

    [Fact]
    public void Suggestions_are_the_built_ins_plus_what_other_mixes_use()
    {
        var mine = Chunky();
        mine.Ingredients.Add(Some("Rice hulls"));

        var suggestions = SoilMixIngredients.Suggestions([mine]);

        Assert.Equal(SoilMixIngredients.Common.Count + 1, suggestions.Count);
        Assert.Contains("Rice hulls", suggestions);
    }

    [Fact]
    public void Suggestions_do_not_repeat_a_built_in_in_another_case()
    {
        var mine = new SoilMix { Name = "Seedling mix", Ingredients = [Some("perlite")] };

        Assert.Equal(SoilMixIngredients.Common.Count, SoilMixIngredients.Suggestions([mine]).Count);
    }

    private static MixIngredient Some(string name) => new() { Name = name };

    private static SoilMix Chunky() => new()
    {
        Name = "Chunky soil",
        Ingredients = [Some("Potting soil"), Some("Bark"), Some("LECA"), Some("Perlite")]
    };

    private static SoilMix Percent(params decimal[] amounts)
    {
        var mix = Chunky();
        mix.Unit = MixUnit.Percent;
        mix.Ingredients = [.. amounts.Select((amount, i) => new MixIngredient { Name = $"Thing {i}", Amount = amount })];
        return mix;
    }
}
