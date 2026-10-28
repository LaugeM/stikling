using Stikling.Core.Care;
using Stikling.Core.Models;
using Stikling.Core.Today;

namespace Stikling.Core.Tests;

public class OuterPotReminderTests
{
    private static readonly DateTimeOffset Watered = new(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);

    private static TimeProvider At(int minutesAfter) => new FixedTime(Watered.AddMinutes(minutesAfter));

    private static Plant InOuterPot(string name = "Monstera") =>
        new() { Nickname = name, OuterPotId = Guid.NewGuid() };

    private static CareLog Water(Plant plant, CareKind kind = CareKind.Watered, DateTimeOffset? at = null, DateOnly? on = null)
    {
        var when = at ?? Watered;
        return new CareLog
        {
            PlantId = plant.Id,
            Kind = kind,
            CreatedAt = when,
            OccurredOn = on ?? DateOnly.FromDateTime(when.UtcDateTime)
        };
    }

    private static IReadOnlyList<OuterPotDue> Due(Plant plant, CareLog[] logs, int minutesAfter, UserSettings? settings = null) =>
        OuterPotReminders.Find([plant], logs, settings ?? new UserSettings(), At(minutesAfter)).Due;

    [Fact]
    public void Not_due_before_the_wait_and_due_after()
    {
        var plant = InOuterPot();
        var log = Water(plant);

        Assert.Empty(Due(plant, [log], 59));
        var due = Assert.Single(Due(plant, [log], 60));
        Assert.Equal(log.Id, due.CareLogId);
    }

    [Fact]
    public void Says_when_the_next_one_is_due()
    {
        var plant = InOuterPot();
        var result = OuterPotReminders.Find([plant], [Water(plant)], new UserSettings(), At(10));

        Assert.Empty(result.Due);
        Assert.Equal(Watered.AddMinutes(60), result.NextDueAt);
    }

    [Fact]
    public void Gone_once_ticked()
    {
        var plant = InOuterPot();
        var log = Water(plant);
        log.OuterPotEmptiedAt = Watered.AddMinutes(70);

        Assert.Empty(Due(plant, [log], 120));
    }

    [Fact]
    public void A_newer_watering_replaces_the_older_one()
    {
        var plant = InOuterPot();
        var older = Water(plant, at: Watered);
        var newer = Water(plant, at: Watered.AddMinutes(30));

        Assert.Empty(Due(plant, [older, newer], 70));
        Assert.Equal(newer.Id, Assert.Single(Due(plant, [older, newer], 90)).CareLogId);
    }

    [Fact]
    public void A_newer_watering_that_was_ticked_leaves_nothing_from_the_older()
    {
        var plant = InOuterPot();
        var older = Water(plant, at: Watered);
        var newer = Water(plant, at: Watered.AddMinutes(30));
        newer.OuterPotEmptiedAt = Watered.AddMinutes(100);

        Assert.Empty(Due(plant, [older, newer], 200));
    }

    [Fact]
    public void Plants_without_an_outer_pot_or_with_water_kept_in_it_are_left_out()
    {
        var none = new Plant { Nickname = "None" };
        var kept = InOuterPot("Kept");
        kept.WaterInOuterPot = true;

        Assert.Empty(Due(none, [Water(none)], 120));
        Assert.Empty(Due(kept, [Water(kept)], 120));
    }

    [Fact]
    public void Topping_up_and_own_kinds_are_left_out()
    {
        var plant = InOuterPot();
        var own = Water(plant);
        own.OwnKindId = Guid.NewGuid();
        own.OwnKindName = "Soak";

        Assert.Empty(Due(plant, [Water(plant, CareKind.ToppedUp)], 120));
        Assert.Empty(Due(plant, [own], 120));
    }

    [Fact]
    public void A_watering_entered_afterwards_for_an_earlier_day_is_left_out()
    {
        var plant = InOuterPot();
        var backDated = Water(plant, on: new DateOnly(2026, 10, 26));

        Assert.Empty(Due(plant, [backDated], 120));
    }

    [Fact]
    public void The_day_is_the_local_one()
    {
        var plant = InOuterPot();
        // 23:30 UTC is 01:30 the next day at +2
        var late = new DateTimeOffset(2026, 10, 27, 23, 30, 0, TimeSpan.Zero);
        var log = Water(plant, at: late, on: new DateOnly(2026, 10, 28));
        var time = new FixedTime(late.AddMinutes(90), TimeSpan.FromHours(2));

        Assert.Single(OuterPotReminders.Find([plant], [log], new UserSettings(), time).Due);
    }

    [Fact]
    public void Zero_turns_it_off_and_null_means_sixty()
    {
        var plant = InOuterPot();
        var log = Water(plant);

        Assert.Empty(Due(plant, [log], 600, new UserSettings { EmptyOuterPotMinutes = 0 }));
        Assert.Empty(Due(plant, [log], 59, new UserSettings { EmptyOuterPotMinutes = null }));
        Assert.Single(Due(plant, [log], 60, new UserSettings { EmptyOuterPotMinutes = null }));
        Assert.Empty(Due(plant, [log], 119, new UserSettings { EmptyOuterPotMinutes = 120 }));
        Assert.Single(Due(plant, [log], 120, new UserSettings { EmptyOuterPotMinutes = 120 }));
    }

    [Fact]
    public void Plants_not_in_the_collection_and_deleted_waterings_are_left_out()
    {
        var gone = InOuterPot("Gone");
        gone.Status = PlantStatus.Died;
        var plant = InOuterPot("Other");
        var deleted = Water(plant);
        deleted.DeletedAt = Watered.AddMinutes(5);

        Assert.Empty(Due(gone, [Water(gone)], 120));
        Assert.Empty(Due(plant, [deleted], 120));
    }

    [Fact]
    public void Plants_watered_together_are_in_one_result_by_name()
    {
        var b = InOuterPot("Beta");
        var a = InOuterPot("Alpha");
        var result = OuterPotReminders.Find([b, a], [Water(b), Water(a)], new UserSettings(), At(90));

        Assert.Equal(["Alpha", "Beta"], result.Due.Select(d => d.Plant.DisplayName));
    }

    [Fact]
    public async Task Ticking_sets_the_time_and_editing_keeps_it()
    {
        var logs = new FakeCareLogRepository();
        var care = new CareService(logs, new FakeTimelineRepository(), new FakePlantRepository(),
            new FakeFeedRepository(), new FakeProductRepository(), new FakeOwnCareKindRepository(), At(90));
        var plant = InOuterPot();
        var log = Water(plant);
        await logs.SaveAsync(log);

        await care.MarkOuterPotEmptiedAsync([log.Id, log.Id]);
        Assert.Equal(Watered.AddMinutes(90), logs.Logs.Single().OuterPotEmptiedAt);

        var edit = logs.Logs.Single().Copy();
        edit.Notes = "Moved";
        edit.OuterPotEmptiedAt = null;
        await care.UpdateAsync(edit, DateOnly.FromDateTime(Watered.UtcDateTime));

        Assert.Equal(Watered.AddMinutes(90), logs.Logs.Single().OuterPotEmptiedAt);
        Assert.Equal("Moved", logs.Logs.Single().Notes);
    }
}
