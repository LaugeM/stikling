using Stikling.Core.Models;
using Stikling.Core.Sync;
using Stikling.Core.Tidy;

namespace Stikling.Core.Tests;

public class RecentlyRemovedTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);

    private readonly InMemoryRecordStore store = new();
    private readonly FakePlantRepository plants = new(stampsDates: true);
    private readonly FakePropagationRepository propagations = new();
    private readonly FakePhotoRepository photos = new();
    private readonly FakeTimelineRepository timeline = new();
    private readonly FakeImages images = new();

    private sealed class FakeImages : IRemovedPhotoImages
    {
        public HashSet<Guid> Here { get; } = [];
        public List<Guid> Queued { get; } = [];

        public Task<bool> HasBytesAsync(Guid id) => Task.FromResult(Here.Contains(id));

        public Task QueueUploadAsync(Guid id)
        {
            Queued.Add(id);
            return Task.CompletedTask;
        }
    }

    private RecentlyRemovedService Service() =>
        new(store, plants, propagations, photos, timeline, images, new FixedTime(Now));

    private async Task<Plant> RemovedPlant(string? name, DateTimeOffset removedAt)
    {
        var plant = new Plant { Nickname = name, CreatedAt = Now.AddDays(-90), UpdatedAt = removedAt, DeletedAt = removedAt };
        plants.Plants[plant.Id] = plant;
        await store.PutAsync(RecordKinds.Plants, plant);
        return plant;
    }

    private async Task<Photo> RemovedPhoto(Guid subjectId, DateTimeOffset removedAt, bool imagesHere = true)
    {
        var photo = new Photo { SubjectId = subjectId, CreatedAt = Now.AddDays(-90), UpdatedAt = removedAt, DeletedAt = removedAt };
        photos.Photos[photo.Id] = photo;
        await store.PutAsync(RecordKinds.Photos, photo);
        if (imagesHere)
            images.Here.Add(photo.Id);
        return photo;
    }

    private async Task<TimelineEntry> RemovedEntry(Guid subjectId, TimelineKind kind, DateTimeOffset removedAt, SubjectType type = SubjectType.Plant)
    {
        var entry = new TimelineEntry { SubjectType = type, SubjectId = subjectId, Kind = kind, Text = "Leaf unfurled\nsecond line", CreatedAt = Now.AddDays(-90), UpdatedAt = removedAt, DeletedAt = removedAt };
        timeline.Entries.Add(entry);
        await store.PutAsync(RecordKinds.Timeline, entry);
        return entry;
    }

    [Fact]
    public async Task Lists_what_was_removed_in_the_last_thirty_days_and_not_older()
    {
        var exactly = await RemovedPlant("Exactly thirty days", Now.AddDays(-30));
        await RemovedPlant("Just too old", Now.AddDays(-30).AddSeconds(-1));
        await RemovedPlant("Yesterday", Now.AddDays(-1));

        var report = await Service().GetAsync();

        Assert.Equal(["Yesterday", "Exactly thirty days"], report.Of(RemovedKind.Plant).Select(i => i.Name));
        Assert.Contains(report.Items, i => i.Id == exactly.Id);
        Assert.Equal(0, report.Stripped);
    }

    [Fact]
    public async Task Newest_removal_comes_first_across_kinds()
    {
        var plant = await RemovedPlant("Hoya", Now.AddDays(-10));
        await RemovedPhoto(plant.Id, Now.AddDays(-2));
        await RemovedEntry(plant.Id, TimelineKind.Note, Now.AddDays(-5));

        var report = await Service().GetAsync();

        Assert.Equal([RemovedKind.Photo, RemovedKind.Note, RemovedKind.Plant], report.Items.Select(i => i.Kind));
    }

    [Fact]
    public async Task A_copy_with_only_its_id_and_dates_is_counted_not_listed()
    {
        await RemovedPlant("Hoya", Now.AddDays(-3));
        await RemovedPlant(null, Now.AddDays(-3));
        await RemovedPhoto(Guid.Empty, Now.AddDays(-3));

        var report = await Service().GetAsync();

        Assert.Single(report.Items);
        Assert.Equal(2, report.Stripped);
    }

    [Fact]
    public async Task A_photo_whose_images_are_gone_is_counted_not_offered()
    {
        var plant = await RemovedPlant("Hoya", Now.AddDays(-3));
        await RemovedPhoto(plant.Id, Now.AddDays(-3), imagesHere: false);

        var report = await Service().GetAsync();

        Assert.Empty(report.Of(RemovedKind.Photo));
        Assert.Equal(1, report.Stripped);
    }

    [Fact]
    public async Task Only_notes_are_listed_from_the_history()
    {
        var plant = await RemovedPlant("Hoya", Now.AddDays(-3));
        await RemovedEntry(plant.Id, TimelineKind.Change, Now.AddDays(-1));
        await RemovedEntry(plant.Id, TimelineKind.Note, Now.AddDays(-2));

        var notes = (await Service().GetAsync()).Of(RemovedKind.Note).ToList();

        Assert.Single(notes);
        Assert.Equal("Leaf unfurled", notes[0].Text);
        Assert.Equal("Hoya", notes[0].Owner?.Name);
    }

    [Fact]
    public async Task Putting_back_a_plant_clears_the_deletion_and_gives_it_a_newer_update()
    {
        var plant = await RemovedPlant("Hoya", Now.AddDays(-3));
        // The fake repository's clock starts on 1 January 2026
        plant.UpdatedAt = new DateTimeOffset(2025, 12, 1, 0, 0, 0, TimeSpan.Zero);
        await store.PutAsync(RecordKinds.Plants, plant);
        var before = plant.UpdatedAt;
        var item = (await Service().GetAsync()).Items.Single();

        var done = await Service().PutBackAsync(item);

        Assert.True(done);
        var saved = plants.Plants[plant.Id];
        Assert.Null(saved.DeletedAt);
        Assert.True(saved.UpdatedAt > before);
        Assert.Equal("Hoya", saved.Nickname);
    }

    [Fact]
    public async Task Putting_back_a_photo_of_a_removed_plant_puts_the_plant_back_and_queues_the_images()
    {
        var plant = await RemovedPlant("Hoya", Now.AddDays(-3));
        var photo = await RemovedPhoto(plant.Id, Now.AddDays(-2));
        var item = (await Service().GetAsync()).Of(RemovedKind.Photo).Single();
        Assert.True(item.Owner?.IsRemoved);

        await Service().PutBackAsync(item);

        Assert.Null(plants.Plants[plant.Id].DeletedAt);
        Assert.Null(photos.Photos[photo.Id].DeletedAt);
        Assert.Equal([photo.Id], images.Queued);
    }

    [Fact]
    public async Task Putting_back_a_note_puts_back_its_removed_cutting()
    {
        var cutting = new Propagation { Nickname = "Stem in water", CreatedAt = Now.AddDays(-90), UpdatedAt = Now.AddDays(-3), DeletedAt = Now.AddDays(-3) };
        propagations.Propagations[cutting.Id] = cutting;
        await store.PutAsync(RecordKinds.Propagations, cutting);
        var entry = await RemovedEntry(cutting.Id, TimelineKind.Note, Now.AddDays(-3), SubjectType.Propagation);
        var item = (await Service().GetAsync()).Of(RemovedKind.Note).Single();

        await Service().PutBackAsync(item);

        Assert.Null(propagations.Propagations[cutting.Id].DeletedAt);
        Assert.Contains(entry.Id, timeline.Updated);
    }

    [Fact]
    public async Task Putting_back_what_is_only_a_stripped_copy_does_nothing()
    {
        var stripped = await RemovedPlant(null, Now.AddDays(-3));

        var done = await Service().PutBackAsync(new RemovedItem(RemovedKind.Plant, stripped.Id, Now.AddDays(-3), "", null, null, null));

        Assert.False(done);
        Assert.NotNull(plants.Plants[stripped.Id].DeletedAt);
    }

    [Fact]
    public async Task Putting_back_a_photo_also_puts_back_the_empty_entry_deleted_with_it()
    {
        var plant = new Plant { Nickname = "Hoya", CreatedAt = Now.AddDays(-90), UpdatedAt = Now.AddDays(-90) };
        plants.Plants[plant.Id] = plant;
        await store.PutAsync(RecordKinds.Plants, plant);
        var photo = await RemovedPhoto(plant.Id, Now.AddDays(-2));
        var withIt = await RemovedEntry(plant.Id, TimelineKind.Photo, Now.AddDays(-2).AddSeconds(1));
        withIt.Text = null;
        withIt.PhotoIds = [photo.Id];
        await store.PutAsync(RecordKinds.Timeline, withIt);
        var earlier = await RemovedEntry(plant.Id, TimelineKind.Photo, Now.AddDays(-5));
        earlier.Text = null;
        earlier.PhotoIds = [photo.Id];
        await store.PutAsync(RecordKinds.Timeline, earlier);
        var item = (await Service().GetAsync()).Of(RemovedKind.Photo).Single();

        await Service().PutBackAsync(item);

        Assert.Contains(withIt.Id, timeline.Updated);
        Assert.DoesNotContain(earlier.Id, timeline.Updated);
    }
}
