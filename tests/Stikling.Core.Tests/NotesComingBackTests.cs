using System.Text.Json;
using Stikling.Core.Models;
using Stikling.Core.Timeline;

namespace Stikling.Core.Tests;

public class NotesComingBackTests
{
    private static readonly DateOnly Today = new(2026, 10, 28);
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeTimelineRepository timeline = new();
    private readonly TimelineService service;

    public NotesComingBackTests() => service = new TimelineService(timeline, new FakePhotoRepository(), new FixedTime(Now));

    private static Plant Plant(string name = "Monstera") => new() { Nickname = name };

    private static TimelineEntry Note(Guid subject, DateOnly? comesBackOn, SubjectType type = SubjectType.Plant) =>
        new()
        {
            Kind = TimelineKind.Note, SubjectType = type, SubjectId = subject, Text = "Repot",
            OccurredAt = new DateTimeOffset(2025, 10, 28, 9, 0, 0, TimeSpan.Zero), ComesBackOn = comesBackOn
        };

    private static IReadOnlyList<NoteComingBack> For(IEnumerable<TimelineEntry> entries, IEnumerable<Plant> plants,
        IEnumerable<Propagation>? propagations = null) =>
        NotesComingBack.For(entries, plants, propagations ?? [], Today);

    [Fact]
    public void A_note_is_due_on_its_day_and_after()
    {
        var plant = Plant();
        var onTheDay = Note(plant.Id, Today);
        var overdue = Note(plant.Id, Today.AddDays(-3));

        var due = For([onTheDay, overdue], [plant]);

        Assert.Equal([overdue.Id, onTheDay.Id], due.Select(n => n.Entry.Id));
        Assert.All(due, n => Assert.Equal("Monstera", n.Name));
    }

    [Fact]
    public void A_note_is_not_due_before_its_day_or_without_one()
    {
        var plant = Plant();

        Assert.Empty(For([Note(plant.Id, Today.AddDays(1)), Note(plant.Id, null)], [plant]));
    }

    [Fact]
    public void A_note_ticked_done_is_hidden()
    {
        var plant = Plant();
        var entry = Note(plant.Id, Today);
        entry.CameBackDoneOn = Today;

        Assert.Empty(For([entry], [plant]));
    }

    [Fact]
    public void Only_notes_come_back()
    {
        var plant = Plant();
        var entry = Note(plant.Id, Today);
        entry.Kind = TimelineKind.Photo;

        Assert.Empty(For([entry], [plant]));
    }

    [Fact]
    public void Deleted_notes_are_left_out()
    {
        var plant = Plant();
        var entry = Note(plant.Id, Today);
        entry.DeletedAt = Now;

        Assert.Empty(For([entry], [plant]));
    }

    [Fact]
    public void Notes_about_plants_that_are_gone_are_left_out()
    {
        var deleted = Plant("Deleted");
        deleted.DeletedAt = Now;
        var died = Plant("Died");
        died.Status = PlantStatus.Died;
        var kept = Plant("Kept");
        var unknown = Guid.NewGuid();

        var due = For(
            [Note(deleted.Id, Today), Note(died.Id, Today), Note(unknown, Today), Note(kept.Id, Today)],
            [deleted, died, kept]);

        Assert.Equal("Kept", Assert.Single(due).Name);
    }

    [Fact]
    public void A_propagation_note_comes_back_only_while_it_is_going()
    {
        var going = new Propagation { Nickname = "Going" };
        var done = new Propagation { Nickname = "Done", Stage = PropagationStage.Done };
        var failed = new Propagation { Nickname = "Failed", Stage = PropagationStage.Failed };
        var deleted = new Propagation { Nickname = "Deleted", DeletedAt = Now };

        var due = For(
            new[] { going, done, failed, deleted }.Select(p => Note(p.Id, Today, SubjectType.Propagation)),
            Array.Empty<Plant>(), [going, done, failed, deleted]);

        Assert.Equal(going.Id, Assert.Single(due).Entry.SubjectId);
    }

    [Fact]
    public void A_plant_id_does_not_match_a_propagation_note()
    {
        var plant = Plant();

        Assert.Empty(For([Note(plant.Id, Today, SubjectType.Propagation)], [plant]));
    }

    [Fact]
    public void Same_day_next_year_moves_29_February_to_28_February()
    {
        Assert.Equal(new DateOnly(2025, 2, 28), NotesComingBack.SameDayNextYear(new DateOnly(2024, 2, 29)));
        Assert.Equal(new DateOnly(2027, 5, 14), NotesComingBack.SameDayNextYear(new DateOnly(2026, 5, 14)));
    }

    [Fact]
    public async Task Again_next_year_moves_the_day_a_year_and_keeps_it_not_done()
    {
        var plant = Plant();
        var entry = Note(plant.Id, Today);

        await service.BringBackAgainAsync(entry);

        Assert.Equal(new DateOnly(2027, 10, 28), entry.ComesBackOn);
        Assert.Null(entry.CameBackDoneOn);
        Assert.Empty(For([entry], [plant]));
        Assert.Contains(entry.Id, timeline.Updated);
    }

