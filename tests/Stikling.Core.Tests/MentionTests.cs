using System.Text.Json;
using Stikling.Core.Models;
using Stikling.Core.Timeline;

namespace Stikling.Core.Tests;

public class MentionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private readonly FakeTimelineRepository timeline = new();
    private readonly TimelineService service;

    public MentionTests() =>
        service = new TimelineService(timeline, new FakePhotoRepository(), new FakeMeasurementRepository(), new FixedTime(Now));

    private static Mention Plant(string name) => new() { Kind = SubjectType.Plant, Id = Guid.NewGuid(), Name = name };

    private async Task<TimelineEntry> NoteAsync(string text, params Mention[] mentions)
    {
        var entry = new TimelineEntry
        {
            SubjectType = SubjectType.Plant,
            SubjectId = Guid.NewGuid(),
            Kind = TimelineKind.Note,
            OccurredAt = Now,
            Text = text,
            Mentions = mentions.Length == 0 ? null : [.. mentions]
        };
        await timeline.AddAsync(entry);
        return entry;
    }

    private static readonly DateOnly Today = new(2026, 11, 17);

    [Fact]
    public void Keep_drops_a_mention_whose_name_is_gone_from_the_text()
    {
        var monstera = Plant("Big Monstera");
        var pothos = Plant("Pothos");

        var kept = Mentions.Keep("Took a cutting from @Big Monstera, not the pot", [monstera, pothos]);

        Assert.Equal([monstera], kept);
    }

    [Fact]
    public void Keep_is_null_when_no_name_is_left()
    {
        Assert.Null(Mentions.Keep("Nothing linked now", [Plant("Pothos")]));
        Assert.Null(Mentions.Keep(null, [Plant("Pothos")]));
        Assert.Null(Mentions.Keep("@Pothos", null));
    }

    [Fact]
    public void A_name_must_end_where_the_word_ends()
    {
        var fern = Plant("Fern");

        Assert.Null(Mentions.Keep("@Fernando waters it", [fern]));
        Assert.Equal([fern], Mentions.Keep("Look at @Fern.", [fern]));
    }

    [Fact]
    public void An_at_inside_a_word_is_not_a_mention()
    {
        Assert.Null(Mentions.Keep("mail anna@Pothos", [Plant("Pothos")]));
    }

    [Fact]
    public void Split_gives_the_text_back_with_each_mention_on_its_own()
    {
        var monstera = Plant("Big Monstera");
        var text = "Cut @Big Monstera today and @Big Monstera again";

        var parts = Mentions.Split(text, [monstera]);

        Assert.Equal(text, string.Concat(parts.Select(p => p.Text)));
        Assert.Equal(2, parts.Count(p => p.Mention == monstera));
        Assert.All(parts.Where(p => p.Mention is not null), p => Assert.Equal("@Big Monstera", p.Text));
    }

    [Fact]
    public void The_longest_name_wins_where_two_start_in_the_same_place()
    {
        var short1 = Plant("Hoya");
        var long1 = Plant("Hoya carnosa");

        var parts = Mentions.Split("@Hoya carnosa looks well", [short1, long1]);

        Assert.Equal(long1, parts[0].Mention);
    }

    [Fact]
    public void Picking_the_same_name_again_replaces_the_mention()
    {
        var first = Plant("Basil");
        var second = Plant("Basil");

        var picked = Mentions.With([first], second);

        Assert.Equal([second], picked);
    }

    [Fact]
    public async Task Correcting_a_note_keeps_only_the_mentions_still_in_the_text()
    {
        var monstera = Plant("Big Monstera");
        var pothos = Plant("Pothos");
        var entry = await NoteAsync("Both @Big Monstera and @Pothos need repotting", monstera, pothos);

        await service.CorrectAsync(entry, "Only @Pothos needs repotting", Today);

        Assert.Equal([pothos], entry.Mentions);
    }

    [Fact]
    public async Task Correcting_a_note_keeps_mentions_when_only_the_day_changes()
    {
        var monstera = Plant("Big Monstera");
        var entry = await NoteAsync("Cut @Big Monstera", monstera);

        await service.CorrectAsync(entry, "Cut @Big Monstera", Today.AddDays(-1));

        Assert.Equal([monstera], entry.Mentions);
    }

    [Fact]
    public async Task Correcting_a_note_can_add_a_mention_picked_while_editing()
    {
        var entry = await NoteAsync("Moved it");
        var monstera = Plant("Big Monstera");

        Assert.True(await service.CorrectAsync(entry, "Moved it next to @Big Monstera", Today, mentions: [monstera]));

        Assert.Equal([monstera], entry.Mentions);
    }

    [Fact]
    public async Task Taking_the_name_out_with_the_same_text_otherwise_drops_the_mention()
    {
        var monstera = Plant("Big Monstera");
        var entry = await NoteAsync("Cut @Big Monstera", monstera);

        await service.CorrectAsync(entry, "Cut the big one", Today);

        Assert.Null(entry.Mentions);
    }

    [Fact]
    public void Typing_finds_the_at_before_the_caret()
    {
        var typing = Mentions.Typing("Next to @Mon", 12);

        Assert.Equal((8, "Mon"), typing);
        Assert.Null(Mentions.Typing("Next to @Mon", 7));
        Assert.Null(Mentions.Typing("mail me@home", 12));
        Assert.Null(Mentions.Typing("@Mon\nand more", 13));
    }

    [Fact]
    public void Choose_lists_names_starting_with_what_was_typed_first()
    {
        var all = new[]
        {
            new MentionChoice(SubjectType.Plant, Guid.NewGuid(), "Big Monstera", null),
            new MentionChoice(SubjectType.Plant, Guid.NewGuid(), "Monstera adansonii", null),
            new MentionChoice(SubjectType.Pot, Guid.NewGuid(), "Terracotta", null),
            new MentionChoice(SubjectType.Propagation, Guid.NewGuid(), "Pothos", null)
        };

        var chosen = Mentions.Choose("mon", all);

        Assert.Equal(["Monstera adansonii", "Big Monstera"], chosen.Select(c => c.Name));
        Assert.Equal(4, Mentions.Choose("", all).Count);
    }

    [Fact]
    public void A_mention_round_trips_and_older_entries_read_as_none()
    {
        var mention = Plant("Pothos");
        var json = JsonSerializer.Serialize(new TimelineEntry { Text = "@Pothos", Mentions = [mention] }, Web);
        Assert.Contains("\"kind\":\"Plant\"", json);

        var back = JsonSerializer.Deserialize<TimelineEntry>(json, Web)!;
        Assert.Equal([mention], back.Mentions);

        Assert.Null(JsonSerializer.Deserialize<TimelineEntry>("""{"text":"Old note"}""", Web)!.Mentions);
    }

    [Fact]
    public async Task Photos_added_with_a_note_keep_the_mentions_in_the_note()
    {
        var monstera = Plant("Big Monstera");
        var photo = new Photo { SubjectType = SubjectType.Plant, SubjectId = Guid.NewGuid(), TakenAt = Now };

        var entries = await service.AddPhotosAsync(photo.SubjectType, photo.SubjectId, [photo], "Beside @Big Monstera", mentions: [monstera]);

        Assert.Equal([monstera], Assert.Single(entries).Mentions);
    }

    [Fact]
    public async Task Mentioned_in_lists_notes_on_other_records_newest_first_and_skips_deleted_ones()
    {
        var plants = new FakePlantRepository();
        var propagations = new FakePropagationRepository();
        var cases = new FakePestCaseRepository();
        var treatments = new FakePestTreatmentRepository();
        var lookup = new MentionService(timeline, plants, propagations, cases, treatments, new FakePotRepository(), new FixedTime(Now));

        var monstera = new Plant { Nickname = "Big Monstera" };
        var pothos = new Plant { Nickname = "Pothos" };
        var gone = new Plant { Nickname = "Gone" };
        await plants.SaveAsync(monstera);
        await plants.SaveAsync(pothos);
        await plants.SaveAsync(gone);
        await plants.DeleteAsync(gone.Id);
        var mention = new Mention { Kind = SubjectType.Plant, Id = monstera.Id, Name = "Big Monstera" };

        var older = await NoteAsync("Older @Big Monstera", mention);
        older.SubjectId = pothos.Id;
        older.OccurredAt = Now.AddDays(-3);
        var newer = await NoteAsync("Newer @Big Monstera", mention);
        newer.SubjectId = pothos.Id;
        var onItself = await NoteAsync("My own @Big Monstera", mention);
        onItself.SubjectId = monstera.Id;
        var onDeleted = await NoteAsync("On a gone plant @Big Monstera", mention);
        onDeleted.SubjectId = gone.Id;
        var deletedNote = await NoteAsync("Deleted @Big Monstera", mention);
        deletedNote.SubjectId = pothos.Id;
        deletedNote.DeletedAt = Now;

        var lines = await lookup.ForAsync(monstera.Id);

        Assert.Equal(["Newer @Big Monstera", "Older @Big Monstera"], lines.Select(l => l.Snippet));
        Assert.All(lines, l => Assert.Equal(pothos.Id, l.Plant!.Id));
    }

    [Fact]
    public async Task Mentioned_in_includes_pest_case_updates()
    {
        var plants = new FakePlantRepository();
        var cases = new FakePestCaseRepository();
        var treatments = new FakePestTreatmentRepository();
        var lookup = new MentionService(timeline, plants, new FakePropagationRepository(), cases, treatments, new FakePotRepository(), new FixedTime(Now));

        var monstera = new Plant { Nickname = "Big Monstera" };
        await plants.SaveAsync(monstera);
        var pestCase = new PestCase { Pest = Pest.Thrips, StartedOn = Today };
        await cases.SaveAsync(pestCase);
        await treatments.SaveAsync(new PestTreatment
        {
            CaseId = pestCase.Id,
            OccurredOn = Today,
            What = "Soap spray",
            Notes = "Worst on @Big Monstera",
            Mentions = [new Mention { Kind = SubjectType.Plant, Id = monstera.Id, Name = "Big Monstera" }]
        });

        var line = Assert.Single(await lookup.ForAsync(monstera.Id));

        Assert.Equal(pestCase.Id, line.PestCase!.Id);
        Assert.Equal("Worst on @Big Monstera", line.Snippet);
    }

    [Fact]
    public void A_long_note_is_cut_at_a_word()
    {
        var snippet = Mentions.Snippet(new string('a', 12) + " " + new string('b', 100), 20);

        Assert.Equal(new string('a', 12) + "…", snippet);
        Assert.Equal("First line", Mentions.Snippet("First line\nSecond line"));
    }
}
