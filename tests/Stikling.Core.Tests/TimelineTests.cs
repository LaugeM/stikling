using Stikling.Core.Models;
using Stikling.Core.Timeline;

namespace Stikling.Core.Tests;

public class PlantChangesTests
{
    private static string Label(Enum value) => value.ToString();

    private static readonly Plant Original = new()
    {
        Nickname = "Big alocasia",
        Location = "Living room",
        Medium = GrowingMedium.Soil
    };

    [Fact]
    public void No_changes_gives_no_entries()
    {
        var edited = Original.Copy();
        edited.Notes = "Only the notes changed";
        edited.Location = " living ROOM ";

        Assert.Empty(PlantChanges.Describe(Original, edited, Label));
    }

    [Fact]
    public void Moving_to_semi_hydro_is_described()
    {
        var edited = Original.Copy();
        edited.Medium = GrowingMedium.Leca;
        edited.InnerPotId = Guid.NewGuid();

        Assert.Equal(
            ["Now grows in Leca (was Soil)", "Potted into Lechuza pot"],
            PlantChanges.Describe(Original, edited, Label, _ => "Lechuza pot"));
    }

    [Theory]
    [InlineData("Living room", "Bedroom", "Moved from Living room to Bedroom")]
    [InlineData(null, "Kitchen", "Placed in Kitchen")]
    [InlineData("Kitchen", "  ", "Removed from Kitchen")]
    public void Location_changes_are_described(string? from, string? to, string expected)
    {
        var before = Original.Copy();
        before.Location = from;
        var after = Original.Copy();
        after.Location = to;

        Assert.Equal([expected], PlantChanges.Describe(before, after, Label));
    }

    [Fact]
    public void Status_change_uses_the_label_function()
    {
        var edited = Original.Copy();
        edited.Status = PlantStatus.GivenAway;

        var changes = PlantChanges.Describe(Original, edited, v => v is PlantStatus.GivenAway ? "Given away" : v.ToString());

        Assert.Equal(["Status: Given away (was Active)"], changes);
    }

    [Fact]
    public void Copy_is_independent_of_the_original()
    {
        var copy = Original.Copy();
        copy.Nickname = "Changed";

        Assert.Equal("Big alocasia", Original.Nickname);
        Assert.Equal(Original.Id, copy.Id);
    }

    [Fact]
    public void Potting_into_a_pot_from_the_library_is_written_down()
    {
        var pot = Guid.NewGuid();
        var edited = Original.Copy();
        edited.InnerPotId = pot;

        Assert.Equal(
            ["Potted into Clear nursery pot, 13 cm"],
            PlantChanges.Describe(Original, edited, Label, _ => "Clear nursery pot, 13 cm"));
    }

    [Fact]
    public void A_pot_nobody_can_name_is_left_unsaid()
    {
        var edited = Original.Copy();
        edited.InnerPotId = Guid.NewGuid();

        Assert.Empty(PlantChanges.Describe(Original, edited, Label));
    }

    [Fact]
    public void Taking_a_plant_out_of_its_outer_pot_is_written_down()
    {
        var before = Original.Copy();
        before.OuterPotId = Guid.NewGuid();
        before.WaterInOuterPot = true;
        var edited = before.Copy();
        edited.OuterPotId = null;
        edited.WaterInOuterPot = false;

        Assert.Equal(
            ["No longer in an outer pot", "No longer watered in the outer pot"],
            PlantChanges.Describe(before, edited, Label));
    }

    [Fact]
    public void Moving_a_plant_into_a_saved_mix_is_written_down()
    {
        var edited = Original.Copy();
        edited.SoilMixId = Guid.NewGuid();

        Assert.Equal(
            ["Now in Chunky soil"],
            PlantChanges.Describe(Original, edited, Label, mixName: _ => "Chunky soil"));
    }
}

public class PropagationChangesTests
{
    private static string Label(Enum value) => value.ToString();

    [Fact]
    public void Clearing_the_container_is_not_reported()
    {
        var before = new Propagation { Genus = "Alocasia", Container = "Humidity box" };
        var edited = before.Copy();
        edited.Container = null;

        Assert.Empty(PropagationChanges.Describe(before, edited, Label));
    }
}

