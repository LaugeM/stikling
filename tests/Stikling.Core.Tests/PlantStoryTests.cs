using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Sharing;

namespace Stikling.Core.Tests;

public class PlantStoryTests
{
    private static readonly TimeProvider Time = new FixedTime(new DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero));

    private static DateTimeOffset On(int year, int month, int day) => new(year, month, day, 12, 0, 0, TimeSpan.Zero);

    private static Photo PhotoOn(DateTimeOffset at, bool reference = false) => new() { TakenAt = at, IsReference = reference };

    private static TimelineEntry Entry(DateTimeOffset at, TimelineKind kind, string? text) => new() { OccurredAt = at, Kind = kind, Text = text };

    [Fact]
    public void Photos_are_oldest_first_without_references()
    {
        var newer = PhotoOn(On(2026, 5, 1));
        var older = PhotoOn(On(2025, 5, 1));
        var reference = PhotoOn(On(2024, 5, 1), reference: true);

        var photos = PlantStory.Photos([newer, older, reference], [], null, Time);

        Assert.Equal([older.Id, newer.Id], photos.Select(p => p.Id));
        Assert.Equal(new DateOnly(2025, 5, 1), photos[0].Day);
    }

    [Fact]
    public void Photos_of_the_cutting_count_up_to_the_pot_up()
    {
        var asCutting = PhotoOn(On(2025, 1, 1));
        var afterPotUp = PhotoOn(On(2025, 6, 1));
        var own = PhotoOn(On(2025, 7, 1));

        var photos = PlantStory.Photos([own], [asCutting, afterPotUp], new DateOnly(2025, 3, 1), Time);

        Assert.Equal([asCutting.Id, own.Id], photos.Select(p => p.Id));
    }

    [Fact]
    public void A_plant_with_nothing_has_an_empty_story()
    {
        Assert.Empty(PlantStory.Photos([], [], null, Time));
        Assert.Empty(PlantStory.Notes([], [], null, null, Time));
        Assert.Empty(PlantStory.Spread(Array.Empty<StoryPhoto>(), PlantStory.PhotosAtFirst));
    }

    [Fact]
    public void Notes_keep_what_was_written_and_leave_out_the_apps_own_lines()
    {
        var deleted = Entry(On(2025, 3, 1), TimelineKind.Note, "Gone");
        deleted.DeletedAt = On(2025, 3, 2);

        var notes = PlantStory.Notes(
        [
            Entry(On(2025, 5, 1), TimelineKind.Note, "  Second  "),
            Entry(On(2025, 4, 1), TimelineKind.Photo, "First"),
            Entry(On(2025, 6, 1), TimelineKind.Note, "   "),
            Entry(On(2025, 7, 1), TimelineKind.Change, "Moved to the kitchen"),
            Entry(On(2025, 8, 1), TimelineKind.Created, "Added"),
            deleted
        ], [], null, null, Time);

        Assert.Equal(["First", "Second"], notes.Select(n => n.Text));
    }

    [Fact]
    public void Notes_include_the_cutting_up_to_the_pot_up_and_what_came_with_it()
    {
        var received = new ReceivedHistory
        {
            Items =
            [
                new() { Day = new DateOnly(2024, 1, 1), Kind = ShareItemKind.Note, Text = "From the friend" },
                new() { Day = new DateOnly(2024, 2, 1), Kind = ShareItemKind.Photos },
                new() { Day = new DateOnly(2024, 3, 1), Kind = ShareItemKind.Note, Text = "Also from the friend" }
            ]
        };

        var notes = PlantStory.Notes(
            [Entry(On(2025, 7, 1), TimelineKind.Note, "Own")],
            [Entry(On(2025, 1, 1), TimelineKind.Note, "As a cutting"), Entry(On(2025, 6, 1), TimelineKind.Note, "Later on the cutting")],
            new DateOnly(2025, 3, 1),
            received,
            Time);

        Assert.Equal(["From the friend", "Also from the friend", "As a cutting", "Own"], notes.Select(n => n.Text));
        Assert.Equal(notes.Count, notes.Select(n => n.Key).Distinct().Count());
    }

    [Fact]
    public void Spread_takes_all_when_they_fit()
    {
        Assert.Equal([1, 2, 3], PlantStory.Spread([1, 2, 3], 6));
    }

    [Theory]
    [InlineData(7)]
    [InlineData(42)]
    [InlineData(1000)]
    public void Spread_keeps_the_first_and_last_and_no_doubles(int many)
    {
        var items = Enumerable.Range(0, many).ToList();

        var picked = PlantStory.Spread(items, 6);

        Assert.Equal(6, picked.Distinct().Count());
        Assert.Equal(0, picked[0]);
        Assert.Equal(many - 1, picked[^1]);
        Assert.Equal(picked.Order(), picked);
    }
}