    [Fact]
    public async Task Again_next_year_on_a_note_long_overdue_lands_after_today()
    {
        var entry = Note(Guid.NewGuid(), new DateOnly(2024, 2, 29));

        await service.BringBackAgainAsync(entry);

        Assert.Equal(new DateOnly(2027, 2, 28), entry.ComesBackOn);
    }

    [Fact]
    public async Task Done_sets_the_day_and_hides_the_note()
    {
        var plant = Plant();
        var entry = Note(plant.Id, Today);

        await service.MarkCameBackDoneAsync(entry);

        Assert.Equal(Today, entry.CameBackDoneOn);
        Assert.Equal(Today, entry.ComesBackOn);
        Assert.Empty(For([entry], [plant]));
    }

    [Fact]
    public async Task Setting_a_new_day_clears_done_and_does_not_add_an_edit()
    {
        var entry = Note(Guid.NewGuid(), Today);
        entry.CameBackDoneOn = Today;

        await service.SetComesBackAsync(entry, new DateOnly(2027, 1, 1));

        Assert.Equal(new DateOnly(2027, 1, 1), entry.ComesBackOn);
        Assert.Null(entry.CameBackDoneOn);
        Assert.Empty(entry.Edits);
    }

    [Fact]
    public async Task A_day_that_is_not_after_today_is_refused()
    {
        var entry = Note(Guid.NewGuid(), null);

        await Assert.ThrowsAsync<ArgumentException>(() => service.SetComesBackAsync(entry, Today));

        Assert.Null(entry.ComesBackOn);
    }

    [Fact]
    public async Task The_day_can_be_removed()
    {
        var entry = Note(Guid.NewGuid(), new DateOnly(2027, 1, 1));

        await service.SetComesBackAsync(entry, null);

        Assert.Null(entry.ComesBackOn);
    }

    [Fact]
    public async Task Only_a_note_can_have_a_day()
    {
        var entry = Note(Guid.NewGuid(), null);
        entry.Kind = TimelineKind.Photo;

        await service.SetComesBackAsync(entry, new DateOnly(2027, 1, 1));

        Assert.Null(entry.ComesBackOn);
    }

    [Fact]
    public async Task The_repository_returns_only_waiting_notes()
    {
        var waiting = Note(Guid.NewGuid(), new DateOnly(2027, 1, 1));
        var done = Note(Guid.NewGuid(), new DateOnly(2027, 1, 1));
        done.CameBackDoneOn = Today;
        timeline.Entries.AddRange([waiting, done, Note(Guid.NewGuid(), null)]);

        Assert.Equal(waiting.Id, Assert.Single(await timeline.GetComingBackAsync()).Id);
    }

    [Fact]
    public void The_choice_works_out_the_day_from_the_notes_day()
    {
        var noteDay = new DateOnly(2026, 5, 14);

        Assert.Null(new BringBackChoice().Resolve(noteDay));
        Assert.Equal(new DateOnly(2027, 5, 14), new BringBackChoice { Mode = BringBackMode.SameDayNextYear }.Resolve(noteDay));
        Assert.Equal(new DateOnly(2027, 1, 1),
            new BringBackChoice { Mode = BringBackMode.OnADay, Picked = new DateOnly(2027, 1, 1) }.Resolve(noteDay));
    }

    [Fact]
    public void The_choice_is_invalid_for_a_day_that_is_not_after_today()
    {
        Assert.True(new BringBackChoice().IsValid(Today, Today));
        Assert.True(new BringBackChoice { Mode = BringBackMode.SameDayNextYear }.IsValid(Today, Today));
        Assert.False(new BringBackChoice { Mode = BringBackMode.SameDayNextYear }.IsValid(Today.AddYears(-2), Today));
        Assert.False(new BringBackChoice { Mode = BringBackMode.OnADay, Picked = Today }.IsValid(Today, Today));
        Assert.False(new BringBackChoice { Mode = BringBackMode.OnADay }.IsValid(Today, Today));
    }

    [Fact]
    public void Older_entries_read_both_days_as_null_and_the_days_round_trip()
    {
        var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        var old = JsonSerializer.Deserialize<TimelineEntry>("""{"kind":"Note","text":"x"}""", web)!;
        Assert.Null(old.ComesBackOn);
        Assert.Null(old.CameBackDoneOn);

        var entry = Note(Guid.NewGuid(), new DateOnly(2027, 5, 14));
        entry.CameBackDoneOn = Today;
        var back = JsonSerializer.Deserialize<TimelineEntry>(JsonSerializer.Serialize(entry, web), web)!;
        Assert.Equal(new DateOnly(2027, 5, 14), back.ComesBackOn);
        Assert.Equal(Today, back.CameBackDoneOn);
    }
}
