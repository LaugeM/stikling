using Stikling.Core.Backup;
using Stikling.Core.Models;
using Stikling.Core.Rooms;
using Stikling.Core.Plants;

namespace Stikling.Core.Tests;

public class LivesElsewhereTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    private static Plant Grandmas(string? livesWith = "Grandma") =>
        new() { Nickname = "Grandma's pothos", LivesElsewhere = true, LivesWith = livesWith };

    [Fact]
    public void A_plant_lives_with_you_unless_said_otherwise()
    {
        var plant = new Plant { Nickname = "Basil" };

        Assert.False(plant.LivesElsewhere);
        Assert.True(plant.InCollection);
    }

    [Fact]
    public void A_plant_that_lives_elsewhere_is_not_in_the_collection_but_stays_active()
    {
        var plant = Grandmas();

        Assert.False(plant.InCollection);
        Assert.Equal(PlantStatus.Active, plant.Status);
    }

    [Fact]
    public async Task LivesWith_is_trimmed_and_null_when_blank()
    {
        var plants = new FakePlantRepository();
        var service = new PlantService(plants, new FakeTimelineRepository(), new FixedTime(Now));
        var padded = Grandmas("  Anna from the swap ");
        var blank = Grandmas("   ");

        await service.CreateAsync(padded);
        await service.CreateAsync(blank);

        Assert.Equal("Anna from the swap", plants.Plants[padded.Id].LivesWith);
        Assert.Null(plants.Plants[blank.Id].LivesWith);
    }

    [Fact]
    public async Task LivesWith_is_dropped_when_the_plant_becomes_yours()
    {
        var plants = new FakePlantRepository();
        var service = new PlantService(plants, new FakeTimelineRepository(), new FixedTime(Now));
        var plant = Grandmas();
        await service.CreateAsync(plant);

        var before = plant.Copy();
        plant.LivesElsewhere = false;
        await service.UpdateAsync(before, plant, value => value.ToString());

        Assert.True(plants.Plants[plant.Id].InCollection);
        Assert.Null(plants.Plants[plant.Id].LivesWith);
    }

    [Fact]
    public void A_plant_that_lives_elsewhere_can_be_a_parent_after_your_own()
    {
        var mine = new Plant { Nickname = "Zebra plant" };
        var theirs = Grandmas();
        var child = new Plant { Nickname = "Cutting" };

        var options = PlantLineage.PossibleParents(child, [theirs, mine]);

        Assert.Equal([mine, theirs], options);
        Assert.Empty(new Plant { Nickname = "Cutting", ParentPlantId = theirs.Id }.Validate(DateOnly.FromDateTime(Now.DateTime)));
    }

    [Theory]
    [InlineData("Grandma", "Grandma's pothos · lives with Grandma")]
    [InlineData(null, "Grandma's pothos · not yours")]
    public void The_parent_picker_says_whose_it_is(string? livesWith, string expected) =>
        Assert.Equal(expected, PlantLineage.ParentLabel(Grandmas(livesWith)));

    [Fact]
    public void The_plant_list_leaves_them_out_of_every_filter()
    {
        Plant[] all = [new Plant { Nickname = "Basil" }, new Plant { Nickname = "Dead", Status = PlantStatus.Died }, Grandmas()];

        foreach (var status in Enum.GetValues<StatusFilter>())
        {
            var shown = new PlantFilter(Status: status).Apply(all).Select(p => p.DisplayName);
            Assert.DoesNotContain("Grandma's pothos", shown);
        }
    }

    [Fact]
    public void The_plants_csv_leaves_them_out()
    {
        var csv = CsvExport.Plants([new Plant { Nickname = "Basil", CreatedAt = Now }, Grandmas()], Places.None, []);

        Assert.Contains("Basil", csv);
        Assert.DoesNotContain("pothos", csv);
    }
}
