using Stikling.Core.Flowering;
using Stikling.Core.Models;

namespace Stikling.Core.Tests;

public class FloweringTests
{
    private static readonly Guid PlantId = Guid.NewGuid();
    private static readonly DateTimeOffset Created = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static int made;

    private static FloweringMark Mark(FloweringStage? stage, LooseDate on, Guid? plant = null, bool deleted = false) =>
        new()
        {
            PlantId = plant ?? PlantId, Stage = stage, On = on,
            CreatedAt = Created.AddSeconds(made++),
            DeletedAt = deleted ? Created : null
        };

    private static FloweringMark Mark(FloweringStage? stage, int year, int month, int day) =>
        Mark(stage, LooseDate.Of(new DateOnly(year, month, day)));

    private static IReadOnlyList<FloweringPeriod> Of(params FloweringMark[] marks) => Flowerings.Of(marks, PlantId);

    // Grouping

    [Fact]
    public void Buds_then_flowers_then_done_is_one_flowering()
    {
        var all = Of(
            Mark(FloweringStage.Buds, 2026, 3, 1),
            Mark(FloweringStage.InFlower, 2026, 3, 20),
            Mark(FloweringStage.Done, 2026, 4, 30));

        var f = Assert.Single(all);
        Assert.Equal(LooseDate.Of(new DateOnly(2026, 3, 1)), f.Buds);
        Assert.Equal(LooseDate.Of(new DateOnly(2026, 3, 20)), f.InFlower);
        Assert.Equal(LooseDate.Of(new DateOnly(2026, 4, 30)), f.Done);
        Assert.Equal(f.Buds, f.Start);
        Assert.Equal(FloweringStage.Done, f.CurrentStage);
        Assert.False(f.IsOpen);
        Assert.False(f.EndUnknown);
        Assert.Equal(new DateOnly(2026, 4, 30), f.LastMarked);
        Assert.Equal(3, f.Marks.Count);
    }

    [Fact]
    public void Straight_to_in_flower_starts_there()
    {
        var f = Assert.Single(Of(Mark(FloweringStage.InFlower, 2026, 5, 2)));

        Assert.Null(f.Buds);
        Assert.Equal(f.InFlower, f.Start);
        Assert.Equal(FloweringStage.InFlower, f.CurrentStage);
        Assert.True(f.IsOpen);
    }

    [Fact]
    public void A_repeated_stage_confirms_and_keeps_the_first_date()
    {
        var f = Assert.Single(Of(
            Mark(FloweringStage.InFlower, 2026, 5, 2),
            Mark(FloweringStage.InFlower, 2026, 6, 10)));
        var buds = Assert.Single(Of(
            Mark(FloweringStage.Buds, 2026, 5, 2),
            Mark(FloweringStage.Buds, 2026, 5, 9)));

        Assert.Equal(LooseDate.Of(new DateOnly(2026, 5, 2)), f.InFlower);
        Assert.Equal(new DateOnly(2026, 6, 10), f.LastMarked);
        Assert.Equal(LooseDate.Of(new DateOnly(2026, 5, 2)), buds.Buds);
        Assert.Equal(new DateOnly(2026, 5, 9), buds.LastMarked);
        Assert.Equal(FloweringStage.Buds, buds.CurrentStage);
    }

    [Fact]
    public void Marks_arriving_in_any_order_give_the_same_result()
    {
        var marks = new[]
        {
            Mark(FloweringStage.Buds, 2025, 3, 1),
            Mark(FloweringStage.InFlower, 2025, 3, 20),
            Mark(FloweringStage.Done, 2025, 4, 30),
            Mark(FloweringStage.InFlower, 2026, 3, 25)
        };

        var shuffled = Of(marks[3], marks[1], marks[2], marks[0]);
        var ordered = Of(marks);

        Assert.Equal(2, shuffled.Count);
        Assert.Equal(ordered.Select(f => f.Start), shuffled.Select(f => f.Start));
        Assert.Equal(ordered.Select(f => f.IsOpen), shuffled.Select(f => f.IsOpen));
        Assert.True(shuffled[1].IsOpen);
    }