public class TimelineOrderTests
{
    private static TimelineEntry At(string time, string? text = null, string? created = null) => new()
    {
        OccurredAt = DateTimeOffset.Parse(time),
        CreatedAt = DateTimeOffset.Parse(created ?? time),
        Text = text
    };

    [Fact]
    public void Newest_first_and_ties_broken_by_recording_time()
    {
        var older = At("2026-09-01T10:00:00Z", "older");
        var first = At("2026-09-05T10:00:00Z", "first", created: "2026-09-05T10:00:00Z");
        var second = At("2026-09-05T10:00:00Z", "second", created: "2026-09-05T10:00:05Z");

        var ordered = TimelineOrder.NewestFirst([older, first, second]);

        Assert.Equal(["second", "first", "older"], ordered.Select(e => e.Text));
    }

    [Fact]
    public void Deleted_entries_are_left_out()
    {
        var kept = At("2026-09-01T10:00:00Z", "kept");
        var deleted = At("2026-09-02T10:00:00Z", "deleted");
        deleted.DeletedAt = DateTimeOffset.UnixEpoch;

        Assert.Equal(["kept"], TimelineOrder.NewestFirst([kept, deleted]).Select(e => e.Text));
    }

    [Fact]
    public void Entries_are_grouped_by_local_day()
    {
        // 23:30 UTC on the 1st is already the 2nd in Denmark (UTC+2 in summer)
        var lateEvening = At("2026-09-01T23:30:00Z", "late");
        var morning = At("2026-09-02T08:00:00Z", "morning");
        var earlier = At("2026-08-30T12:00:00Z", "earlier");

        var days = TimelineOrder.ByDay([lateEvening, morning, earlier], TimeSpan.FromHours(2));

        Assert.Equal([new DateOnly(2026, 9, 2), new DateOnly(2026, 8, 30)], days.Select(d => d.Day));
        Assert.Equal(["morning", "late"], days[0].Entries.Select(e => e.Text));
    }
}

