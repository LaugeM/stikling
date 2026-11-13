using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Propagations;
using Stikling.Core.Timeline;

namespace Stikling.Core.Tests;

public class PublicPageHidingTests
{
    private static readonly FixedTime Time = new(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task A_plant_is_hidden_and_shown_again_through_the_repository_once_per_change()
    {
        var plants = new FakePlantRepository();
        var service = new PlantService(plants, new FakeTimelineRepository(), Time);
        var plant = new Plant { Nickname = "Monstera" };

        await service.SetHiddenFromPublicPageAsync(plant, true);
        await service.SetHiddenFromPublicPageAsync(plant, true);

        Assert.True(plants.Plants[plant.Id].HiddenFromPublicPage);
        Assert.Equal(1, plants.Saves);

        await service.SetHiddenFromPublicPageAsync(plant, false);

        Assert.False(plants.Plants[plant.Id].HiddenFromPublicPage);
        Assert.Equal(2, plants.Saves);
    }

    [Fact]
    public async Task Showing_a_plant_that_was_never_hidden_saves_nothing()
    {
        var plants = new FakePlantRepository();
        var service = new PlantService(plants, new FakeTimelineRepository(), Time);

        await service.SetHiddenFromPublicPageAsync(new Plant { Nickname = "Monstera" }, false);

        Assert.Equal(0, plants.Saves);
    }

    [Fact]
    public async Task A_propagation_is_hidden_and_shown_again_once_per_change()
    {
        var propagations = new FakePropagationRepository();
        var service = new PropagationService(propagations, new FakePlantRepository(), new FakeTimelineRepository(),
            new FakePropagationCareLogRepository(), new FakeCareLogRepository(), new FakePhotoRepository(), Time);
        var cutting = new Propagation { Nickname = "Monstera cutting", StartedOn = new DateOnly(2026, 10, 1) };

        await service.SetHiddenFromPublicPageAsync(cutting, true);
        await service.SetHiddenFromPublicPageAsync(cutting, true);

        Assert.True(propagations.Propagations[cutting.Id].HiddenFromPublicPage);
        Assert.Equal(1, propagations.Saves);

        await service.SetHiddenFromPublicPageAsync(cutting, false);

        Assert.False(propagations.Propagations[cutting.Id].HiddenFromPublicPage);
        Assert.Equal(2, propagations.Saves);
    }

    [Fact]
    public async Task A_photo_is_hidden_and_shown_again_once_per_change()
    {
        var photos = new FakePhotoRepository();
        var photo = new Photo { SubjectType = SubjectType.Plant, SubjectId = Guid.NewGuid() };
        photos.Photos[photo.Id] = photo;
        var updates = new CountingPhotos(photos);
        var service = new TimelineService(new FakeTimelineRepository(), updates, new FakeMeasurementRepository(), Time);

        await service.SetPhotoHiddenFromPublicPageAsync(photo, true);
        await service.SetPhotoHiddenFromPublicPageAsync(photo, true);

        Assert.True(photos.Photos[photo.Id].HiddenFromPublicPage);
        Assert.Equal(1, updates.Updates);

        await service.SetPhotoHiddenFromPublicPageAsync(photo, false);

        Assert.False(photos.Photos[photo.Id].HiddenFromPublicPage);
        Assert.Equal(2, updates.Updates);
    }

    // Counts the updates, since the fake photo repository keeps no count of its own
    private sealed class CountingPhotos(FakePhotoRepository inner) : IPhotoRepository
    {
        public int Updates { get; private set; }
        public Task<IReadOnlyList<Photo>> GetForAsync(Guid subjectId) => inner.GetForAsync(subjectId);
        public Task<Photo?> GetAsync(Guid id) => inner.GetAsync(id);
        public Task<IReadOnlyList<Photo>> GetOfKindAsync(SubjectType subjectType) => inner.GetOfKindAsync(subjectType);
        public Task<IReadOnlyDictionary<Guid, Photo>> GetNewestPerSubjectAsync() => inner.GetNewestPerSubjectAsync();
        public Task AddAsync(Photo photo) => inner.AddAsync(photo);
        public Task DeleteAsync(Guid id) => inner.DeleteAsync(id);

        public Task UpdateAsync(Photo photo)
        {
            Updates++;
            return inner.UpdateAsync(photo);
        }
    }
}
