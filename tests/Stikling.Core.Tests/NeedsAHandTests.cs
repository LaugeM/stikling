using System.Text.Json;
using Stikling.Core.Away;
using Stikling.Core.Care;
using Stikling.Core.Models;
using Stikling.Core.Plants;

namespace Stikling.Core.Tests;

/// <summary>Needs a hand: marks on a plant like high up or heavy, kept like tags.</summary>
public class NeedsAHandTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeProvider Time = new FixedTime(Now);

    [Fact]
    public void A_new_plant_has_none_and_an_older_record_without_the_field_reads_as_none()
    {
        Assert.Empty(new Plant().NeedsAHand);

        var read = JsonSerializer.Deserialize<Plant>("""{"nickname":"Hoya"}""", new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Empty(read!.NeedsAHand);
    }

    [Fact]
    public void Marks_are_stored_and_read_back_with_the_plant()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var json = JsonSerializer.Serialize(new Plant { Nickname = "Hoya", NeedsAHand = ["High up", "Heavy"] }, options);

        Assert.Equal(["High up", "Heavy"], JsonSerializer.Deserialize<Plant>(json, options)!.NeedsAHand);
    }

    [Fact]
    public void Both_copies_get_a_list_of_their_own()
    {
        var plant = new Plant { Nickname = "Hoya", NeedsAHand = ["High up"] };

        var copy = plant.Copy();
        copy.NeedsAHand.Add("Heavy");
        var like = plant.Duplicate();
        like.NeedsAHand.Add("Hanging");

        Assert.Equal(["High up"], plant.NeedsAHand);
        Assert.Equal(["High up", "Heavy"], copy.NeedsAHand);
        Assert.Equal(["High up", "Hanging"], like.NeedsAHand);
    }

    [Fact]
    public void Marks_are_tidied_without_blanks_or_repeats_like_tags()
    {
        Assert.Equal(["High up", "Heavy"], NeedsAHand.Normalize([" High   up ", "high up", "", null, "Heavy"]));
    }

    [Fact]
    public async Task Creating_and_updating_a_plant_tidies_the_marks()
    {
        var plants = new FakePlantRepository();
        var service = new PlantService(plants, new FakeTimelineRepository(), Time);
        var plant = new Plant { Nickname = "Hoya", NeedsAHand = ["High up", " high  up ", ""] };

        await service.CreateAsync(plant);
        Assert.Equal(["High up"], plants.Plants[plant.Id].NeedsAHand);

        var after = plant.Copy();
        after.NeedsAHand = ["Heavy", "heavy"];
        await service.UpdateAsync(plant, after, e => e.ToString());
        Assert.Equal(["Heavy"], plants.Plants[plant.Id].NeedsAHand);
    }

    [Fact]
    public async Task Changing_the_marks_writes_a_line_for_each_one_added_or_removed()
    {
        var plants = new FakePlantRepository();
        var timeline = new FakeTimelineRepository();
        var service = new PlantService(plants, timeline, Time);
        var plant = new Plant { Nickname = "Hoya", NeedsAHand = ["High up", "Heavy"] };
        await plants.SaveAsync(plant);

        var after = plant.Copy();
        after.NeedsAHand = ["heavy", "Hanging"];
        await service.UpdateAsync(plant, after, e => e.ToString());

        var entry = Assert.Single(timeline.Entries);
        Assert.Equal(["MarkedNeedsAHand", "NoLongerNeedsAHand"], entry.Lines!.Select(l => l.Key));
        Assert.Equal("Needs a hand: Hanging\nNo longer needs a hand: High up", entry.Text);
    }

    [Fact]
    public void The_line_is_lower_case_and_empty_means_none()
    {
        Assert.Equal("high up, heavy", NeedsAHand.Line(new Plant { NeedsAHand = ["High up", "Heavy"] }));
        Assert.Null(NeedsAHand.Line(new Plant()));
    }

    [Fact]
    public void Suggestions_are_the_marks_in_use_then_the_common_ones_not_used_yet()
    {
        var plants = new[]
        {
            new Plant { NeedsAHand = ["Behind the sofa", "heavy"] },
            new Plant { NeedsAHand = ["Behind the sofa"] },
            new Plant { NeedsAHand = ["Gone"], DeletedAt = Now }
        };

        var suggestions = NeedsAHand.Suggestions(plants, ["High up", "Hanging", "Heavy"]);

        Assert.Equal(["Behind the sofa", "heavy", "High up", "Hanging"], suggestions);
    }

    [Fact]
    public void The_sitter_sheet_says_to_ask_someone_to_help_for_a_marked_plant_only()
    {
        var marked = new Plant { Nickname = "Hoya", NeedsAHand = ["High up", "Heavy"] };
        var plain = new Plant { Nickname = "Fern" };
        var stretches = new List<AwayStretch> { new() { From = new DateOnly(2026, 10, 20), Until = new DateOnly(2026, 11, 3), Kind = AwayKind.Away } };

        var view = SitterSheetBuilder.Build(new SitterSheet { HideNotes = true }, stretches, [marked, plain], [], [], [], null, [],
            Stikling.Core.Rooms.Places.None, Hemisphere.Northern, Time);

        Assert.Equal("Ask someone to help: high up, heavy", view.Subjects.Single(s => s.Name == "Hoya").Help);
        Assert.Null(view.Subjects.Single(s => s.Name == "Fern").Help);
        Assert.Contains("Ask someone to help: high up, heavy", SitterSheetText.Write(view));
    }
}