    [Fact]
    public void The_same_mark_from_two_devices_counts_once()
    {
        var all = Of(
            Mark(FloweringStage.InFlower, 2026, 5, 2),
            Mark(FloweringStage.InFlower, 2026, 5, 2),
            Mark(FloweringStage.Done, 2026, 6, 1),
            Mark(FloweringStage.Done, 2026, 6, 1));

        var f = Assert.Single(all);
        Assert.False(f.IsOpen);
    }

    [Fact]
    public void Buds_after_flowers_starts_a_new_flowering_and_closes_the_old_one()
    {
        var all = Of(
            Mark(FloweringStage.InFlower, 2025, 3, 20),
            Mark(FloweringStage.Buds, 2026, 2, 1));

        Assert.Equal(2, all.Count);
        Assert.True(all[0].EndUnknown);
        Assert.False(all[0].IsOpen);
        Assert.Null(all[0].Done);
        Assert.False(all[1].EndUnknown);
        Assert.True(all[1].IsOpen);
        Assert.Equal(FloweringStage.Buds, all[1].CurrentStage);
    }

    [Fact]
    public void A_lone_done_is_a_flowering_of_its_own()
    {
        var all = Of(Mark(FloweringStage.Done, 2026, 6, 1));

        var f = Assert.Single(all);
        Assert.Equal(FloweringStage.Done, f.CurrentStage);
        Assert.Null(f.Buds);
        Assert.Null(f.InFlower);
        Assert.False(f.IsOpen);
        Assert.False(f.HasStart);
    }

    [Fact]
    public void Marks_with_an_unknown_stage_deleted_marks_and_other_plants_are_skipped()
    {
        var all = Of(
            Mark(null, 2026, 5, 1),
            Mark(FloweringStage.InFlower, LooseDate.Of(new DateOnly(2026, 5, 2)), deleted: true),
            Mark(FloweringStage.InFlower, LooseDate.Of(new DateOnly(2026, 5, 3)), plant: Guid.NewGuid()));

        Assert.Empty(all);
    }

    // Now

    [Theory]
    [InlineData(120, false)]
    [InlineData(121, true)]
    public void An_open_flowering_goes_stale_after_120_days(int days, bool stale)
    {
        var last = new DateOnly(2026, 1, 10);
        var all = Of(Mark(FloweringStage.InFlower, LooseDate.Of(last)));

        var now = FloweringNow.Of(all, last.AddDays(days));

        Assert.NotNull(now);
        Assert.Equal(stale, now.Stale);
        Assert.Equal(FloweringStage.InFlower, now.Stage);
    }

    [Fact]
    public void Nothing_is_flowering_when_the_newest_flowering_is_done()
    {
        var all = Of(Mark(FloweringStage.InFlower, 2026, 1, 1), Mark(FloweringStage.Done, 2026, 2, 1));

        Assert.Null(FloweringNow.Of(all, new DateOnly(2026, 2, 2)));
        Assert.Null(FloweringNow.Of([], new DateOnly(2026, 2, 2)));
    }

    // Usual start

