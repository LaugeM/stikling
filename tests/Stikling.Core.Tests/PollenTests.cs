using System.Text.Json;
using Stikling.Core.Crosses;
using Stikling.Core.Flowering;
using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Today;

namespace Stikling.Core.Tests;

public class PollenTests
{
    private static readonly DateOnly Today = new(2026, 11, 19);

    private static Plant Hoya(string name, string genus = "Hoya") => new() { Nickname = name, Genus = genus, Species = "carnosa" };

    private static PollenView Stored(Plant from, int daysAgo = 21, PollenStorage? kept = PollenStorage.Freezer) =>
        new(new Pollen { FromPlantId = from.Id, FromName = from.DisplayName, Genus = from.Genus, CollectedOn = Today.AddDays(-daysAgo), KeptIn = kept },
            ParentRef.Of(from.Id, from.DisplayName, new Dictionary<Guid, Plant> { [from.Id] = from }));

    private static FloweringMark Mark(Plant plant, FloweringStage stage, int daysAgo) =>
        new() { PlantId = plant.Id, Stage = stage, On = LooseDate.Of(Today.AddDays(-daysAgo)) };

    [Fact]
    public void Pollen_needs_a_plant_or_a_name_and_cannot_be_collected_in_the_future()
    {
        Assert.NotEmpty(new Pollen { CollectedOn = Today }.Validate(Today));
        Assert.Empty(new Pollen { FromName = "A friend's hoya", CollectedOn = Today }.Validate(Today));
        Assert.NotEmpty(new Pollen { FromName = "x", CollectedOn = Today.AddDays(1) }.Validate(Today));
        Assert.NotEmpty(new Pollen { FromName = "x", CollectedOn = Today, UsedUpOn = Today.AddDays(-1) }.Validate(Today));
        Assert.NotEmpty(new Pollen { FromName = "x", CollectedOn = Today, Amount = new string('a', 81) }.Validate(Today));
    }

    [Theory]
    [InlineData(0, "today")]
    [InlineData(1, "1 day ago")]
    [InlineData(10, "10 days ago")]
    [InlineData(21, "3 weeks ago")]
    [InlineData(75, "2 months ago")]
    [InlineData(400, "13 months ago")]
    [InlineData(800, "2 years ago")]
    public void The_age_says_how_long_ago_it_was_collected(int days, string expected)
    {
        Assert.Equal(expected, PollenService.Age(Today.AddDays(-days), Today));
    }

    [Fact]
    public void The_line_says_when_and_where_and_nothing_else()
    {
        var line = PollenService.Line(new Pollen { CollectedOn = Today.AddDays(-21), KeptIn = PollenStorage.Fridge }, Today);

        Assert.Equal("Collected 3 weeks ago · fridge", line);
        Assert.Equal("Collected 3 weeks ago", PollenService.Line(new Pollen { CollectedOn = Today.AddDays(-21) }, Today));
    }

    [Fact]
    public void Pollen_from_a_plant_keeps_its_genus_and_name_on_the_record()
    {
        var pollen = PollenService.StartFrom(Hoya("Dad"), Today);

        Assert.Equal("Dad", pollen.FromName);
        Assert.Equal("Hoya", pollen.Genus);
        Assert.Equal("carnosa", pollen.Species);
        Assert.Equal(Today, pollen.CollectedOn);
    }

