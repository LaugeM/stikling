using System.Globalization;
using System.Text.Json;
using Stikling.Core.Measurements;
using Stikling.Core.Models;
using Stikling.Core.Settings;
using Stikling.Core.Text;
using Stikling.Core.Timeline;

namespace Stikling.Core.Tests;

[Collection("Display choices")]
public sealed class MeasurementTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly Guid plant = Guid.NewGuid();
    private readonly FakeMeasurementRepository measurements = new();
    private readonly FakeOwnMeasureKindRepository kinds = new();
    private readonly FakeTimelineRepository timeline = new();
    private readonly FakePhotoRepository photos = new();
    private readonly MeasurementService service;
    private readonly TimelineService timelineService;

    public MeasurementTests()
    {
        Lengths.Unit = LengthUnit.Centimetres;
        var time = new FixedTime(Now);
        service = new MeasurementService(measurements, kinds, timeline, time);
        timelineService = new TimelineService(timeline, photos, measurements, time);
    }

    public void Dispose() => Lengths.Unit = LengthUnit.Centimetres;

    private TimelineEntry Note(string text = "Looking good", DateTimeOffset? at = null)
    {
        var entry = new TimelineEntry
        {
            SubjectType = SubjectType.Plant,
            SubjectId = plant,
            Kind = TimelineKind.Note,
            Text = text,
            OccurredAt = at ?? Now
        };
        timeline.Entries.Add(entry);
        return entry;
    }

    private List<Measurement> Live(Guid? entryId = null) =>
        [.. measurements.Measurements.Values.Where(m => !m.IsDeleted && (entryId is null || m.EntryId == entryId))];

    // Validation

    private Measurement Valid() => new()
    {
        SubjectType = SubjectType.Plant, SubjectId = plant, EntryId = Guid.NewGuid(), Kind = MeasureKind.Height, Value = 12.5m
    };

    [Fact]
    public void A_measurement_with_a_kind_and_a_value_is_valid() => Assert.Empty(Valid().Validate());

    [Fact]
    public void A_measurement_has_exactly_one_of_a_kind_and_an_own_kind()
    {
        var neither = Valid();
        neither.Kind = null;
        Assert.NotEmpty(neither.Validate());

        var both = Valid();
        both.OwnKindId = Guid.NewGuid();
        both.OwnKindName = "Nodes";
        Assert.NotEmpty(both.Validate());
    }

    [Fact]
    public void An_own_kind_needs_a_copy_of_its_name()
    {
        var m = Valid();
        m.Kind = null;
        m.OwnKindId = Guid.NewGuid();
        m.Value = 3;
        Assert.NotEmpty(m.Validate());
        m.OwnKindName = "Nodes";
        Assert.Empty(m.Validate());
    }

    [Fact]
    public void A_value_can_not_be_negative_and_a_count_is_whole()
    {
        var negative = Valid();
        negative.Value = -1;
        Assert.NotEmpty(negative.Validate());

        var leaves = Valid();
        leaves.Kind = MeasureKind.Leaves;
        leaves.Value = 7;
        Assert.Empty(leaves.Validate());
        leaves.Value = 7.5m;
        Assert.NotEmpty(leaves.Validate());
    }

    [Fact]
    public void A_measurement_belongs_to_a_plant_or_a_propagation()
    {
        var m = Valid();
        m.SubjectType = SubjectType.Propagation;
        Assert.Empty(m.Validate());
        m.SubjectType = SubjectType.Pot;
        Assert.NotEmpty(m.Validate());
    }

    [Fact]
    public void An_own_kind_needs_a_name() =>
        Assert.NotEmpty(new OwnMeasureKind { Name = "  " }.Validate());

    [Fact]
    public void The_same_name_gives_the_same_own_kind_id()
    {
        Assert.Equal(OwnMeasureKind.IdFor("Nodes"), OwnMeasureKind.IdFor(" nodes "));
        Assert.NotEqual(OwnMeasureKind.IdFor("Nodes"), OwnMeasureKind.IdFor("Aerial root"));
    }

    // Writing the values of an entry

    [Fact]
    public async Task Setting_the_values_adds_changes_and_removes()
    {
        var entry = Note();

        await service.SetForEntryAsync(entry, [MeasureValue.Of(MeasureKind.Height, 30), MeasureValue.Of(MeasureKind.Leaves, 6)]);
        Assert.Equal(2, Live(entry.Id).Count);
        Assert.All(Live(), m => Assert.Equal(entry.OccurredAt, m.MeasuredAt));

        await service.SetForEntryAsync(entry, [MeasureValue.Of(MeasureKind.Height, 34.26m), MeasureValue.Of(MeasureKind.Width, 12)]);
        var live = Live(entry.Id);
        Assert.Equal(2, live.Count);
        Assert.Equal(34.3m, live.Single(m => m.Kind == MeasureKind.Height).Value);
        Assert.Equal(12m, live.Single(m => m.Kind == MeasureKind.Width).Value);
        Assert.DoesNotContain(live, m => m.Kind == MeasureKind.Leaves);

        // Nothing changed, nothing is saved again
        var saves = measurements.Saves;
        await service.SetForEntryAsync(entry, [MeasureValue.Of(MeasureKind.Height, 34.3m), MeasureValue.Of(MeasureKind.Width, 12)]);
        Assert.Equal(saves, measurements.Saves);

        await service.SetForEntryAsync(entry, []);
        Assert.Empty(Live(entry.Id));
        Assert.Equal(TimelineKind.Note, entry.Kind);
    }

    [Fact]
    public async Task A_value_that_is_not_valid_saves_nothing()
    {
        var entry = Note();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SetForEntryAsync(entry, [MeasureValue.Of(MeasureKind.Height, 30), MeasureValue.Of(MeasureKind.Leaves, 6.5m)]));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SetForEntryAsync(entry, [MeasureValue.Of(MeasureKind.Height, -2)]));

        Assert.Empty(measurements.Measurements);
    }

    [Fact]
    public async Task Measurements_with_no_text_and_no_photos_become_a_measured_change_entry()
    {
        var entry = await service.AddMeasuredEntryAsync(
            SubjectType.Plant, plant, [MeasureValue.Of(MeasureKind.Leaves, 7), MeasureValue.Of(MeasureKind.Height, 34)], new DateOnly(2026, 11, 8));

        Assert.Contains(entry, timeline.Entries);
        Assert.Equal(TimelineKind.Change, entry.Kind);
        Assert.Equal(TimelineEvent.Measured, entry.Event);
        Assert.Equal("Measured: 34 cm tall, 7 leaves", entry.Text);
        var line = Assert.Single(entry.Lines!);
        Assert.Equal(MeasuredEntries.LineKey, line.Key);
        Assert.Equal(new DateOnly(2026, 11, 8), new DateOnly(entry.OccurredAt.Year, entry.OccurredAt.Month, entry.OccurredAt.Day));
        Assert.All(Live(entry.Id), m => Assert.Equal(entry.OccurredAt, m.MeasuredAt));
    }

    [Fact]
    public async Task A_measured_entry_is_shown_in_the_readers_language_and_unit()
    {
        var entry = await service.AddMeasuredEntryAsync(SubjectType.Plant, plant, [MeasureValue.Of(MeasureKind.Leaves, 1), MeasureValue.Of(MeasureKind.Height, 25.4m)]);
        var danish = CultureInfo.GetCultureInfo("da-DK");

        Assert.Equal("Målt: 25,4 cm høj, 1 blad", HistoryText.Show(entry, e => e.ToString(), e => e.ToString(), danish));

        Lengths.Unit = LengthUnit.Inches;
        var inInches = HistoryText.Show(entry, e => e.ToString(), e => e.ToString(), danish);
        Assert.StartsWith("Målt: 10 ", inInches);
        Assert.EndsWith(" høj, 1 blad", inInches);
        Assert.DoesNotContain("cm", inInches);
        // The text that is kept never follows the unit
        Assert.Equal("Measured: 25.4 cm tall, 1 leaf", entry.Text);
    }

    [Fact]
    public async Task A_measured_entry_must_keep_a_value()
    {
        var entry = await service.AddMeasuredEntryAsync(SubjectType.Plant, plant, [MeasureValue.Of(MeasureKind.Height, 34)]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetForEntryAsync(entry, []));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AddMeasuredEntryAsync(SubjectType.Plant, plant, []));
        Assert.Single(Live(entry.Id));
    }

    [Fact]
    public async Task Changing_the_values_of_a_measured_entry_writes_its_line_again()
    {
        var entry = await service.AddMeasuredEntryAsync(SubjectType.Plant, plant, [MeasureValue.Of(MeasureKind.Height, 34)]);

        await service.SetForEntryAsync(entry, [MeasureValue.Of(MeasureKind.Height, 35), MeasureValue.Of(MeasureKind.RootLength, 3)]);

        Assert.Equal("Measured: 35 cm tall, 3 cm of roots", entry.Text);
        Assert.Contains(entry.Id, timeline.Updated);
    }

    // Entries

    [Fact]
    public async Task Deleting_an_entry_deletes_its_measurements()
    {
        var entry = Note();
        var other = Note("Another");
        await service.SetForEntryAsync(entry, [MeasureValue.Of(MeasureKind.Height, 30)]);
        await service.SetForEntryAsync(other, [MeasureValue.Of(MeasureKind.Height, 31)]);

        await timelineService.RemoveAsync(entry.Id);

        Assert.True(entry.IsDeleted);
        Assert.Empty(Live(entry.Id));
        Assert.Single(Live(other.Id));
        Assert.Equal(30m, measurements.Measurements.Values.Single(m => m.EntryId == entry.Id).Value);
    }

    [Fact]
    public async Task Correcting_the_day_of_an_entry_moves_its_measurements()
    {
        var entry = Note();
        await service.SetForEntryAsync(entry, [MeasureValue.Of(MeasureKind.Height, 30), MeasureValue.Of(MeasureKind.Leaves, 4)]);

        await timelineService.CorrectAsync(entry, "Looking good", new DateOnly(2026, 11, 3));

        Assert.NotEqual(Now, entry.OccurredAt);
        Assert.All(Live(entry.Id), m => Assert.Equal(entry.OccurredAt, m.MeasuredAt));
    }

    [Fact]
    public async Task Correcting_the_day_of_a_measured_entry_keeps_its_line_and_versions()
    {
        var entry = await service.AddMeasuredEntryAsync(SubjectType.Plant, plant, [MeasureValue.Of(MeasureKind.Height, 34)]);
        Assert.True(TimelineService.CanCorrect(entry));

        Assert.True(await timelineService.CorrectAsync(entry, null, new DateOnly(2026, 11, 3)));

        Assert.Equal("Measured: 34 cm tall", entry.Text);
        Assert.Equal(TimelineEvent.Measured, entry.Event);
        Assert.Empty(entry.Edits);
        Assert.All(Live(entry.Id), m => Assert.Equal(entry.OccurredAt, m.MeasuredAt));
    }

    [Fact]
    public async Task A_note_can_lose_its_text_when_it_keeps_measurements()
    {
        var entry = Note();
        await service.SetForEntryAsync(entry, [MeasureValue.Of(MeasureKind.Height, 30)]);

        await Assert.ThrowsAsync<ArgumentException>(() => timelineService.CorrectAsync(entry, "", new DateOnly(2026, 11, 10)));
        await timelineService.CorrectAsync(entry, "", new DateOnly(2026, 11, 10), keepsMeasurements: true);
        await service.SetForEntryAsync(entry, [MeasureValue.Of(MeasureKind.Height, 30)]);

        Assert.Equal(TimelineEvent.Measured, entry.Event);
        Assert.Equal("Measured: 30 cm tall", entry.Text);
    }

    [Fact]
    public async Task An_entry_of_photos_with_measurements_stays_when_its_photos_go()
    {
        var photo = new Photo { TakenAt = Now };
        photos.Photos[photo.Id] = photo;
        var entry = new TimelineEntry { SubjectType = SubjectType.Plant, SubjectId = plant, Kind = TimelineKind.Photo, OccurredAt = Now, PhotoIds = [photo.Id] };
        timeline.Entries.Add(entry);
        await service.SetForEntryAsync(entry, [MeasureValue.Of(MeasureKind.Height, 30)]);

        await photos.DeleteAsync(photo.Id);
        await timelineService.RemoveEmptiedEntriesAsync(plant, photo.Id);

        Assert.False(entry.IsDeleted);
        Assert.Equal(TimelineEvent.Measured, entry.Event);
        Assert.Single(Live(entry.Id));
    }

    [Fact]
    public async Task A_measured_entry_is_not_a_change_to_the_plant_but_it_is_a_visit()
    {
        var entry = await service.AddMeasuredEntryAsync(SubjectType.Plant, plant, [MeasureValue.Of(MeasureKind.Height, 34)]);

        Assert.DoesNotContain(entry, await timeline.GetPlantChangesAsync());
        Assert.True(TimelineActivity.Counts(entry));
    }

    // Curves

    private async Task Measure(int day, MeasureKind kind, decimal value)
    {
        var entry = Note($"Day {day}", new DateTimeOffset(2026, 10, day, 12, 0, 0, TimeSpan.Zero));
        await service.SetForEntryAsync(entry, [MeasureValue.Of(kind, value)]);
    }

    [Fact]
    public async Task A_curve_needs_three_points()
    {
        await Measure(1, MeasureKind.Height, 10);
        await Measure(5, MeasureKind.Height, 12);
        Assert.Empty(await service.CurvesForAsync(plant));

        await Measure(9, MeasureKind.Height, 15);
        var curve = Assert.Single(await service.CurvesForAsync(plant));
        Assert.Equal(MeasureKind.Height, curve.Kind);
        Assert.Equal([10m, 12m, 15m], curve.Points.Select(p => p.Value));
        Assert.Equal(new DateOnly(2026, 10, 9), curve.Latest.Day);
        Assert.True(curve.IsLength);
    }

    [Fact]
    public async Task Curves_come_in_the_order_of_the_kinds_with_own_kinds_last_by_name()
    {
        var nodes = await service.AddOwnKindAsync("Nodes", false);
        var aerial = await service.AddOwnKindAsync("Aerial root", true);
        foreach (var day in new[] { 1, 2, 3 })
        {
            var entry = Note($"Day {day}", new DateTimeOffset(2026, 10, day, 12, 0, 0, TimeSpan.Zero));
            await service.SetForEntryAsync(entry,
            [
                MeasureValue.OfOwn(nodes.Id, day),
                MeasureValue.Of(MeasureKind.RootLength, day),
                MeasureValue.OfOwn(aerial.Id, day),
                MeasureValue.Of(MeasureKind.Height, day),
                MeasureValue.Of(MeasureKind.Leaves, day)
            ]);
        }

        var names = (await service.CurvesForAsync(plant)).Select(c => c.Name);

        Assert.Equal(["Height", "Leaves", "Root length", "Aerial root", "Nodes"], names);
    }

    [Fact]
    public void The_newest_of_two_for_the_same_entry_and_kind_counts_and_null_kinds_are_ignored()
    {
        var entryId = Guid.NewGuid();
        Measurement Make(Guid entry, DateTimeOffset at, decimal value, DateTimeOffset updated, MeasureKind? kind = MeasureKind.Height) => new()
        {
            SubjectType = SubjectType.Plant, SubjectId = plant, EntryId = entry, MeasuredAt = at, Kind = kind, Value = value, UpdatedAt = updated
        };
        var day1 = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
        var all = new List<Measurement>
        {
            Make(entryId, day1, 10, Now.AddHours(-2)),
            Make(entryId, day1, 11, Now),
            Make(Guid.NewGuid(), day1.AddDays(2), 13, Now),
            Make(Guid.NewGuid(), day1.AddDays(4), 15, Now),
            Make(Guid.NewGuid(), day1.AddDays(6), 99, Now, kind: null),
            Make(Guid.NewGuid(), day1.AddDays(7), 99, Now, kind: null)
        };

        var curve = Assert.Single(MeasurementService.Curves(all, d => DateOnly.FromDateTime(d.UtcDateTime)));

        Assert.Equal([11m, 13m, 15m], curve.Points.Select(p => p.Value));
    }

    [Fact]
    public async Task A_removed_kind_keeps_its_curve_under_the_name_it_had()
    {
        var nodes = await service.AddOwnKindAsync("Nodes", false);
        foreach (var day in new[] { 1, 2, 3 })
        {
            var entry = Note($"Day {day}", new DateTimeOffset(2026, 10, day, 12, 0, 0, TimeSpan.Zero));
            await service.SetForEntryAsync(entry, [MeasureValue.OfOwn(nodes.Id, day)]);
        }

        await service.RenameOwnKindAsync(nodes.Id, "Knuder");
        Assert.Equal("Knuder", Assert.Single(await service.CurvesForAsync(plant)).Name);

        await service.RemoveOwnKindAsync(nodes.Id);
        var curve = Assert.Single(await service.CurvesForAsync(plant));
        Assert.Equal("Nodes", curve.Name);
        Assert.False(curve.IsLength);
    }

    [Fact]
    public async Task Measurements_of_a_deleted_entry_are_left_out_of_the_curve()
    {
        await Measure(1, MeasureKind.Height, 10);
        await Measure(2, MeasureKind.Height, 11);
        await Measure(3, MeasureKind.Height, 12);
        timeline.Entries.Last().DeletedAt = Now;

        Assert.Empty(await service.CurvesForAsync(plant));
    }

    // Own kinds

    [Fact]
    public async Task The_same_name_twice_is_one_own_kind_and_a_removed_one_comes_back()
    {
        var first = await service.AddOwnKindAsync("Nodes", false);
        var second = await service.AddOwnKindAsync(" nodes ", true);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(OwnMeasureKind.IdFor("Nodes"), first.Id);
        Assert.Single(await service.GetOwnKindsAsync());

        await service.RemoveOwnKindAsync(first.Id);
        Assert.Empty(await service.GetOwnKindsAsync());

        var back = await service.AddOwnKindAsync("Nodes", true);
        Assert.Equal(first.Id, back.Id);
        Assert.True(back.IsLength);
        Assert.False(back.IsDeleted);
    }

    [Fact]
    public async Task An_own_kind_can_not_take_the_name_of_a_built_in_one_or_of_another_kind()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AddOwnKindAsync("height", true));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AddOwnKindAsync("  ", true));

        await service.AddOwnKindAsync("Nodes", false);
        var other = await service.AddOwnKindAsync("Aerial root", true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RenameOwnKindAsync(other.Id, "nodes"));
    }

    [Fact]
    public async Task Renaming_a_kind_leaves_the_measurements_with_the_name_they_have_until_they_are_saved_again()
    {
        var nodes = await service.AddOwnKindAsync("Nodes", false);
        var entry = Note();
        await service.SetForEntryAsync(entry, [MeasureValue.OfOwn(nodes.Id, 3)]);

        await service.RenameOwnKindAsync(nodes.Id, "Knuder");
        Assert.Equal("Nodes", Live(entry.Id).Single().OwnKindName);

        await service.SetForEntryAsync(entry, [MeasureValue.OfOwn(nodes.Id, 4)]);
        Assert.Equal("Knuder", Live(entry.Id).Single().OwnKindName);
    }

    [Fact]
    public async Task A_measurement_of_a_removed_kind_keeps_showing_with_its_copied_name()
    {
        var nodes = await service.AddOwnKindAsync("Nodes", false);
        var entry = Note();
        await service.SetForEntryAsync(entry, [MeasureValue.OfOwn(nodes.Id, 3)]);
        await service.RemoveOwnKindAsync(nodes.Id);

        var shown = (await service.ForEntriesAsync([entry.Id]))[entry.Id].Single();
        Assert.Equal("Nodes 3", MeasureText.Phrase(shown));

        // Saving the entry again keeps the value of a kind that is gone
        await service.SetForEntryAsync(entry, [MeasureValue.OfOwn(nodes.Id, 5)]);
        Assert.Equal("Nodes 5", MeasureText.Phrase(Live(entry.Id).Single()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetForEntryAsync(Note("x"), [MeasureValue.OfOwn(nodes.Id, 1)]));
    }

    [Fact]
    public async Task Correcting_an_entry_keeps_a_value_of_a_removed_kind()
    {
        var nodes = await service.AddOwnKindAsync("Nodes", false);
        var entry = Note();
        await service.SetForEntryAsync(entry, [MeasureValue.OfOwn(nodes.Id, 3), MeasureValue.Of(MeasureKind.Height, 30)]);
        await service.RemoveOwnKindAsync(nodes.Id);

        // What the correction sends: the removed kind's value as it was, and a changed height
        var values = new[] { MeasureValue.OfOwn(nodes.Id, 3), MeasureValue.Of(MeasureKind.Height, 31) };
        await service.CheckAsync(entry, values);
        await timelineService.CorrectAsync(entry, "Looking better", new DateOnly(2026, 11, 10), keepsMeasurements: true);
        await service.SetForEntryAsync(entry, values);

        var live = Live(entry.Id);
        Assert.Equal(3m, live.Single(m => m.OwnKindId == nodes.Id).Value);
        Assert.Equal(31m, live.Single(m => m.Kind == MeasureKind.Height).Value);
    }

    [Fact]
    public async Task A_note_that_becomes_measured_no_longer_comes_back()
    {
        var entry = Note();
        entry.ComesBackOn = new DateOnly(2027, 1, 1);
        await service.SetForEntryAsync(entry, [MeasureValue.Of(MeasureKind.Height, 30)]);

        await timelineService.CorrectAsync(entry, "", new DateOnly(2026, 11, 10), keepsMeasurements: true);

        Assert.Equal(TimelineEvent.Measured, entry.Event);
        Assert.Null(entry.ComesBackOn);
        Assert.Null(entry.CameBackDoneOn);
    }

    [Fact]
    public async Task A_plant_potted_up_from_a_propagation_joins_the_propagations_measurements_up_to_the_pot_up_day()
    {
        var propagation = Guid.NewGuid();
        async Task Cutting(int day, decimal height)
        {
            var entry = new TimelineEntry
            {
                SubjectType = SubjectType.Propagation, SubjectId = propagation, Kind = TimelineKind.Note, Text = "x",
                OccurredAt = new DateTimeOffset(2026, 10, day, 12, 0, 0, TimeSpan.Zero)
            };
            timeline.Entries.Add(entry);
            await service.SetForEntryAsync(entry, [MeasureValue.Of(MeasureKind.Height, height)]);
        }
        await Cutting(1, 5);
        await Cutting(5, 8);
        await Cutting(25, 99);
        await Measure(10, MeasureKind.Height, 12);

        Assert.Empty(await service.CurvesForAsync(plant));

        var curve = Assert.Single(await service.CurvesForAsync(plant, propagation, new DateOnly(2026, 10, 9)));
        Assert.Equal([5m, 8m, 12m], curve.Points.Select(p => p.Value));
    }

    // Text and units

    [Fact]
    public void The_phrases_read_like_the_summary()
    {
        Measurement Make(MeasureKind? kind, decimal value, string? own = null, bool? isLength = null) =>
            new() { Kind = kind, Value = value, OwnKindName = own, OwnKindIsLength = isLength };

        Assert.Equal("34 cm tall", MeasureText.Phrase(Make(MeasureKind.Height, 34)));
        Assert.Equal("12.5 cm wide", MeasureText.Phrase(Make(MeasureKind.Width, 12.5m)));
        Assert.Equal("1 leaf", MeasureText.Phrase(Make(MeasureKind.Leaves, 1)));
        Assert.Equal("7 leaves", MeasureText.Phrase(Make(MeasureKind.Leaves, 7)));
        Assert.Equal("3 cm of roots", MeasureText.Phrase(Make(MeasureKind.RootLength, 3)));
        Assert.Equal("Nodes 3", MeasureText.Phrase(Make(null, 3, "Nodes", false)));
        Assert.Equal("Aerial root 4 cm", MeasureText.Phrase(Make(null, 4, "Aerial root", true)));
    }

    [Fact]
    public async Task Lengths_typed_in_inches_are_stored_in_cm_and_shown_back_the_same()
    {
        Lengths.Unit = LengthUnit.Inches;
        var entry = Note();

        await service.SetForEntryAsync(entry, [MeasureValue.Of(MeasureKind.Height, MeasureText.FromInput(10)), MeasureValue.Of(MeasureKind.Width, MeasureText.FromInput(4.5m))]);

        var height = Live(entry.Id).Single(m => m.Kind == MeasureKind.Height);
        var width = Live(entry.Id).Single(m => m.Kind == MeasureKind.Width);
        Assert.Equal(25.4m, height.Value);
        Assert.Equal(11.4m, width.Value);
        Assert.Equal(10m, MeasureText.ToInput(height.Value));
        Assert.Equal(4.5m, MeasureText.ToInput(width.Value));
        Assert.Equal("10 " + Lengths.UnitName + " tall", MeasureText.Phrase(height, CultureInfo.InvariantCulture));
    }

    // Storage

    [Fact]
    public void A_kind_this_version_does_not_know_reads_as_no_kind()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var json = JsonSerializer.Serialize(Valid(), options).Replace("\"Height\"", "\"Depth\"");

        var read = JsonSerializer.Deserialize<Measurement>(json, options)!;

        Assert.Null(read.Kind);
        Assert.False(read.HasKind);
        Assert.Equal("\"Height\"", JsonSerializer.Serialize(Valid().Kind, options));
        Assert.Contains("\"kind\":\"Height\"", JsonSerializer.Serialize(Valid(), options));
    }

    [Fact]
    public void An_older_version_reads_the_measured_event_as_nothing()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var json = JsonSerializer.Serialize(new TimelineEntry { Kind = TimelineKind.Change, Event = TimelineEvent.Measured }, options);

        Assert.Contains("\"event\":\"Measured\"", json);
        Assert.Equal(TimelineEvent.Measured, JsonSerializer.Deserialize<TimelineEntry>(json, options)!.Event);
        Assert.Null(JsonSerializer.Deserialize<TimelineEntry>(json.Replace("Measured", "Weighed"), options)!.Event);
    }
}
