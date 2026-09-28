using System.Text.Json;
using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Propagations;
using Stikling.Core.Today;

namespace Stikling.Core.Tests;

public class AttentionTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 9, 28);

    private readonly FakePlantRepository plants = new();
    private readonly FakePropagationRepository propagations = new();
    private readonly FakeTimelineRepository timeline = new();

    private static string Label(Enum value) => value.ToString();

    [Fact]
    public void A_reason_raises_a_flag_from_today_and_an_empty_one_clears_it()
    {
        var flag = Attention.Change(null, "  Repot soon ", Today);

        Assert.Equal(new Attention("Repot soon", Today), flag);
        Assert.Null(Attention.Change(flag, " ", Today));
    }

    [Fact]
    public void Rewording_a_flag_keeps_the_day_it_was_raised()
    {
        var flag = new Attention("Repot", Today.AddDays(-5));

        var changed = Attention.Change(flag, "Repot soon", Today);

        Assert.Equal(new Attention("Repot soon", Today.AddDays(-5)), changed);
        Assert.Equal(5, changed!.DaysSince(Today));
    }

    [Fact]
    public void A_flag_is_stored_and_read_back_with_the_plant()
    {
        var json = JsonSerializer.Serialize(
            new Plant { Nickname = "Monstera", Attention = new("Repot soon", Today) },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var read = JsonSerializer.Deserialize<Plant>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(new Attention("Repot soon", Today), read!.Attention);
    }

    [Fact]
    public async Task Flagging_a_plant_saves_it_without_writing_on_the_history()
    {
        var plant = new Plant { Nickname = "Monstera" };
        await plants.SaveAsync(plant);
        var service = new PlantService(plants, timeline, new FixedTime(Now));

        await service.SetAttentionAsync(plant, "Look closer");

        Assert.Equal("Look closer", plants.Plants[plant.Id].Attention?.Reason);
        Assert.Empty(timeline.Entries);

        await service.SetAttentionAsync(plant, null);
        Assert.Null(plants.Plants[plant.Id].Attention);
    }

    [Fact]
    public async Task A_plant_that_leaves_the_collection_is_no_longer_flagged()
    {
        var plant = new Plant { Nickname = "Pothos", Attention = new("Ready to divide", Today) };
        await plants.SaveAsync(plant);
        var service = new PlantService(plants, timeline, new FixedTime(Now));

        await service.SetStatusAsync([plant], PlantStatus.GivenAway, Label);

        Assert.Null(plant.Attention);
    }

    [Fact]
    public async Task A_finished_propagation_is_no_longer_flagged()
    {
        var propagation = new Propagation { Nickname = "Corms", StartedOn = Today, Attention = new("Ready to pot up", Today) };
        await propagations.SaveAsync(propagation);
        var service = new PropagationService(propagations, plants, timeline, new FixedTime(Now));

        await service.MarkFailedAsync(propagation, 1);

        Assert.Null(propagation.Attention);
    }

    [Fact]
    public void Today_lists_flagged_plants_and_propagations_the_longest_waiting_first()
    {
        var newer = new Plant { Nickname = "Monstera", Attention = new("Look closer", Today) };
        var older = new Propagation { Nickname = "Coleus", StartedOn = Today, Attention = new("Change the water", Today.AddDays(-3)) };
        var dormant = new Plant { Nickname = "Alocasia", DormantSince = Today, Attention = new("Repot soon", Today.AddDays(-1)) };
        var unflagged = new Plant { Nickname = "Pothos" };
        var gone = new Plant { Nickname = "Hoya", Status = PlantStatus.Died, Attention = new("Repot soon", Today) };
        var deleted = new Plant { Nickname = "Fern", DeletedAt = Now, Attention = new("Repot soon", Today) };

        var items = TodayBoard.NeedsAttention([newer, dormant, unflagged, gone, deleted], [older]);

        Assert.Equal(["Coleus", "Alocasia", "Monstera"], items.Select(i => i.Name));
        Assert.Same(older, items[0].Subject);
    }
}
