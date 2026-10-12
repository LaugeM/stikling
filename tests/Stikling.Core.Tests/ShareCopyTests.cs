using System.Text.Json;
using Stikling.Core.Models;
using Stikling.Core.Sharing;

namespace Stikling.Core.Tests;

public class ShareCopyTests
{
    private static readonly DateOnly Today = new(2026, 10, 12);
    private static readonly TimeZoneInfo Copenhagen = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");

    private static DateTimeOffset At(int month, int day, int hour = 12) => new(2026, month, day, hour, 0, 0, TimeSpan.Zero);

    private static ShareLinkSettings Settings(bool notes = true, params Guid[] leftOut) => new(notes, null, null, leftOut);

    private static Plant GivenAwayPlant()
    {
        var plant = new Plant
        {
            Nickname = "Mona", Genus = "monstera", Species = "deliciosa", Cultivar = "Thai Constellation",
            AcquiredOn = LooseDate.Of(new DateOnly(2026, 8, 29)), Status = PlantStatus.GivenAway, Source = "Secret shop"
        };
        return plant;
    }

    private static Photo PhotoOf(Entity subject, DateTimeOffset taken, SubjectType type = SubjectType.Plant) =>
        new() { SubjectType = type, SubjectId = subject.Id, TakenAt = taken, UpdatedAt = taken, Width = 1600, Height = 1200 };

    private static SharePageModel Page(Plant plant, IEnumerable<Photo> photos, IEnumerable<TimelineEntry>? entries = null, ShareLinkSettings? settings = null) =>
        SharePage.ForPlant(plant, null, null, [], photos, entries ?? [], [], new Dictionary<Guid, Propagation>(), settings ?? Settings(), Copenhagen, Today);

    private static TimelineEntry Note(Entity subject, DateTimeOffset at, string text, params Photo[] photos) =>
        new()
        {
            SubjectType = SubjectType.Plant, SubjectId = subject.Id, Kind = TimelineKind.Note, OccurredAt = at, Text = text, UpdatedAt = at,
            PhotoIds = photos.Select(p => p.Id).ToList()
        };

    [Fact]
    public void A_plants_copy_has_its_names_cover_and_items_and_no_propagation()
    {
        var plant = GivenAwayPlant();
        var photo = PhotoOf(plant, At(9, 1));
        plant.CoverPhotoId = photo.Id;
        var page = Page(plant, [photo]);

        var copy = ShareCopy.ForPlant(page, plant, Copenhagen);

        Assert.Equal(1, copy.Version);
        Assert.Equal(SubjectType.Plant, copy.SubjectType);
        Assert.Equal(plant.Id, copy.SubjectId);
        Assert.Equal("Mona", copy.Name);
        Assert.Equal("monstera", copy.Genus);
        Assert.Equal("deliciosa", copy.Species);
        Assert.Equal("Thai Constellation", copy.Cultivar);
        Assert.Null(copy.Propagation);
        Assert.Equal(photo.Id, copy.CoverPhotoId);
        Assert.Equal(page.Items.Count, copy.Items.Count);
        Assert.Equal(photo.Id, copy.Items.SelectMany(i => i.Photos).Single().Id);
        Assert.Equal((1600, 1200), (copy.Items[1].Photos[0].Width, copy.Items[1].Photos[0].Height));
    }