    [Fact]
    public void A_storage_place_from_a_later_version_reads_as_not_said()
    {
        var pollen = JsonSerializer.Deserialize<Pollen>("{\"fromName\":\"x\",\"keptIn\":\"Cellar\"}", new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        Assert.Null(pollen.KeptIn);
    }

    [Fact]
    public void A_plant_coming_into_flower_gets_the_stored_pollen_of_its_genus_from_other_plants()
    {
        var mum = Hoya("Mum");
        var dad = Hoya("Dad");
        var other = Hoya("Monstera friend", genus: "Monstera");

        var matches = PollenMatches.For([mum, dad, other], [Mark(mum, FloweringStage.Buds, 3)], [Stored(dad), Stored(other)], PutOffs.Empty(), Today);

        var match = Assert.Single(matches);
        Assert.Equal(mum.Id, match.Plant.Id);
        Assert.Equal(FloweringStage.Buds, match.Stage);
        Assert.Equal([dad.Id], match.Pollen.Select(p => p.Pollen.FromPlantId!.Value));
    }

    [Fact]
    public void A_plant_in_flower_counts_and_one_that_is_done_does_not()
    {
        var mum = Hoya("Mum");
        var dad = Hoya("Dad");
        var inFlower = PollenMatches.For([mum, dad], [Mark(mum, FloweringStage.Buds, 10), Mark(mum, FloweringStage.InFlower, 2)], [Stored(dad)], PutOffs.Empty(), Today);
        var done = PollenMatches.For([mum, dad], [Mark(mum, FloweringStage.Buds, 10), Mark(mum, FloweringStage.Done, 1)], [Stored(dad)], PutOffs.Empty(), Today);

        Assert.Single(inFlower);
        Assert.Empty(done);
    }

    [Fact]
    public void Pollen_from_the_plant_itself_is_not_offered_to_it()
    {
        var mum = Hoya("Mum");

        Assert.Empty(PollenMatches.For([mum], [Mark(mum, FloweringStage.Buds, 3)], [Stored(mum)], PutOffs.Empty(), Today));
    }

    [Fact]
    public void Pollen_that_is_used_up_or_deleted_is_not_offered()
    {
        var mum = Hoya("Mum");
        var dad = Hoya("Dad");
        var used = Stored(dad);
        used.Pollen.UsedUpOn = Today;
        var gone = Stored(dad);
        gone.Pollen.DeletedAt = DateTimeOffset.UtcNow;

        Assert.Empty(PollenMatches.For([mum, dad], [Mark(mum, FloweringStage.Buds, 3)], [used, gone], PutOffs.Empty(), Today));
    }

    [Fact]
    public void Pollen_of_a_plant_that_is_gone_still_matches_by_the_genus_on_its_record()
    {
        var mum = Hoya("Mum");
        var pollen = new PollenView(
            new Pollen { FromName = "Friend's plant", Genus = "hoya", CollectedOn = Today.AddDays(-30) },
            ParentRef.Of(null, "Friend's plant", new Dictionary<Guid, Plant>()));

        var match = Assert.Single(PollenMatches.For([mum], [Mark(mum, FloweringStage.InFlower, 1)], [pollen], PutOffs.Empty(), Today));
        Assert.Single(match.Pollen);
    }

    [Fact]
    public void A_plant_with_no_genus_or_not_in_the_collection_gets_nothing()
    {
        var dad = Hoya("Dad");
        var nameless = new Plant { Nickname = "Mystery" };
        var away = Hoya("Away");
        away.Status = PlantStatus.GivenAway;
        var marks = new[] { Mark(nameless, FloweringStage.Buds, 1), Mark(away, FloweringStage.Buds, 1) };

        Assert.Empty(PollenMatches.For([nameless, away, dad], marks, [Stored(dad)], PutOffs.Empty(), Today));
    }

    [Fact]
    public void A_flowering_not_confirmed_for_a_long_time_is_not_offered_pollen()
    {
        var mum = Hoya("Mum");
        var dad = Hoya("Dad");

        Assert.Empty(PollenMatches.For([mum, dad], [Mark(mum, FloweringStage.InFlower, FloweringNow.StaleAfterDays + 5)], [Stored(dad)], PutOffs.Empty(), Today));
    }

    [Fact]
    public void Putting_it_off_for_this_flowering_hides_it_but_not_the_next_one()
    {
        var mum = Hoya("Mum");
        var dad = Hoya("Dad");
        var marks = new List<FloweringMark> { Mark(mum, FloweringStage.Buds, 3) };
        var first = Assert.Single(PollenMatches.For([mum, dad], marks, [Stored(dad)], PutOffs.Empty(), Today));

        var putOffs = PutOffs.Empty();
        putOffs.PutOff(first.PutOffKey, Today.AddYears(1));
        Assert.Empty(PollenMatches.For([mum, dad], marks, [Stored(dad)], putOffs, Today));

        // The flowering ends and a new one starts a year later: a new key, so it shows again
        var later = Today.AddDays(400);
        marks.Add(new FloweringMark { PlantId = mum.Id, Stage = FloweringStage.Done, On = LooseDate.Of(Today.AddDays(30)) });
        marks.Add(new FloweringMark { PlantId = mum.Id, Stage = FloweringStage.Buds, On = LooseDate.Of(later.AddDays(-2)) });
        Assert.Single(PollenMatches.For([mum, dad], marks, [Stored(dad, 400)], putOffs, later));
    }

    [Fact]
    public void Once_a_cross_is_recorded_in_this_flowering_the_card_goes_away()
    {
        var mum = Hoya("Mum");
        var dad = Hoya("Dad");
        var marks = new[] { Mark(mum, FloweringStage.Buds, 10) };
        var before = new Cross { PodParentPlantId = mum.Id, PollenParentPlantId = dad.Id, PollinatedOn = Today.AddDays(-30) };
        var during = new Cross { PodParentPlantId = mum.Id, PollenParentPlantId = dad.Id, PollinatedOn = Today.AddDays(-2) };
        var asPollen = new Cross { PodParentPlantId = dad.Id, PollenParentPlantId = mum.Id, PollinatedOn = Today.AddDays(-2) };

        Assert.Single(PollenMatches.For([mum, dad], marks, [Stored(dad)], PutOffs.Empty(), Today, [before, asPollen]));
        Assert.Empty(PollenMatches.For([mum, dad], marks, [Stored(dad)], PutOffs.Empty(), Today, [before, during]));
    }

    [Fact]
    public void The_newest_pollen_comes_first()
    {
        var mum = Hoya("Mum");
        var old = Hoya("Old");
        var fresh = Hoya("Fresh");

        var match = Assert.Single(PollenMatches.For([mum, old, fresh], [Mark(mum, FloweringStage.Buds, 1)], [Stored(old, 90), Stored(fresh, 5)], PutOffs.Empty(), Today));

        Assert.Equal("Fresh", match.Pollen[0].Title);
    }
}

public class TraitTests
{
    [Fact]
    public void Traits_are_suggested_from_plants_and_propagations_without_repeats()
    {
        var plants = new[] { new Plant { Traits = ["Dark new leaves", "silver splash"] }, new Plant { Traits = ["Silver  splash"] } };
        var propagations = new[] { new Propagation { Traits = ["dark new leaves", "Pink veins"] } };

        var suggestions = Traits.Suggestions(plants, propagations);

        Assert.Equal(["Dark new leaves", "Pink veins", "silver splash"], suggestions);
    }

    [Fact]
    public void Nothing_is_suggested_before_a_trait_is_used()
    {
        Assert.Empty(Traits.Suggestions([new Plant()], [new Propagation()]));
    }

    [Fact]
    public void Deleted_plants_and_propagations_do_not_suggest_traits()
    {
        var plant = new Plant { Traits = ["Gone"], DeletedAt = DateTimeOffset.UtcNow };

        Assert.Empty(Traits.Suggestions([plant], []));
    }

    [Fact]
    public void A_copy_gets_its_own_list_of_traits()
    {
        var plant = new Plant { Traits = ["a"] };
        var propagation = new Propagation { Traits = ["a"] };

        plant.Copy().Traits.Add("b");
        propagation.Copy().Traits.Add("b");

        Assert.Single(plant.Traits);
        Assert.Single(propagation.Traits);
    }

    [Fact]
    public void A_plant_from_before_traits_has_none()
    {
        var plant = JsonSerializer.Deserialize<Plant>("{\"nickname\":\"Old\"}", new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        Assert.Empty(plant.Traits);
    }
}
