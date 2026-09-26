using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Timeline;

namespace Stikling.Core.Tests;

public class PlantTagsTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 19, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 9, 19);

    private static string Label(Enum value) => value.ToString();

    [Theory]
    [InlineData("  For   swap ", "For swap")]
    [InlineData("Rare", "Rare")]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void Clean_trims_and_collapses_spaces(string? typed, string? expected)
    {
        Assert.Equal(expected, PlantTags.Clean(typed));
    }

    [Fact]
    public void Normalize_drops_blanks_and_repeats_and_keeps_the_first_spelling()
    {
        Assert.Equal(["For swap", "rare"], PlantTags.Normalize(["For swap", " ", "rare", "for  SWAP", "Rare"]));
    }

    [Fact]
    public void Adding_a_tag_the_plant_has_in_another_case_changes_nothing()
    {
        Assert.Equal(["Rare"], PlantTags.Add(["Rare"], "RARE"));
        Assert.Equal(["Rare", "Variegated"], PlantTags.Add(["Rare"], " Variegated "));
    }

    [Fact]
    public void Removing_a_tag_ignores_case()
    {
        Assert.Equal(["Rare"], PlantTags.Remove(["Rare", "For swap"], "for swap"));
    }

    [Fact]
    public void In_use_counts_each_tag_once_per_plant_and_skips_deleted_plants()
    {
        Plant[] plants =
        [
            new() { Nickname = "A", Tags = ["Rare", "rare", "For swap"] },
            new() { Nickname = "B", Tags = ["for swap"] },
            new() { Nickname = "C", Tags = ["Gone"], Status = PlantStatus.GivenAway },
            new() { Nickname = "D", Tags = ["Deleted"], DeletedAt = Now }
        ];

        var inUse = PlantTags.InUse(plants);

        Assert.Equal([new TagCount("For swap", 2), new TagCount("Gone", 1), new TagCount("Rare", 1)], inUse);
    }

    [Fact]
    public void Suggestions_put_your_own_tags_first_and_fill_in_the_common_ones()
    {
        Plant[] plants = [new() { Nickname = "A", Tags = ["Hoya cuttings", "rare"] }];

        Assert.Equal(["Hoya cuttings", "rare", "Variegated", "For swap"], PlantTags.Suggestions(plants));
    }

    [Fact]
    public void Copy_gives_the_tags_their_own_list()
    {
        var plant = new Plant { Nickname = "A", Tags = ["Rare"] };

        var copy = plant.Copy();
        copy.Tags.Add("For swap");

        Assert.Equal(["Rare"], plant.Tags);
    }

    [Fact]
    public void Days_in_quarantine_count_from_the_day_it_went_in()
    {
        var plant = new Plant { QuarantinedSince = Today.AddDays(-12) };

        Assert.Equal(12, plant.DaysInQuarantine(Today));
        Assert.Null(new Plant().DaysInQuarantine(Today));
    }

    [Fact]
    public void Quarantine_cannot_start_in_the_future()
    {
        var plant = new Plant { Nickname = "A", QuarantinedSince = Today.AddDays(1) };

        Assert.Contains("The quarantine can't start in the future.", plant.Validate(Today));
    }

    [Fact]
    public void Going_in_and_out_of_quarantine_is_described_but_tags_are_not()
    {
        var plant = new Plant { Nickname = "A" };

        var quarantined = plant.Copy();
        quarantined.QuarantinedSince = Today;
        quarantined.Tags = ["Rare"];
        Assert.Equal(["Put in quarantine"], PlantChanges.Describe(plant, quarantined, Label));

        var released = quarantined.Copy();
        released.QuarantinedSince = null;
        Assert.Equal(["Out of quarantine"], PlantChanges.Describe(quarantined, released, Label));

        var moved = quarantined.Copy();
        moved.QuarantinedSince = Today.AddDays(-3);
        Assert.Empty(PlantChanges.Describe(quarantined, moved, Label));
    }

    [Fact]
    public async Task A_new_plant_in_quarantine_says_so_on_its_first_entry()
    {
        var plants = new FakePlantRepository();
        var timeline = new FakeTimelineRepository();
        var service = new PlantService(plants, timeline, new FixedTime(Now));

        await service.CreateAsync(new Plant { Nickname = "New alocasia", QuarantinedSince = Today, Tags = [" rare ", "Rare"] });

        var entry = Assert.Single(timeline.Entries);
        Assert.Equal("Added to collection\nPut in quarantine", entry.Text);
        Assert.Equal(["rare"], Assert.Single(plants.Plants.Values).Tags);
    }

    [Fact]
    public async Task Ending_quarantine_is_recorded_on_the_history()
    {
        var plants = new FakePlantRepository();
        var timeline = new FakeTimelineRepository();
        var service = new PlantService(plants, timeline, new FixedTime(Now));
        var plant = new Plant { Nickname = "A", QuarantinedSince = Today.AddDays(-21) };

        await service.SetQuarantineAsync(plant, null, Label);
        await service.SetQuarantineAsync(plant, null, Label); // already out, nothing more to say

        Assert.False(plant.InQuarantine);
        Assert.Equal("Out of quarantine", Assert.Single(timeline.Entries).Text);
    }

    [Fact]
    public async Task Adding_tags_to_several_plants_skips_the_ones_that_have_them()
    {
        var plants = new FakePlantRepository();
        var timeline = new FakeTimelineRepository();
        var service = new PlantService(plants, timeline, new FixedTime(Now));
        var tagged = new Plant { Nickname = "A", Tags = ["for swap"] };
        var untagged = new Plant { Nickname = "B" };

        var changed = await service.AddTagsAsync([tagged, untagged], ["For swap"]);

        Assert.Equal(1, changed);
        Assert.Equal(["for swap"], tagged.Tags);
        Assert.Equal(["For swap"], untagged.Tags);
        Assert.Empty(timeline.Entries);
        Assert.Same(untagged, Assert.Single(plants.Plants.Values));
    }
}