public class TimelineCorrectionTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-26T15:00:00Z");

    private readonly FakeTimelineRepository timeline = new();
    private readonly FakePhotoRepository photos = new();
    private readonly TimelineService service;

    public TimelineCorrectionTests() => service = new TimelineService(timeline, photos, new FixedTime(Now));

    private TimelineEntry Note(string? text, params Photo[] withPhotos)
    {
        foreach (var photo in withPhotos)
            photos.Photos[photo.Id] = photo;

        var entry = new TimelineEntry
        {
            Kind = text is null ? TimelineKind.Photo : TimelineKind.Note,
            OccurredAt = DateTimeOffset.Parse("2026-09-20T09:30:00Z"),
            Text = text,
            PhotoIds = withPhotos.Select(p => p.Id).ToList()
        };
        timeline.Entries.Add(entry);
        return entry;
    }

    private static Photo Photo() => new() { TakenAt = DateTimeOffset.Parse("2026-09-20T09:30:00Z") };

    [Fact]
    public async Task Correcting_the_text_keeps_the_version_it_replaces()
    {
        var entry = Note("Roots 2 cm");

        Assert.True(await service.CorrectAsync(entry, "Roots 3 cm", new DateOnly(2026, 9, 20)));

        Assert.Equal("Roots 3 cm", entry.Text);
        Assert.Equal(DateTimeOffset.Parse("2026-09-20T09:30:00Z"), entry.OccurredAt);
        var edit = Assert.Single(entry.Edits);
        Assert.Equal("Roots 2 cm", edit.Text);
        Assert.Equal(DateTimeOffset.Parse("2026-09-20T09:30:00Z"), edit.OccurredAt);
        Assert.Equal(Now, edit.ReplacedAt);
        Assert.Equal([entry.Id], timeline.Updated);
    }

    [Fact]
    public async Task Every_correction_is_kept_in_order()
    {
        var entry = Note("First");

        await service.CorrectAsync(entry, "Second", new DateOnly(2026, 9, 20));
        await service.CorrectAsync(entry, "Third", new DateOnly(2026, 9, 20));

        Assert.Equal(["First", "Second"], entry.Edits.Select(e => e.Text));
        Assert.Equal("Third", entry.Text);
    }

    [Fact]
    public async Task An_earlier_day_is_recorded_at_noon_and_the_photos_move_with_it()
    {
        var photo = Photo();
        var entry = Note(null, photo);

        await service.CorrectAsync(entry, null, new DateOnly(2026, 9, 12));

        Assert.Equal(DateTimeOffset.Parse("2026-09-12T12:00:00Z"), entry.OccurredAt);
        Assert.Equal(entry.OccurredAt, photo.TakenAt);
    }

    [Fact]
    public async Task Moving_an_entry_to_today_records_it_now()
    {
        var entry = Note("Forgot to add this today");

        await service.CorrectAsync(entry, entry.Text, new DateOnly(2026, 9, 26));

        Assert.Equal(Now, entry.OccurredAt);
    }

    [Fact]
    public async Task Changing_only_the_text_leaves_the_photos_dates_alone()
    {
        var photo = Photo();
        photo.TakenAt = DateTimeOffset.Parse("2026-09-20T08:00:00Z");
        var entry = Note("New leaf", photo);

        await service.CorrectAsync(entry, "Two new leaves", new DateOnly(2026, 9, 20));

        Assert.Equal(DateTimeOffset.Parse("2026-09-20T08:00:00Z"), photo.TakenAt);
    }

    [Fact]
    public async Task Nothing_changed_saves_nothing()
    {
        var entry = Note("Roots 2 cm");

        Assert.False(await service.CorrectAsync(entry, "  Roots 2 cm ", new DateOnly(2026, 9, 20)));

        Assert.Empty(entry.Edits);
        Assert.Empty(timeline.Updated);
    }

    [Fact]
    public async Task Clearing_the_text_of_a_note_with_photos_leaves_a_photo_entry()
    {
        var entry = Note("Blurry", Photo());

        await service.CorrectAsync(entry, " ", new DateOnly(2026, 9, 20));

        Assert.Null(entry.Text);
        Assert.Equal(TimelineKind.Photo, entry.Kind);
    }

    [Fact]
    public async Task Writing_on_a_photo_entry_makes_it_a_note()
    {
        var entry = Note(null, Photo());

        await service.CorrectAsync(entry, "First flower", new DateOnly(2026, 9, 20));

        Assert.Equal(TimelineKind.Note, entry.Kind);
    }

    [Fact]
    public async Task A_note_without_photos_cannot_be_left_empty()
    {
        var entry = Note("Roots 2 cm");

        await Assert.ThrowsAsync<ArgumentException>(() => service.CorrectAsync(entry, "", new DateOnly(2026, 9, 20)));
        Assert.Equal("Roots 2 cm", entry.Text);
    }

    [Fact]
    public async Task Deleted_photos_do_not_count_as_something_left_on_the_entry()
    {
        var photo = Photo();
        var entry = Note("Blurry", photo);
        await photos.DeleteAsync(photo.Id);

        await Assert.ThrowsAsync<ArgumentException>(() => service.CorrectAsync(entry, null, new DateOnly(2026, 9, 20)));
    }

    [Fact]
    public async Task An_entry_cannot_be_moved_into_the_future()
    {
        var entry = Note("Roots 2 cm");

        await Assert.ThrowsAsync<ArgumentException>(() => service.CorrectAsync(entry, entry.Text, new DateOnly(2026, 9, 27)));
    }

    [Theory]
    [InlineData(TimelineKind.Created)]
    [InlineData(TimelineKind.Change)]
    [InlineData(TimelineKind.Propagated)]
    public async Task Automatic_entries_are_fixed_where_they_come_from(TimelineKind kind)
    {
        var entry = Note("Moved from Kitchen to Bedroom");
        entry.Kind = kind;

        Assert.False(TimelineService.CanCorrect(entry));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CorrectAsync(entry, "Moved", new DateOnly(2026, 9, 20)));
    }
}
