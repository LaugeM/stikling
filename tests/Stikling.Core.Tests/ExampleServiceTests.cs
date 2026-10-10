using Stikling.Core.Examples;
using Stikling.Core.Models;

namespace Stikling.Core.Tests;

public class ExampleServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 18, 0, 0, TimeSpan.FromHours(2));
    private static readonly DateOnly Today = new(2026, 10, 10);

    private readonly FakePlantRepository plants = new();
    private readonly FakePropagationRepository propagations = new();
    private readonly FakePhotoRepository photos = new();
    private readonly FakeTimelineRepository timeline = new();
    private readonly FakeCareLogRepository careLogs = new();
    private readonly FakeExamplePhotos images = new();
    private readonly FixedTime time = new(Now, TimeSpan.FromHours(2));
    private readonly ExampleService service;

    public ExampleServiceTests() =>
        service = new ExampleService(plants, propagations, photos, timeline, careLogs, images, time);

    private DateOnly DayOf(DateTimeOffset moment) => time.LocalDay(moment);

    [Fact]
    public void Every_record_validates_and_the_ids_are_distinct()
    {
        var records = ExampleFamily.Build(time);

        Assert.All(records.Plants, p => Assert.Empty(p.Validate(Today)));
        Assert.All(records.Propagations, p => Assert.Empty(p.Validate(Today)));

        var ids = records.Plants.Select(p => p.Id)
            .Concat(records.Propagations.Select(p => p.Id))
            .Concat(records.Photos.Select(p => p.Id))
            .Concat(records.Entries.Select(e => e.Id))
            .ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.DoesNotContain(Guid.Empty, ids);
    }

    [Fact]
    public void Dates_count_back_from_today()
    {
        var records = ExampleFamily.Build(time);
        var current = records.Propagations.Single(p => p.Id == ExampleFamily.CurrentCuttingId);
        var first = records.Propagations.Single(p => p.Id == ExampleFamily.FirstCuttingId);
        var young = records.Plants.Single(p => p.Id == ExampleFamily.YoungPlantId);
        var mother = records.Plants.Single(p => p.Id == ExampleFamily.MotherId);

        Assert.Equal(Today.AddDays(-30), current.StartedOn);
        Assert.Equal(Today.AddDays(-16), current.FirstRootOn);
        Assert.Equal(Today.AddDays(-120), first.StartedOn);
        Assert.Equal(Today.AddDays(-90), first.FinishedOn);
        Assert.Equal(Today.AddDays(-90), young.AcquiredOn!.Start);
        Assert.Equal(DatePrecision.Month, mother.AcquiredOn!.Precision);
        Assert.Equal(new DateOnly(2025, 8, 1), mother.AcquiredOn.Start);

        var rootsNote = records.Photos.Single(p => p.Id == ExampleFamily.FirstRootsPhotoId);
        Assert.Equal(Today.AddDays(-16), DayOf(rootsNote.TakenAt));
        Assert.Equal(12, rootsNote.TakenAt.Hour);
        var youngNote = records.Photos.Single(p => p.Id == ExampleFamily.YoungPlantPhotoId);
        Assert.Equal(Today.AddDays(-3), DayOf(youngNote.TakenAt));

        // The same day a month later moves everything with it
        var later = new FixedTime(Now.AddDays(10), TimeSpan.FromHours(2));
        Assert.Equal(Today.AddDays(-20), ExampleFamily.Build(later).Propagations.Single(p => p.Id == ExampleFamily.CurrentCuttingId).StartedOn);
    }

    [Fact]
    public void The_lineage_holds()
    {
        var records = ExampleFamily.Build(time);

        var young = records.Plants.Single(p => p.Id == ExampleFamily.YoungPlantId);
        Assert.Equal(ExampleFamily.MotherId, young.ParentPlantId);
        Assert.Equal(ExampleFamily.FirstCuttingId, young.FromPropagationId);
        Assert.All(records.Propagations, p => Assert.Equal(ExampleFamily.MotherId, p.ParentPlantId));
        Assert.All(records.Photos, p => Assert.True(ExampleFamily.IsExample(p.SubjectId)));
        Assert.All(records.Entries, e => Assert.True(ExampleFamily.IsExample(e.SubjectId)));
    }

    [Fact]
    public async Task Load_then_remove_leaves_nothing_live_and_frees_all_five_images()
    {
        Assert.False(await service.IsLoadedAsync());
        await service.LoadAsync();
        Assert.True(await service.IsLoadedAsync());
        Assert.Equal(2, (await plants.GetAllAsync()).Count);
        Assert.Equal(2, (await propagations.GetAllAsync()).Count);
        Assert.Equal(5, images.Stored.Count);

        await service.RemoveAsync();

        Assert.False(await service.IsLoadedAsync());
        Assert.Empty(await plants.GetAllAsync());
        Assert.Empty(await propagations.GetAllAsync());
        Assert.All(photos.Photos.Values, p => Assert.True(p.IsDeleted));
        Assert.All(timeline.Entries, e => Assert.True(e.IsDeleted));
        Assert.Empty(images.Stored);
        Assert.Equal(ExampleFamily.PhotoIds.Order(), images.Removed.Distinct().Order());
    }

    [Fact]
    public async Task Remove_also_deletes_what_was_added_but_keeps_plants_made_from_it()
    {
        await service.LoadAsync();
        var addedPhoto = new Photo { SubjectType = SubjectType.Plant, SubjectId = ExampleFamily.MotherId, TakenAt = Now };
        await photos.AddAsync(addedPhoto);
        images.Stored.Add(addedPhoto.Id);
        var addedNote = new TimelineEntry
        {
            SubjectType = SubjectType.Propagation, SubjectId = ExampleFamily.CurrentCuttingId,
            Kind = TimelineKind.Note, Text = "Mine", OccurredAt = Now
        };
        await timeline.AddAsync(addedNote);
        await careLogs.SaveAsync(new CareLog { PlantId = ExampleFamily.YoungPlantId, OccurredOn = Today, Kind = CareKind.Watered });
        var offspring = new Plant { Nickname = "Mine", ParentPlantId = ExampleFamily.MotherId };
        await plants.SaveAsync(offspring);

        await service.RemoveAsync();

        Assert.True(addedNote.IsDeleted);
        Assert.True(photos.Photos[addedPhoto.Id].IsDeleted);
        Assert.DoesNotContain(addedPhoto.Id, images.Stored);
        Assert.Empty(await careLogs.GetAllAsync());
        var left = Assert.Single(await plants.GetAllAsync());
        Assert.Equal(offspring.Id, left.Id);
        Assert.Equal(ExampleFamily.MotherId, left.ParentPlantId);
    }

    [Fact]
    public async Task Loading_again_after_a_removal_brings_everything_back_with_the_same_ids()
    {
        await service.LoadAsync();
        var plantIds = (await plants.GetAllAsync()).Select(p => p.Id).Order().ToList();
        var entryIds = timeline.Entries.Select(e => e.Id).Order().ToList();
        await service.RemoveAsync();

        await service.LoadAsync();

        Assert.True(await service.IsLoadedAsync());
        Assert.Equal(plantIds, (await plants.GetAllAsync()).Select(p => p.Id).Order());
        Assert.Equal(2, (await propagations.GetAllAsync()).Count);
        Assert.Equal(entryIds, timeline.Entries.Select(e => e.Id).Order());
        Assert.All(timeline.Entries, e => Assert.False(e.IsDeleted));
        Assert.Equal(5, photos.Photos.Values.Count(p => !p.IsDeleted));
        Assert.Equal(5, images.Stored.Count);
    }

    [Fact]
    public async Task A_failing_image_writes_no_records_and_frees_what_was_stored()
    {
        images.FailOn = ExampleFamily.Images[3].FileName;

        await Assert.ThrowsAsync<InvalidOperationException>(service.LoadAsync);

        Assert.Empty(await plants.GetAllAsync());
        Assert.Empty(await propagations.GetAllAsync());
        Assert.Empty(photos.Photos);
        Assert.Empty(timeline.Entries);
        Assert.Empty(images.Stored);
        Assert.Equal(3, images.Removed.Count);
    }

    private sealed class FakeExamplePhotos : IExamplePhotos
    {
        public HashSet<Guid> Stored { get; } = [];
        public List<Guid> Removed { get; } = [];
        public string? FailOn { get; set; }

        public Task<(int Width, int Height)> StoreAsync(Guid photoId, string fileName)
        {
            if (fileName == FailOn)
                throw new InvalidOperationException("No connection");
            Stored.Add(photoId);
            return Task.FromResult((1200, 1600));
        }

        public Task RemoveBytesAsync(Guid photoId)
        {
            Stored.Remove(photoId);
            Removed.Add(photoId);
            return Task.CompletedTask;
        }
    }
}