    [Fact]
    public void The_usual_start_needs_two_different_years()
    {
        var one = Of(Mark(FloweringStage.Buds, 2025, 3, 1), Mark(FloweringStage.Done, 2025, 5, 1),
            Mark(FloweringStage.Buds, 2025, 9, 1));

        Assert.Null(UsualStart.Of(one, new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void The_usual_start_is_the_latest_month_and_late_when_this_year_has_none_by_then()
    {
        var all = Of(
            Mark(FloweringStage.Buds, 2024, 3, 10),
            Mark(FloweringStage.Done, 2024, 4, 10),
            Mark(FloweringStage.InFlower, 2025, 4, 2));

        var early = UsualStart.Of(all, new DateOnly(2026, 4, 30));
        var late = UsualStart.Of(all, new DateOnly(2026, 5, 1));

        Assert.NotNull(early);
        Assert.Equal(4, early.Month);
        Assert.Equal(2, early.Years);
        Assert.False(early.LateThisYear);
        Assert.True(late!.LateThisYear);
    }

    [Fact]
    public void Not_late_when_a_flowering_has_started_this_year()
    {
        var all = Of(
            Mark(FloweringStage.InFlower, 2025, 3, 2), Mark(FloweringStage.Done, 2025, 4, 2),
            Mark(FloweringStage.InFlower, 2026, 1, 15));

        Assert.False(UsualStart.Of(all, new DateOnly(2026, 8, 1))!.LateThisYear);
    }

    [Fact]
    public void Year_only_flowerings_are_ignored_for_the_usual_start()
    {
        var all = Of(
            Mark(FloweringStage.InFlower, LooseDate.Of(2023)),
            Mark(FloweringStage.Done, LooseDate.Of(2023, 6)),
            Mark(FloweringStage.InFlower, LooseDate.Of(2024)),
            Mark(FloweringStage.Done, LooseDate.Of(2024, 6)),
            Mark(FloweringStage.InFlower, 2025, 4, 2));

        Assert.Null(UsualStart.Of(all, new DateOnly(2026, 1, 1)));
    }

    // Calendar

    private static Plant APlant(string name, PlantStatus status = PlantStatus.Active, bool deleted = false) =>
        new() { Nickname = name, Status = status, DeletedAt = deleted ? Created : null };

    private static FloweringMark For(Plant plant, FloweringStage stage, LooseDate on) =>
        Mark(stage, on, plant.Id);

    private static LooseDate Day(int y, int m, int d) => LooseDate.Of(new DateOnly(y, m, d));

    [Fact]
    public void A_flowering_covers_its_months_across_the_new_year()
    {
        var hoya = APlant("Hoya");
        var marks = new[]
        {
            For(hoya, FloweringStage.InFlower, Day(2025, 11, 20)),
            For(hoya, FloweringStage.Done, Day(2026, 2, 3))
        };

        var calendar = FloweringCalendar.Build([hoya], marks, new DateOnly(2026, 6, 1));

        Assert.Equal(12, calendar.Months.Count);
        Assert.Equal(1, calendar.Months[0].Month);
        Assert.Equal([2026], Assert.Single(calendar.Months[0].Plants).Years);
        Assert.Equal([2026], Assert.Single(calendar.Months[1].Plants).Years);
        Assert.Equal([2025], Assert.Single(calendar.Months[10].Plants).Years);
        Assert.Equal([2025], Assert.Single(calendar.Months[11].Plants).Years);
        Assert.Empty(calendar.Months[2].Plants);
        Assert.False(calendar.IsEmpty);
    }

    [Fact]
    public void A_month_precision_date_covers_the_whole_month()
    {
        var orchid = APlant("Orchid");
        var marks = new[] { For(orchid, FloweringStage.InFlower, LooseDate.Of(2025, 5)) };

        var calendar = FloweringCalendar.Build([orchid], marks, new DateOnly(2025, 5, 31));

        Assert.Single(calendar.Months[4].Plants);
    }

    [Fact]
    public void Year_only_dates_are_left_out()
    {
        var orchid = APlant("Orchid");
        var marks = new[]
        {
            For(orchid, FloweringStage.InFlower, LooseDate.Of(2024)),
            For(orchid, FloweringStage.Done, LooseDate.Of(2024))
        };

        var calendar = FloweringCalendar.Build([orchid], marks, new DateOnly(2026, 6, 1));

        Assert.True(calendar.IsEmpty);
    }

    [Fact]
    public void A_stale_open_flowering_stops_at_its_last_mark()
    {
        var hoya = APlant("Hoya");
        var marks = new[] { For(hoya, FloweringStage.InFlower, Day(2026, 1, 10)) };

        var calendar = FloweringCalendar.Build([hoya], marks, new DateOnly(2026, 8, 1));

        Assert.Single(calendar.Months[0].Plants);
        Assert.Empty(calendar.Months[1].Plants);
        Assert.Empty(calendar.Months[7].Plants);
        Assert.Empty(calendar.InFlowerNow);
    }

    [Fact]
    public void A_fresh_open_flowering_runs_to_today()
    {
        var hoya = APlant("Hoya");
        var marks = new[] { For(hoya, FloweringStage.InFlower, Day(2026, 3, 10)) };

        var calendar = FloweringCalendar.Build([hoya], marks, new DateOnly(2026, 5, 4));

        Assert.Equal(3, calendar.Months.Count(m => m.Plants.Count > 0));
        var now = Assert.Single(calendar.InFlowerNow);
        Assert.Equal(Day(2026, 3, 10), now.Since);
        Assert.Empty(calendar.BudsNow);
        Assert.Equal(6, calendar.NextMonth);
    }

    [Fact]
    public void Buds_now_are_listed_apart_from_flowers()
    {
        var hoya = APlant("Hoya");
        var marks = new[] { For(hoya, FloweringStage.Buds, Day(2026, 5, 1)) };

        var calendar = FloweringCalendar.Build([hoya], marks, new DateOnly(2026, 5, 4));

        Assert.Equal(hoya.Id, Assert.Single(calendar.BudsNow).Plant.Id);
        Assert.Empty(calendar.InFlowerNow);
    }

    [Fact]
    public void A_flowering_closed_by_a_later_one_ends_at_its_last_mark()
    {
        var hoya = APlant("Hoya");
        var marks = new[]
        {
            For(hoya, FloweringStage.InFlower, Day(2024, 3, 10)),
            For(hoya, FloweringStage.Buds, Day(2025, 2, 1)),
            For(hoya, FloweringStage.Done, Day(2025, 2, 20))
        };

        var calendar = FloweringCalendar.Build([hoya], marks, new DateOnly(2026, 6, 1));

        Assert.Equal([2024], calendar.Months[2].Plants.Single().Years);
        Assert.Empty(calendar.Months[3].Plants);
    }

    [Fact]
    public void Inactive_and_deleted_plants_are_left_out()
    {
        var sold = APlant("Sold", PlantStatus.Sold);
        var gone = APlant("Gone", deleted: true);
        var marks = new[]
        {
            For(sold, FloweringStage.InFlower, Day(2026, 5, 1)),
            For(gone, FloweringStage.InFlower, Day(2026, 5, 1))
        };

        var calendar = FloweringCalendar.Build([sold, gone], marks, new DateOnly(2026, 5, 4));

        Assert.True(calendar.IsEmpty);
        Assert.Empty(calendar.InFlowerNow);
    }

    [Fact]
    public void Plants_are_sorted_by_name_in_each_month()
    {
        var b = APlant("banana");
        var a = APlant("Anthurium");
        var marks = new[]
        {
            For(b, FloweringStage.InFlower, Day(2025, 5, 1)),
            For(a, FloweringStage.InFlower, Day(2025, 5, 1))
        };

        var calendar = FloweringCalendar.Build([b, a], marks, new DateOnly(2026, 1, 1));

        Assert.Equal(["Anthurium", "banana"], calendar.Months[4].Plants.Select(p => p.Plant.DisplayName));
    }

    [Fact]
    public void Plants_that_usually_flower_next_month_leave_out_those_in_flower_now()
    {
        var steady = APlant("Steady");
        var now = APlant("Now");
        var marks = new[]
        {
            // Flowered in June in 2024 and 2025
            For(steady, FloweringStage.InFlower, Day(2024, 6, 2)), For(steady, FloweringStage.Done, Day(2024, 6, 20)),
            For(steady, FloweringStage.InFlower, Day(2025, 6, 2)), For(steady, FloweringStage.Done, Day(2025, 6, 20)),
            // In flower now, and flowered in June before
            For(now, FloweringStage.InFlower, Day(2025, 6, 2)), For(now, FloweringStage.Done, Day(2025, 6, 20)),
            For(now, FloweringStage.InFlower, Day(2026, 5, 20))
        };

        var calendar = FloweringCalendar.Build([steady, now], marks, new DateOnly(2026, 5, 25));

        Assert.Equal(6, calendar.NextMonth);
        var usually = Assert.Single(calendar.UsuallyNextMonth);
        Assert.Equal("Steady", usually.Plant.DisplayName);
        Assert.Equal([2024, 2025], usually.Years);
        Assert.Equal("Now", Assert.Single(calendar.InFlowerNow).Plant.DisplayName);
    }

    [Fact]
    public void A_calendar_with_no_marks_is_empty()
    {
        var calendar = FloweringCalendar.Build([APlant("Hoya")], [], new DateOnly(2026, 5, 25));

        Assert.True(calendar.IsEmpty);
        Assert.Equal(12, calendar.Months.Count);
    }

    // Service

    private sealed class Setup
    {
        public readonly FakeFloweringMarkRepository Marks = new();
        public readonly FakeTimelineRepository Timeline = new();
        public readonly FloweringService Service;
        public readonly Plant Plant = APlant("Hoya");

        public Setup() => Service = new FloweringService(Marks, Timeline, new FixedTime(new DateTimeOffset(2026, 5, 20, 9, 30, 0, TimeSpan.Zero)));
    }

    [Fact]
    public async Task Marking_today_saves_the_mark_and_writes_a_linked_history_line()
    {
        var s = new Setup();

        var mark = await s.Service.MarkAsync(s.Plant, FloweringStage.Buds, new DateOnly(2026, 5, 20));

        var entry = Assert.Single(s.Timeline.Entries);
        Assert.Equal(TimelineKind.Change, entry.Kind);
        Assert.Equal(TimelineEvent.Flowering, entry.Event);
        Assert.Equal(SubjectType.Plant, entry.SubjectType);
        Assert.Equal(s.Plant.Id, entry.SubjectId);
        Assert.Equal("Spike or buds", entry.Text);
        Assert.Equal(new DateTimeOffset(2026, 5, 20, 9, 30, 0, TimeSpan.Zero), entry.OccurredAt);
        Assert.Equal(entry.Id, mark.TimelineEntryId);
        Assert.Equal(LooseDate.Of(new DateOnly(2026, 5, 20)), mark.On);
        Assert.Single(await s.Service.ForPlantAsync(s.Plant.Id));
    }

    [Theory]
    [InlineData(FloweringStage.InFlower, "In flower")]
    [InlineData(FloweringStage.Done, "Done flowering")]
    public async Task The_history_line_names_the_stage(FloweringStage stage, string text)
    {
        var s = new Setup();

        await s.Service.MarkAsync(s.Plant, stage, new DateOnly(2026, 5, 20));

        Assert.Equal(text, s.Timeline.Entries.Single().Text);
    }

    [Fact]
    public async Task An_earlier_day_is_recorded_at_noon()
    {
        var s = new Setup();

        await s.Service.MarkAsync(s.Plant, FloweringStage.InFlower, new DateOnly(2026, 5, 10));

        Assert.Equal(new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero), s.Timeline.Entries.Single().OccurredAt);
    }

    [Fact]
    public async Task A_day_in_the_future_is_refused()
    {
        var s = new Setup();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => s.Service.MarkAsync(s.Plant, FloweringStage.InFlower, new DateOnly(2026, 5, 21)));

        Assert.Empty(s.Timeline.Entries);
        Assert.Empty(s.Marks.Marks);
    }

    [Fact]
    public async Task Still_in_flower_saves_a_mark_dated_today_and_writes_no_history()
    {
        var s = new Setup();

        await s.Service.StillAsync(s.Plant, FloweringStage.InFlower);

        var mark = Assert.Single(s.Marks.Marks.Values);
        Assert.Equal(LooseDate.Of(new DateOnly(2026, 5, 20)), mark.On);
        Assert.Equal(FloweringStage.InFlower, mark.Stage);
        Assert.Null(mark.TimelineEntryId);
        Assert.Empty(s.Timeline.Entries);
    }

    [Fact]
    public async Task A_past_flowering_writes_its_marks_and_no_history()
    {
        var s = new Setup();

        await s.Service.AddPastAsync(s.Plant, LooseDate.Of(2025, 3), LooseDate.Of(2025, 4), LooseDate.Of(2025, 6));

        var all = Flowerings.Of(s.Marks.Marks.Values, s.Plant.Id);
        var f = Assert.Single(all);
        Assert.Equal(LooseDate.Of(2025, 3), f.Buds);
        Assert.Equal(LooseDate.Of(2025, 6), f.Done);
        Assert.Empty(s.Timeline.Entries);
    }

    [Fact]
    public async Task A_past_flowering_without_buds_or_an_end_writes_one_mark()
    {
        var s = new Setup();

        await s.Service.AddPastAsync(s.Plant, null, LooseDate.Of(2025, 5), null);

        Assert.Equal(FloweringStage.InFlower, Assert.Single(s.Marks.Marks.Values).Stage);
    }

    [Fact]
    public async Task A_past_flowering_in_the_future_or_out_of_order_is_refused()
    {
        var s = new Setup();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => s.Service.AddPastAsync(s.Plant, null, LooseDate.Of(2026, 6), null));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => s.Service.AddPastAsync(s.Plant, LooseDate.Of(2025, 5), LooseDate.Of(2025, 4), null));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => s.Service.AddPastAsync(s.Plant, null, LooseDate.Of(2025, 5), LooseDate.Of(2025, 4)));

        Assert.Empty(s.Marks.Marks);
    }

    [Fact]
    public async Task Removing_a_mark_removes_its_history_line_too()
    {
        var s = new Setup();
        var mark = await s.Service.MarkAsync(s.Plant, FloweringStage.InFlower, new DateOnly(2026, 5, 10));
        var confirmed = new FloweringMark { PlantId = s.Plant.Id, Stage = FloweringStage.InFlower, On = LooseDate.Of(new DateOnly(2026, 5, 15)) };
        await s.Marks.SaveAsync(confirmed);

        await s.Service.RemoveAsync(mark);
        await s.Service.RemoveAsync(confirmed);

        Assert.Empty(await s.Service.ForPlantAsync(s.Plant.Id));
        Assert.True(s.Timeline.Entries.Single().IsDeleted);
    }

    // Storage

    [Fact]
    public void A_mark_is_stored_as_text_and_a_stage_from_a_later_version_reads_as_null()
    {
        var options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        var mark = Mark(FloweringStage.InFlower, LooseDate.Of(2025, 5));

        var json = System.Text.Json.JsonSerializer.Serialize(mark, options);
        var back = System.Text.Json.JsonSerializer.Deserialize<FloweringMark>(json, options)!;
        var future = System.Text.Json.JsonSerializer.Deserialize<FloweringMark>(
            json.Replace("InFlower", "Seeding"), options)!;

        Assert.Contains("\"stage\":\"InFlower\"", json);
        Assert.Equal(FloweringStage.InFlower, back.Stage);
        Assert.Null(future.Stage);
        Assert.Empty(Flowerings.Of([future], PlantId));
        Assert.NotEmpty(future.Validate(new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void A_mark_needs_a_plant_a_stage_and_a_day_that_isnt_ahead()
    {
        var today = new DateOnly(2026, 5, 20);

        Assert.Empty(Mark(FloweringStage.Buds, 2026, 5, 20).Validate(today));
        Assert.NotEmpty(Mark(FloweringStage.Buds, 2026, 5, 21).Validate(today));
        Assert.NotEmpty(Mark(null, 2026, 5, 1).Validate(today));
        Assert.NotEmpty(Mark(FloweringStage.Buds, LooseDate.Of(2026, 5), plant: Guid.Empty).Validate(today));
    }

    [Fact]
    public void A_flowering_left_open_for_over_a_year_ends_and_the_next_mark_starts_a_new_one()
    {
        var all = Of(
            Mark(FloweringStage.InFlower, LooseDate.Of(2024, 5)),
            Mark(FloweringStage.InFlower, 2026, 4, 10));

        Assert.Equal(2, all.Count);
        Assert.True(all[0].EndUnknown);
        Assert.False(all[0].IsOpen);
        Assert.True(all[1].IsOpen);
    }

    [Fact]
    public void Still_in_flower_within_a_year_keeps_the_same_flowering()
    {
        var all = Of(
            Mark(FloweringStage.InFlower, 2026, 1, 10),
            Mark(FloweringStage.InFlower, 2026, 6, 1));

        Assert.Single(all);
        Assert.True(all[0].IsOpen);
    }

    [Fact]
    public void A_forgotten_open_flowering_gets_no_badge_and_no_question()
    {
        var marks = new[] { Mark(FloweringStage.InFlower, LooseDate.Of(2024, 5)) };
        var today = new DateOnly(2026, 10, 14);

        var all = Flowerings.Of(marks, PlantId, today);

        Assert.True(all[0].EndUnknown);
        Assert.Null(FloweringNow.Of(all, today));
        Assert.NotNull(FloweringNow.Of(Flowerings.Of(marks, PlantId, new DateOnly(2024, 11, 1)), new DateOnly(2024, 11, 1)));
    }
}