    [Fact]
    public void A_propagations_copy_has_its_dates()
    {
        var cutting = new Propagation
        {
            Nickname = "Mona cutting", Type = PropagationType.Corm, Medium = GrowingMedium.Leca, StartedOn = new DateOnly(2026, 8, 1),
            FirstRootOn = new DateOnly(2026, 8, 19), FirstLeafOn = new DateOnly(2026, 9, 2), FirstGerminatedOn = new DateOnly(2026, 8, 20),
            RootedOn = new DateOnly(2026, 9, 5), InitialCount = 3, GivenAwayCount = 1
        };
        var page = SharePage.ForPropagation(cutting, null, [], [], [], Settings(), Copenhagen, Today);

        var copy = ShareCopy.ForPropagation(page, cutting, Copenhagen);

        Assert.Equal(SubjectType.Propagation, copy.SubjectType);
        Assert.Equal(new ShareCopyPropagation(PropagationType.Corm, GrowingMedium.Leca, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 19),
            new DateOnly(2026, 9, 2), new DateOnly(2026, 8, 20), new DateOnly(2026, 9, 5)), copy.Propagation);
    }

    [Fact]
    public void Day_counts_are_frozen_without_the_separator()
    {
        var cutting = new Propagation { Nickname = "Cut", StartedOn = new DateOnly(2026, 8, 1) };
        var photo = PhotoOf(cutting, At(8, 25), SubjectType.Propagation);
        var page = SharePage.ForPropagation(cutting, null, [photo], [], [], Settings(), Copenhagen, Today);

        var copy = ShareCopy.ForPropagation(page, cutting, Copenhagen);

        Assert.Equal(" · day 24", page.Items.Last().DayCount);
        Assert.Equal("day 24", copy.Items.Last().DayCount);
        Assert.Equal("day 0", copy.Items[0].DayCount);
    }

    [Fact]
    public void A_day_without_a_count_has_no_label()
    {
        var plant = GivenAwayPlant();
        var photo = PhotoOf(plant, At(8, 1));

        var copy = ShareCopy.ForPlant(Page(plant, [photo]), plant, Copenhagen);

        // Before the day it was got, a plant has no age
        Assert.Null(copy.Items[0].DayCount);
    }

    [Fact]
    public void A_photo_is_taken_at_noon_on_its_day_in_the_links_zone()
    {
        var plant = GivenAwayPlant();
        // 22:30 UTC on 1 Sep is already 2 Sep in Copenhagen (summer time, +02:00)
        var photo = PhotoOf(plant, new DateTimeOffset(2026, 9, 1, 22, 30, 0, TimeSpan.Zero));
        var winter = PhotoOf(plant, new DateTimeOffset(2026, 1, 10, 11, 0, 0, TimeSpan.Zero));

        var copy = ShareCopy.ForPlant(Page(plant, [photo, winter]), plant, Copenhagen);

        var taken = copy.Items.SelectMany(i => i.Photos).ToDictionary(p => p.Id, p => p.TakenAt);
        Assert.Equal(new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.FromHours(2)), taken[photo.Id]);
        Assert.Equal(TimeSpan.FromHours(2), taken[photo.Id].Offset);
        Assert.Equal(new DateTimeOffset(2026, 1, 10, 12, 0, 0, TimeSpan.FromHours(1)), taken[winter.Id]);
    }

    [Fact]
    public void Left_out_photos_and_hidden_notes_stay_out_of_the_copy()
    {
        var plant = GivenAwayPlant();
        var hidden = PhotoOf(plant, At(9, 1));
        var shown = PhotoOf(plant, At(9, 2));
        var noted = PhotoOf(plant, At(9, 3));
        var entry = Note(plant, At(9, 3), "Private words", noted);

        var copy = ShareCopy.ForPlant(Page(plant, [hidden, shown, noted], [entry], Settings(notes: false, hidden.Id)), plant, Copenhagen);

        var json = JsonSerializer.Serialize(copy);
        Assert.DoesNotContain("Private words", json);
        Assert.DoesNotContain("Secret shop", json);
        Assert.DoesNotContain(copy.Items.SelectMany(i => i.Photos), p => p.Id == hidden.Id);
        Assert.Contains(copy.Items.SelectMany(i => i.Photos), p => p.Id == noted.Id);
    }

    [Fact]
    public void Enums_are_written_as_text()
    {
        var cutting = new Propagation { Nickname = "Cut", Type = PropagationType.AirLayer, StartedOn = new DateOnly(2026, 8, 1) };
        var page = SharePage.ForPropagation(cutting, null, [], [], [], Settings(), Copenhagen, Today);

        var json = JsonSerializer.Serialize(ShareCopy.ForPropagation(page, cutting, Copenhagen));

        Assert.Contains("\"AirLayer\"", json);
        Assert.Contains("\"Milestone\"", json);
        Assert.Contains("\"Propagation\"", json);
    }
}
