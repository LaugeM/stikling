using Stikling.Core.Models;
using Stikling.Core.Propagations;

namespace Stikling.Core.Tests;

public class CarriedPhotosTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 15);

    private readonly FakePlantRepository plants = new();
    private readonly FakePropagationRepository propagations = new();
    private readonly FakeTimelineRepository timeline = new();
    private readonly FakePhotoRepository photos = new();
    private readonly FixedTime time = new(Now);
    private readonly PropagationService service;
    private readonly Propagation cutting = new() { Genus = "Monstera", Type = PropagationType.Cutting, InitialCount = 3, StartedOn = Today.AddDays(-30) };

    public CarriedPhotosTests()
    {
        service = new PropagationService(propagations, plants, timeline, new FakePropagationCareLogRepository(),
            new FakeCareLogRepository(), photos, time);
        propagations.Propagations[cutting.Id] = cutting;
    }

    private Photo AddPhoto(int daysAgo, bool reference = false)
    {
        var photo = new Photo
        {
            SubjectType = SubjectType.Propagation,
            SubjectId = cutting.Id,
            TakenAt = Now.AddDays(-daysAgo),
            IsReference = reference
        };
        photos.Photos[photo.Id] = photo;
        return photo;
    }

    [Fact]
    public async Task Potting_up_gives_the_plants_the_cuttings_cover()
    {
        var cover = AddPhoto(10);
        AddPhoto(5);
        cutting.CoverPhotoId = cover.Id;

        var made = await service.PotUpAsync(cutting, new PotUpRequest(2, Today));

        Assert.All(made, p => Assert.Equal(cover.Id, p.CoverPhotoId));
    }

    [Fact]
    public async Task A_reference_cover_is_skipped_for_the_newest_other_photo()
    {
        var reference = AddPhoto(1, reference: true);
        AddPhoto(20);
        var newest = AddPhoto(8);
        cutting.CoverPhotoId = reference.Id;

        var made = await service.PotUpAsync(cutting, new PotUpRequest(1, Today));

        Assert.Equal(newest.Id, Assert.Single(made).CoverPhotoId);
    }

    [Fact]
    public async Task A_photo_after_the_pot_up_day_is_not_used()
    {
        var before = AddPhoto(12);
        AddPhoto(2);

        var made = await service.PotUpAsync(cutting, new PotUpRequest(1, Today.AddDays(-5)));

        Assert.Equal(before.Id, Assert.Single(made).CoverPhotoId);
    }

    [Fact]
    public async Task Without_photos_there_is_no_cover()
    {
        var made = await service.PotUpAsync(cutting, new PotUpRequest(1, Today));
        Assert.Null(Assert.Single(made).CoverPhotoId);
    }

    private async Task<Plant> OldPlantAsync(DateOnly pottedUp, bool deleted = false)
    {
        var plant = new Plant { Genus = "Monstera", FromPropagationId = cutting.Id, CreatedAt = Now, DeletedAt = deleted ? Now : null };
        plants.Plants[plant.Id] = plant;
        await timeline.AddAsync(new TimelineEntry
        {
            SubjectType = SubjectType.Plant,
            SubjectId = plant.Id,
            Kind = TimelineKind.Created,
            OccurredAt = time.MomentOn(pottedUp),
            RelatedType = SubjectType.Propagation,
            RelatedId = cutting.Id
        });
        return plant;
    }

    [Fact]
    public async Task Carrying_covers_fills_a_missing_cover_from_before_the_pot_up_day()
    {
        var fitting = AddPhoto(12);
        AddPhoto(2);
        var plant = await OldPlantAsync(Today.AddDays(-5));

        Assert.Equal(1, await service.CarryCoversAsync());
        Assert.Equal(fitting.Id, plant.CoverPhotoId);
    }

    [Fact]
    public async Task Carrying_covers_leaves_a_set_cover_alone()
    {
        AddPhoto(3);
        var plant = await OldPlantAsync(Today);
        var own = Guid.NewGuid();
        plant.CoverPhotoId = own;

        Assert.Equal(0, await service.CarryCoversAsync());
        Assert.Equal(own, plant.CoverPhotoId);
    }

    [Fact]
    public async Task Carrying_covers_skips_deleted_plants()
    {
        AddPhoto(3);
        var plant = await OldPlantAsync(Today, deleted: true);

        Assert.Equal(0, await service.CarryCoversAsync());
        Assert.Null(plant.CoverPhotoId);
    }

    [Fact]
    public async Task Carrying_covers_twice_changes_nothing_the_second_time()
    {
        AddPhoto(3);
        await OldPlantAsync(Today);

        Assert.Equal(1, await service.CarryCoversAsync());
        Assert.Equal(0, await service.CarryCoversAsync());
    }

    [Fact]
    public void Earlier_entries_are_those_with_photos_up_to_the_pot_up_day()
    {
        var early = AddPhoto(10);
        var late = AddPhoto(2);
        var reference = AddPhoto(9, reference: true);
        TimelineEntry Entry(Photo on) => new()
        {
            SubjectType = SubjectType.Propagation,
            SubjectId = cutting.Id,
            Kind = TimelineKind.Photo,
            OccurredAt = on.TakenAt,
            PhotoIds = [on.Id]
        };
        var note = new TimelineEntry { SubjectType = SubjectType.Propagation, SubjectId = cutting.Id, Kind = TimelineKind.Note, OccurredAt = Now.AddDays(-9), Text = "Roots" };
        var entries = new[] { Entry(early), Entry(late), Entry(reference), note };

        var shown = CarriedPhotos.EarlierEntries(entries, new HashSet<Guid> { reference.Id }, Today.AddDays(-5), time);

        Assert.Equal([entries[0].Id], shown.Select(e => e.Id));
    }
}
