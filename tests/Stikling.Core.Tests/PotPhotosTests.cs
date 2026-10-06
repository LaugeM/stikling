using System.Text.Json;
using Stikling.Core.Models;
using Stikling.Core.Pots;

namespace Stikling.Core.Tests;

public class PotPhotosTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    private readonly FakePotRepository pots = new();
    private readonly FakePlantRepository plants = new();
    private readonly FakePhotoRepository photos = new();

    private PotService Service => new(pots, plants, photos);

    private static Pot Outer(string name = "Ceramic cachepot") =>
        new() { Group = PotGroup.Outer, Name = name, TopCm = 14 };

    private Photo PhotoOf(Pot pot, int daysAgo)
    {
        var photo = new Photo { SubjectType = SubjectType.Pot, SubjectId = pot.Id, TakenAt = Now.AddDays(-daysAgo) };
        photos.Photos[photo.Id] = photo;
        return photo;
    }

    [Fact]
    public void A_pot_without_photos_has_no_cover()
    {
        Assert.Null(PotPhotos.Cover(Outer(), []));
    }

    [Fact]
    public void The_newest_photo_stands_for_the_pot_until_one_is_chosen()
    {
        var pot = Outer();
        PhotoOf(pot, daysAgo: 10);
        var newest = PhotoOf(pot, daysAgo: 1);

        Assert.Same(newest, PotPhotos.Cover(pot, photos.Photos.Values));
    }

    [Fact]
    public void A_chosen_cover_wins_and_falls_back_to_the_newest_once_deleted()
    {
        var pot = Outer();
        var chosen = PhotoOf(pot, daysAgo: 10);
        var newest = PhotoOf(pot, daysAgo: 1);
        pot.CoverPhotoId = chosen.Id;

        Assert.Same(chosen, PotPhotos.Cover(pot, photos.Photos.Values));

        chosen.DeletedAt = Now;

        Assert.Same(newest, PotPhotos.Cover(pot, photos.Photos.Values));
    }

    [Fact]
    public void Covers_only_list_pots_that_have_a_photo()
    {
        var withPhoto = Outer();
        var without = Outer("Glazed bowl");
        var photo = PhotoOf(withPhoto, daysAgo: 2);

        var covers = PotPhotos.Covers([withPhoto, without], photos.Photos.Values);

        Assert.Same(photo, Assert.Single(covers).Value);
        Assert.False(covers.ContainsKey(without.Id));
    }

    [Fact]
    public async Task Deleting_a_pot_deletes_its_photos_and_leaves_the_plant_pointing_at_it()
    {
        var pot = Outer();
        var other = Outer("Glazed bowl");
        await pots.SaveAsync(pot);
        await pots.SaveAsync(other);
        var plant = new Plant { Nickname = "Hoya", OuterPotId = pot.Id };
        await plants.SaveAsync(plant);
        var first = PhotoOf(pot, daysAgo: 3);
        var second = PhotoOf(pot, daysAgo: 1);
        var kept = PhotoOf(other, daysAgo: 1);

        var deleted = await Service.DeleteAsync(pot.Id);

        Assert.Equal(new HashSet<Guid> { first.Id, second.Id }, deleted.ToHashSet());
        Assert.True(first.IsDeleted && second.IsDeleted);
        Assert.False(kept.IsDeleted);
        Assert.Null(await pots.GetAsync(pot.Id));
        Assert.Equal(pot.Id, plants.Plants[plant.Id].OuterPotId);
    }

    [Fact]
    public async Task Choosing_a_cover_saves_it_on_the_pot()
    {
        var pot = Outer();
        await pots.SaveAsync(pot);
        var photo = PhotoOf(pot, daysAgo: 5);
        PhotoOf(pot, daysAgo: 1);

        await Service.SetCoverAsync(pot, photo);

        Assert.Same(photo, (await Service.GetCoversAsync())[pot.Id]);
    }

    [Fact]
    public void A_pot_photo_is_stored_with_its_subject_as_text()
    {
        var photo = new Photo { SubjectType = SubjectType.Pot, SubjectId = Guid.NewGuid() };

        var json = JsonSerializer.Serialize(photo, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"subjectType\":\"Pot\"", json);
        Assert.Equal(SubjectType.Pot, JsonSerializer.Deserialize<Photo>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.SubjectType);
    }

    [Fact]
    public void A_pot_saved_before_covers_existed_has_none()
    {
        var pot = JsonSerializer.Deserialize<Pot>("""{"id":"6f9619ff-8b86-d011-b42d-00c04fc964ff","name":"Clear nursery pot","owned":5}""",
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        Assert.Null(pot.CoverPhotoId);
    }
}
