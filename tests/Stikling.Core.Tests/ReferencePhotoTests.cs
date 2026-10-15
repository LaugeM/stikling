using Stikling.Core.Models;
using Stikling.Core.Sharing;
using Stikling.Core.Timeline;
using Stikling.Core.Wishlist;

namespace Stikling.Core.Tests;

public class ReferencePhotoTests
{
    private static readonly FixedTime Time = new(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));

    private static DateTimeOffset At(int month, int day) => new(2026, month, day, 12, 0, 0, TimeSpan.Zero);

    private static Photo PhotoOf(Guid subjectId, DateTimeOffset taken, bool reference = false,
        SubjectType type = SubjectType.Plant) =>
        new() { SubjectType = type, SubjectId = subjectId, TakenAt = taken, UpdatedAt = taken, IsReference = reference };

    [Fact]
    public void Own_leaves_out_reference_and_deleted_photos()
    {
        var plantId = Guid.NewGuid();
        var own = PhotoOf(plantId, At(9, 1));
        var reference = PhotoOf(plantId, At(9, 2), reference: true);
        var deleted = PhotoOf(plantId, At(9, 3));
        deleted.DeletedAt = At(9, 4);

        Assert.Equal([own], PhotoKinds.Own([own, reference, deleted]));
    }

    [Fact]
    public void References_are_the_live_reference_photos_newest_first()
    {
        var plantId = Guid.NewGuid();
        var older = PhotoOf(plantId, At(9, 1), reference: true);
        var newer = PhotoOf(plantId, At(9, 5), reference: true);
        var own = PhotoOf(plantId, At(9, 9));
        var deleted = PhotoOf(plantId, At(9, 7), reference: true);
        deleted.DeletedAt = At(9, 8);

        Assert.Equal([newer, older], PhotoKinds.References([older, own, newer, deleted]));
    }

    [Fact]
    public void A_cover_that_is_a_reference_photo_falls_back_to_none()
    {
        var plantId = Guid.NewGuid();
        var own = PhotoOf(plantId, At(9, 1));
        var reference = PhotoOf(plantId, At(9, 2), reference: true);

        Assert.Equal(own.Id, PhotoKinds.ValidCover(own.Id, [own, reference]));
        Assert.Null(PhotoKinds.ValidCover(reference.Id, [own, reference]));
        Assert.Null(PhotoKinds.ValidCover(Guid.NewGuid(), [own, reference]));
        Assert.Null(PhotoKinds.ValidCover(null, [own, reference]));
    }

    [Fact]
    public void A_time_lapse_leaves_reference_photos_out()
    {
        var plantId = Guid.NewGuid();
        var first = PhotoOf(plantId, At(9, 1));
        var reference = PhotoOf(plantId, At(9, 2), reference: true);
        var last = PhotoOf(plantId, At(9, 3));

        var photos = TimelapsePlan.PhotosOf([first, reference, last]);

        Assert.Equal([first.Id, last.Id], photos.Select(p => p.Id));
    }

    [Fact]
    public void The_share_page_and_the_history_passed_on_leave_reference_photos_out()
    {
        var plant = new Plant { Nickname = "Mona", Genus = "monstera" };
        var own = PhotoOf(plant.Id, At(9, 1));
        var reference = PhotoOf(plant.Id, At(9, 2), reference: true);
        var entry = new TimelineEntry
        {
            SubjectType = SubjectType.Plant, SubjectId = plant.Id, Kind = TimelineKind.Photo, OccurredAt = At(9, 2),
            UpdatedAt = At(9, 2), PhotoIds = [reference.Id]
        };

        var page = SharePage.ForPlant(plant, null, null, [], [own, reference], [entry], [], new Dictionary<Guid, Propagation>(),
            new ShareLinkSettings(true, null, null, []), TimeZoneInfo.Utc, new DateOnly(2026, 10, 15));

        var shown = page.Items.SelectMany(i => i.Photos).Select(p => p.Id).ToList();
        Assert.Equal([own.Id], shown);
        Assert.Equal(1, page.PhotoCount);
        Assert.Equal(own.Id, page.Cover?.Id);
    }

    [Fact]
    public void The_photo_round_does_not_count_a_reference_photo()
    {
        var plantId = Guid.NewGuid();
        var photos = new FakePhotoRepository();
        photos.Photos.Add(Guid.NewGuid(), PhotoOf(plantId, At(9, 1)));
        var recent = PhotoOf(plantId, At(10, 10), reference: true);
        photos.Photos.Add(recent.Id, recent);

        var newest = photos.GetNewestPerSubjectAsync().Result;

        Assert.Equal(At(9, 1), newest[plantId].TakenAt);
    }

    private static (WishService Service, FakePhotoRepository Photos, Wish Wish) WishWithPhotos(int count)
    {
        var photos = new FakePhotoRepository();
        var service = new WishService(new FakeWishRepository(), photos, Time);
        var wish = new Wish { Genus = "Monstera" };
        for (var i = 0; i < count; i++)
        {
            var photo = PhotoOf(wish.Id, At(9, 1 + i), type: SubjectType.Wish);
            photos.Photos[photo.Id] = photo;
        }

        return (service, photos, wish);
    }

    [Fact]
    public async Task Getting_a_wish_as_a_plant_moves_its_photos_to_the_plant_as_reference_photos()
    {
        var (service, photos, wish) = WishWithPhotos(2);
        await service.SaveAsync(wish);
        var gone = PhotoOf(wish.Id, At(9, 20), type: SubjectType.Wish);
        gone.DeletedAt = At(9, 21);
        photos.Photos[gone.Id] = gone;
        var plantId = Guid.NewGuid();

        Assert.True(await service.MarkGotAsync(wish.Id, plantId, null));

        var moved = photos.Photos.Values.Where(p => !p.IsDeleted).ToList();
        Assert.Equal(2, moved.Count);
        Assert.All(moved, p =>
        {
            Assert.Equal(SubjectType.Plant, p.SubjectType);
            Assert.Equal(plantId, p.SubjectId);
            Assert.True(p.IsReference);
        });
        Assert.Equal(SubjectType.Wish, gone.SubjectType);
    }

    [Fact]
    public async Task Getting_a_wish_as_a_cutting_moves_its_photos_to_the_propagation()
    {
        var (service, photos, wish) = WishWithPhotos(1);
        await service.SaveAsync(wish);
        var propagationId = Guid.NewGuid();

        await service.MarkGotAsync(wish.Id, null, propagationId);

        var photo = photos.Photos.Values.Single();
        Assert.Equal(SubjectType.Propagation, photo.SubjectType);
        Assert.Equal(propagationId, photo.SubjectId);
        Assert.True(photo.IsReference);
    }

    [Fact]
    public async Task Putting_a_got_wish_back_leaves_the_photos_with_the_plant()
    {
        var (service, photos, wish) = WishWithPhotos(1);
        await service.SaveAsync(wish);
        var plantId = Guid.NewGuid();
        await service.MarkGotAsync(wish.Id, plantId, null);

        await service.PutBackAsync(wish.Id);

        Assert.Equal(plantId, photos.Photos.Values.Single().SubjectId);
    }

    [Fact]
    public async Task A_got_wish_that_was_already_got_does_not_move_photos_again()
    {
        var (service, photos, wish) = WishWithPhotos(1);
        await service.SaveAsync(wish);
        var plantId = Guid.NewGuid();
        await service.MarkGotAsync(wish.Id, plantId, null);

        Assert.False(await service.MarkGotAsync(wish.Id, Guid.NewGuid(), null));

        Assert.Equal(plantId, photos.Photos.Values.Single().SubjectId);
    }

    [Fact]
    public async Task Deleting_a_wish_soft_deletes_its_photos()
    {
        var (service, photos, wish) = WishWithPhotos(2);
        await service.SaveAsync(wish);

        var deleted = await service.DeleteAsync(wish.Id);

        Assert.Equal(2, deleted.Count);
        Assert.All(photos.Photos.Values, p => Assert.True(p.IsDeleted));
    }

    [Fact]
    public async Task The_wishlist_shows_the_newest_photo_of_each_wish()
    {
        var (service, photos, wish) = WishWithPhotos(3);
        await service.SaveAsync(wish);

        var newest = await service.GetNewestPhotosAsync();

        Assert.Equal(At(9, 3), newest[wish.Id].TakenAt);
    }
}
