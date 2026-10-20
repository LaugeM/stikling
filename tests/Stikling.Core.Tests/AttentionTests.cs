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
        var service = new PropagationService(propagations, plants, timeline, new FakePropagationCareLogRepository(), new FakeCareLogRepository(), new FakePhotoRepository(), new FixedTime(Now));

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

        var items = TodayBoard.NeedsAttention([newer, dormant, unflagged, gone, deleted], [older], Today);

        Assert.Equal(["Coleus", "Alocasia", "Monstera"], items.Select(i => i.Name));
        Assert.Same(older, items[0].Subject);
    }

    private static AttentionLater Later(Attention flag, LooseDate from) => new(flag.Since, from);

    [Fact]
    public void A_flag_set_for_later_stays_off_Today_until_its_day_and_then_counts_from_it()
    {
        var flag = new Attention("Repot soon", Today);
        var march = new Plant { Nickname = "Monstera", Attention = flag, AttentionLater = Later(flag, LooseDate.Of(2027, 3)) };
        var soon = new Plant { Nickname = "Pothos", Attention = flag, AttentionLater = Later(flag, LooseDate.Of(Today.AddDays(3))) };

        Assert.Empty(TodayBoard.NeedsAttention([march, soon], [], Today));

        var item = Assert.Single(TodayBoard.NeedsAttention([march, soon], [], Today.AddDays(3)));
        Assert.Equal("Pothos", item.Name);
        Assert.Equal(Today.AddDays(3), item.StartsOn);
        Assert.Equal(LooseDate.Of(Today.AddDays(3)), item.From);

        Assert.Equal(["Pothos", "Monstera"], TodayBoard.NeedsAttention([march, soon], [], new DateOnly(2027, 3, 1)).Select(i => i.Name));
    }

    [Fact]
    public void A_flag_that_started_is_ordered_by_its_start_not_the_day_it_was_raised()
    {
        var early = new Attention("Look closer", Today.AddDays(-20));
        var recent = new Attention("Repot soon", Today.AddDays(-10));
        var delayed = new Plant { Nickname = "Aloe", Attention = early, AttentionLater = Later(early, LooseDate.Of(Today.AddDays(-1))) };
        var plain = new Plant { Nickname = "Fern", Attention = recent };

        Assert.Equal(["Fern", "Aloe"], TodayBoard.NeedsAttention([delayed, plain], [], Today).Select(i => i.Name));
    }

    [Fact]
    public void A_start_left_for_an_older_flag_never_hides_a_new_one()
    {
        var plant = new Plant
        {
            Nickname = "Monstera",
            Attention = new("Look closer", Today),
            AttentionLater = new(Today.AddDays(-30), LooseDate.Of(2027, 3))
        };

        var item = Assert.Single(TodayBoard.NeedsAttention([plant], [], Today));
        Assert.Null(item.From);
        Assert.Null(AttentionLater.StartsOn(plant.Attention, plant.AttentionLater));
    }

    [Fact]
    public async Task Flagging_for_later_saves_the_start_with_the_flag_and_flagging_now_drops_it()
    {
        var plant = new Plant { Nickname = "Monstera" };
        await plants.SaveAsync(plant);
        var service = new PlantService(plants, timeline, new FixedTime(Now));

        await service.SetAttentionAsync(plant, "Repot soon", LooseDate.Of(2027, 3));
        Assert.Equal(new AttentionLater(Today, LooseDate.Of(2027, 3)), plants.Plants[plant.Id].AttentionLater);

        await service.SetAttentionAsync(plant, "Repot soon");
        Assert.Null(plants.Plants[plant.Id].AttentionLater);

        await service.SetAttentionAsync(plant, "Repot soon", LooseDate.Of(2027, 3));
        await service.SetAttentionAsync(plant, null, LooseDate.Of(2027, 3));
        Assert.Null(plants.Plants[plant.Id].Attention);
        Assert.Null(plants.Plants[plant.Id].AttentionLater);
    }

    [Fact]
    public async Task Flagging_a_propagation_for_later_saves_the_start_too()
    {
        var propagation = new Propagation { Nickname = "Corms", StartedOn = Today };
        await propagations.SaveAsync(propagation);
        var service = new PropagationService(propagations, plants, timeline, new FakePropagationCareLogRepository(), new FakeCareLogRepository(), new FakePhotoRepository(), new FixedTime(Now));

        await service.SetAttentionAsync(propagation, "Ready to pot up", LooseDate.Of(Today.AddDays(5)));

        Assert.Equal(new AttentionLater(Today, LooseDate.Of(Today.AddDays(5))), propagations.Propagations[propagation.Id].AttentionLater);
    }

    [Fact]
    public async Task Leaving_the_collection_or_finishing_clears_the_start()
    {
        var flag = new Attention("Ready to divide", Today);
        var plant = new Plant { Nickname = "Pothos", Attention = flag, AttentionLater = Later(flag, LooseDate.Of(2027, 3)) };
        await plants.SaveAsync(plant);
        await new PlantService(plants, timeline, new FixedTime(Now)).SetStatusAsync([plant], PlantStatus.Sold, Label);
        Assert.Null(plant.AttentionLater);

        var propagation = new Propagation { Nickname = "Corms", StartedOn = Today, Attention = flag, AttentionLater = Later(flag, LooseDate.Of(2027, 3)) };
        await propagations.SaveAsync(propagation);
        var service = new PropagationService(propagations, plants, timeline, new FakePropagationCareLogRepository(), new FakeCareLogRepository(), new FakePhotoRepository(), new FixedTime(Now));
        await service.MarkFailedAsync(propagation, 1);
        Assert.Null(propagation.AttentionLater);
    }

    [Fact]
    public void A_start_is_stored_as_text_and_an_older_record_reads_without_one()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var flag = new Attention("Repot soon", Today);
        var json = JsonSerializer.Serialize(new Plant { Nickname = "Monstera", Attention = flag, AttentionLater = Later(flag, LooseDate.Of(2027, 3)) }, options);

        Assert.Contains("\"2027-03\"", json);
        Assert.Equal(LooseDate.Of(2027, 3), JsonSerializer.Deserialize<Plant>(json, options)!.AttentionLater!.From);
        Assert.Null(JsonSerializer.Deserialize<Plant>("""{"nickname":"Old"}""", options)!.AttentionLater);
    }
}
