using Stikling.Core.Models;
using Stikling.Core.Plants;

namespace Stikling.Core.Tests;

public class RecentChangesTests
{
    private static readonly DateOnly Today = new(2026, 10, 11);

    private static DateOnly Ago(int days) => Today.AddDays(-days);

    private static DateOnly LocalDay(DateTimeOffset moment) => DateOnly.FromDateTime(moment.DateTime);

    private static Plant PlantInARoom() => new() { Nickname = "Hoya", PlaceId = Guid.NewGuid() };

    private static CareLog Repot(Plant plant, DateOnly day) =>
        new() { PlantId = plant.Id, OccurredOn = day, Kind = CareKind.Repotted };

    private static TimelineEntry Change(Plant plant, DateOnly day, string text) => new()
    {
        SubjectType = SubjectType.Plant,
        SubjectId = plant.Id,
        Kind = TimelineKind.Change,
        OccurredAt = new DateTimeOffset(day.Year, day.Month, day.Day, 12, 0, 0, TimeSpan.Zero),
        Text = text
    };

    private static IReadOnlyList<RecentChange> Of(
        Plant plant, CareLog[]? care = null, TimelineEntry[]? timeline = null, int days = 14) =>
        RecentChanges.Of(plant, care ?? [], timeline ?? [], Today, days, LocalDay);

    [Fact]
    public void A_repot_inside_the_window_counts_and_one_outside_does_not()
    {
        var plant = PlantInARoom();

        Assert.Equal([new RecentChange(RecentChangeKind.Repotted, Ago(14))], Of(plant, [Repot(plant, Ago(14))]));
        Assert.Empty(Of(plant, [Repot(plant, Ago(15))]));
    }

    [Fact]
    public void Only_the_newest_repot_counts()
    {
        var plant = PlantInARoom();

        var result = Of(plant, [Repot(plant, Ago(40)), Repot(plant, Ago(3)), Repot(plant, Ago(20))]);

        Assert.Equal([new RecentChange(RecentChangeKind.Repotted, Ago(3))], result);
    }

    [Fact]
    public void A_deleted_repot_is_ignored_and_so_is_another_plants()
    {
        var plant = PlantInARoom();
        var deleted = Repot(plant, Ago(1));
        deleted.DeletedAt = DateTimeOffset.UtcNow;

        Assert.Empty(Of(plant, [deleted, Repot(new Plant(), Ago(1))]));
    }

    [Fact]
    public void A_move_inside_the_window_counts_and_one_outside_does_not()
    {
        var plant = PlantInARoom();

        Assert.Equal(
            [new RecentChange(RecentChangeKind.Moved, Ago(3))],
            Of(plant, timeline: [Change(plant, Ago(3), "Moved to Hall")]));
        Assert.Empty(Of(plant, timeline: [Change(plant, Ago(15), "Moved to Hall")]));
    }

    [Fact]
    public void Moved_from_counts_even_among_other_lines()
    {
        var plant = PlantInARoom();

        var result = Of(plant, timeline: [Change(plant, Ago(2), "Light: Bright\nMoved from Kitchen to Hall")]);

        Assert.Equal([new RecentChange(RecentChangeKind.Moved, Ago(2))], result);
    }

    [Fact]
    public void Placed_in_and_removed_from_are_not_moves()
    {
        var plant = PlantInARoom();

        Assert.Empty(Of(plant, timeline: [Change(plant, Ago(1), "Placed in Hall"), Change(plant, Ago(1), "Removed from Hall")]));
    }

    [Fact]
    public void A_move_is_left_out_when_the_plant_has_no_place_now()
    {
        var plant = new Plant { Nickname = "Hoya" };

        Assert.Empty(Of(plant, timeline: [Change(plant, Ago(1), "Moved to Hall")]));
    }

    [Fact]
    public void Only_the_newest_move_counts_and_deleted_and_other_plants_entries_are_ignored()
    {
        var plant = PlantInARoom();
        var deleted = Change(plant, Ago(1), "Moved to Hall");
        deleted.DeletedAt = DateTimeOffset.UtcNow;

        var result = Of(plant, timeline:
        [
            Change(plant, Ago(9), "Moved to Kitchen"),
            Change(plant, Ago(4), "Moved to Hall"),
            deleted,
            Change(new Plant(), Ago(0), "Moved to Hall")
        ]);

        Assert.Equal([new RecentChange(RecentChangeKind.Moved, Ago(4))], result);
    }

    [Fact]
    public void Arriving_counts_only_when_the_day_is_known()
    {
        var plant = PlantInARoom();
        plant.AcquiredOn = LooseDate.Of(Ago(5));
        Assert.Equal([new RecentChange(RecentChangeKind.Arrived, Ago(5))], Of(plant));

        plant.AcquiredOn = LooseDate.Of(Today.Year, Today.Month);
        Assert.Empty(Of(plant));

        plant.AcquiredOn = LooseDate.Of(Ago(15));
        Assert.Empty(Of(plant));
    }

    [Fact]
    public void Zero_days_turns_it_off()
    {
        var plant = PlantInARoom();
        plant.AcquiredOn = LooseDate.Of(Today);

        Assert.Empty(Of(plant, [Repot(plant, Today)], [Change(plant, Today, "Moved to Hall")], days: 0));
        Assert.Single(Of(plant, days: 1));
    }

    [Fact]
    public void The_changes_come_oldest_first()
    {
        var plant = PlantInARoom();
        plant.AcquiredOn = LooseDate.Of(Ago(10));

        var result = Of(plant, [Repot(plant, Ago(2))], [Change(plant, Ago(6), "Moved to Hall")]);

        Assert.Equal(
            [RecentChangeKind.Arrived, RecentChangeKind.Moved, RecentChangeKind.Repotted],
            result.Select(c => c.Kind));
    }

    [Fact]
    public void A_setting_never_chosen_reads_as_two_weeks()
    {
        Assert.Equal(14, RecentChanges.DaysOf(null));
        Assert.Equal(0, RecentChanges.DaysOf(0));
        Assert.Equal(28, RecentChanges.DaysOf(28));
    }
}
