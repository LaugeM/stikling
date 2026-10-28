using Stikling.Core.Models;
using Stikling.Core.Names;
using Stikling.Core.Pests;
using Stikling.Core.Plants;
using Stikling.Core.Propagations;
using Stikling.Core.Rooms;
using Stikling.Core.Sellers;
using Stikling.Core.Settings;
using Stikling.Core.Fields;
using Stikling.Core.Wishlist;

namespace Stikling.Core.Tests;

public class PlantFormTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 24);

    private readonly FakePlantRepository plants = new();
    private readonly FakeTimelineRepository timeline = new();
    private readonly FakePlaceRepository places = new();
    private readonly FakeSellerRepository sellers = new();
    private readonly FakeSettingsRepository settingsRepository = new();
    private readonly FakeFeedRepository feeds = new();
    private readonly FakeOwnFieldRepository fields = new();
    private FakeWishRepository wishes = new();
    private IWishRepository wishRepository;
    private FakePestTreatmentRepository pests = new();

    public PlantFormTests() => wishRepository = wishes;

    private static string Label(Enum value) => value.ToString();

    private PlantForms Forms()
    {
        var time = new FixedTime(Now);
        var propagations = new FakePropagationRepository();
        var settings = new SettingsService(settingsRepository);
        var plantService = new PlantService(plants, timeline, time);
        return new PlantForms(
            plants,
            propagations,
            new FakePotRepository(),
            new FakeSoilMixRepository(),
            feeds,
            pests,
            settings,
            plantService,
            new WishService(wishRepository, new FakePhotoRepository(), time),
            new PlantNameService(new FakePlantNameSource(PlantNameData.Empty), plants, propagations, settings, new FakeDeviceLanguages()),
            new RoomService(places, plants, propagations),
            new SellerService(sellers, plants, propagations),
            new OwnFieldService(fields, plants),
            time);
    }

    private async Task<Plant> Stored(Action<Plant>? change = null)
    {
        var plant = new Plant { Nickname = "Monstera" };
        change?.Invoke(plant);
        await plants.SaveAsync(plant);
        return plant;
    }

    private void QuarantineSetting(bool on) =>
        settingsRepository.Settings[UserSettings.SettingsId] = new UserSettings { QuarantineNewPlants = on };

    [Fact]
    public async Task A_copy_is_a_duplicate_dated_today()
    {
        var source = await Stored(p =>
        {
            p.Genus = "Hoya";
            p.AcquiredOn = LooseDate.Of(new DateOnly(2024, 3, 1));
        });

        var form = await Forms().NewAsync(source.Id, null, null, Label);

        Assert.True(form.IsNew);
        Assert.NotEqual(source.Id, form.Plant.Id);
        Assert.Equal("Hoya", form.Plant.Genus);
        Assert.Equal(LooseDate.Of(Today), form.Plant.AcquiredOn);
        Assert.Equal(LooseDate.Of(new DateOnly(2024, 3, 1)), form.CopiedDate);
        Assert.Equal(source.DisplayName, form.CopiedFrom);
    }

    [Fact]
    public async Task A_wish_gives_the_names_and_source_and_saving_makes_the_seller_and_marks_it_got()
    {
        var wish = new Wish { Genus = "Hoya", Species = "carnosa", FoundAt = "Plant fair" };
        await wishes.SaveAsync(wish);

        var form = await Forms().NewAsync(null, wish.Id, null, Label);

        Assert.Equal("Hoya", form.Plant.Genus);
        Assert.Equal("carnosa", form.Plant.Species);
        Assert.Equal("Plant fair", form.Plant.Source);
        Assert.Equal("Plant fair", form.SellerText);
        Assert.Equal(wish.Id, form.Wish?.Id);

        var result = await form.SaveAsync([]);

        Assert.Empty(result.Errors);
        Assert.True(result.Created);
        Assert.Contains(sellers.Sellers.Values, s => s.Name == "Plant fair");
        Assert.Equal(sellers.Sellers.Values.Single().Id, plants.Plants[form.Plant.Id].SellerId);
        Assert.NotNull(wishes.Wishes[wish.Id].GotOn);
        Assert.Equal(form.Plant.Id, wishes.Wishes[wish.Id].PlantId);
    }

    [Fact]
    public async Task A_copy_wins_over_a_wish()
    {
        var source = await Stored(p => p.Genus = "Ficus");
        var wish = new Wish { Genus = "Hoya", FoundAt = "Plant fair" };
        await wishes.SaveAsync(wish);

        var form = await Forms().NewAsync(source.Id, wish.Id, null, Label);

        Assert.Equal("Ficus", form.Plant.Genus);
        Assert.NotEqual("Plant fair", form.Plant.Source);
    }

    [Fact]
    public async Task A_wish_that_fails_to_move_is_reported_apart_from_the_save()
    {
        var wish = new Wish { Genus = "Hoya" };
        await wishes.SaveAsync(wish);
        wishRepository = new ThrowingWishRepository(wishes);

        var form = await Forms().NewAsync(null, wish.Id, null, Label);
        var result = await form.SaveAsync([]);

        Assert.Empty(result.Errors);
        Assert.NotNull(result.LaterError);
        Assert.True(plants.Plants.ContainsKey(form.Plant.Id));
    }

    [Fact]
    public async Task A_parent_makes_it_propagated_with_the_parents_names_and_place()
    {
        var spot = places.Add("Living room");
        var parent = await Stored(p =>
        {
            p.Genus = "Monstera";
            p.Species = "deliciosa";
            p.Cultivar = "Albo";
            p.PlaceId = spot.Id;
        });

        var form = await Forms().NewAsync(null, null, parent.Id, Label);

        Assert.Equal(PlantOrigin.Propagated, form.Plant.Origin);
        Assert.Equal(parent.Id, form.Plant.ParentPlantId);
        Assert.Equal("Monstera", form.Plant.Genus);
        Assert.Equal("deliciosa", form.Plant.Species);
        Assert.Equal("Albo", form.Plant.Cultivar);
        Assert.Equal(spot.Id, form.Plant.PlaceId);
        Assert.Equal("Living room", form.Location);
    }

    [Fact]
    public async Task A_bought_plant_starts_in_quarantine_when_the_setting_is_on()
    {
        QuarantineSetting(true);

        var form = await Forms().NewAsync(null, null, null, Label);

        Assert.Equal(PlantOrigin.Purchased, form.Plant.Origin);
        Assert.Equal(Today, form.Plant.QuarantinedSince);
        Assert.Equal(Plant.DefaultQuarantineDays, form.Plant.QuarantineDays);
    }

    [Fact]
    public async Task A_bought_plant_does_not_start_in_quarantine_when_the_setting_is_off()
    {
        var form = await Forms().NewAsync(null, null, null, Label);

        Assert.False(form.Plant.InQuarantine);
    }

    [Fact]
    public async Task Changing_the_origin_to_one_that_does_not_quarantine_clears_it()
    {
        QuarantineSetting(true);
        var form = await Forms().NewAsync(null, null, null, Label);

        form.Plant.Origin = PlantOrigin.Propagated;
        form.OriginChanged();

        Assert.False(form.Plant.InQuarantine);
        Assert.Null(form.Plant.QuarantineDays);

        form.Plant.Origin = PlantOrigin.Gift;
        form.OriginChanged();

        Assert.True(form.Plant.InQuarantine);
    }

    [Fact]
    public async Task After_the_box_is_ticked_the_origin_no_longer_touches_quarantine()
    {
        QuarantineSetting(true);
        var form = await Forms().NewAsync(null, null, null, Label);

        form.SetQuarantine(false);
        form.Plant.Origin = PlantOrigin.Gift;
        form.OriginChanged();

        Assert.False(form.Plant.InQuarantine);
    }

    [Fact]
    public async Task Ticking_quarantine_again_on_a_saved_plant_keeps_its_original_date()
    {
        var since = new DateOnly(2026, 10, 10);
        var saved = await Stored(p =>
        {
            p.QuarantinedSince = since;
            p.QuarantineDays = 21;
        });
        var form = (await Forms().EditAsync(saved.Id, Label))!;

        form.SetQuarantine(false);
        Assert.False(form.Plant.InQuarantine);
        form.SetQuarantine(true);

        Assert.Equal(since, form.Plant.QuarantinedSince);
        Assert.Equal(21, form.Plant.QuarantineDays);
    }

    private (Place Balcony, Place Living) OutdoorAndIndoor()
    {
        var balcony = places.Add("Balcony");
        balcony.Outdoors = true;
        return (balcony, places.Add("Living room"));
    }

    [Fact]
    public async Task A_plant_moved_from_an_outdoor_place_to_an_indoor_one_came_inside()
    {
        var (balcony, _) = OutdoorAndIndoor();
        var saved = await Stored(p => p.PlaceId = balcony.Id);
        var form = (await Forms().EditAsync(saved.Id, Label))!;

        Assert.False(form.CameInside);

        form.Location = "Living room";

        Assert.True(form.CameInside);
    }

    [Fact]
    public async Task Coming_inside_with_quarantine_is_saved_in_quarantine_since_today_in_one_write()
    {
        var (balcony, _) = OutdoorAndIndoor();
        var saved = await Stored(p => p.PlaceId = balcony.Id);
        var form = (await Forms().EditAsync(saved.Id, Label))!;
        form.Location = "Living room";
        form.BackQuarantine = true;
        var writes = plants.Saves;

        var result = await form.SaveAsync([]);

        Assert.Empty(result.Errors);
        Assert.Equal(writes + 1, plants.Saves);
        Assert.Equal(Today, plants.Plants[saved.Id].QuarantinedSince);
        Assert.Equal(Plant.DefaultQuarantineDays, plants.Plants[saved.Id].QuarantineDays);
        Assert.Empty(pests.Treatments);
    }

    [Fact]
    public async Task Coming_inside_with_a_pest_check_saves_the_check()
    {
        var (balcony, _) = OutdoorAndIndoor();
        var saved = await Stored(p => p.PlaceId = balcony.Id);
        var form = (await Forms().EditAsync(saved.Id, Label))!;
        form.Location = "Living room";
        form.BackChecked = true;

        await form.SaveAsync([]);

        var check = Assert.Single(pests.Treatments.Values);
        Assert.Equal(saved.Id, check.PlantId);
        Assert.Equal(PestTreatmentKind.Checked, check.Kind);
        Assert.Equal(Today, check.OccurredOn);
    }

    [Fact]
    public async Task A_new_spot_under_an_outdoor_room_is_not_coming_inside()
    {
        var (balcony, _) = OutdoorAndIndoor();
        var saved = await Stored(p => p.PlaceId = balcony.Id);
        var form = (await Forms().EditAsync(saved.Id, Label))!;

        form.Location = "Balcony / Table";

        Assert.False(form.CameInside);
    }

    [Fact]
    public async Task Soil_to_leca_on_a_saved_plant_offers_tracking_and_saving_starts_it()
    {
        var saved = await Stored();
        var form = (await Forms().EditAsync(saved.Id, Label))!;

        Assert.False(form.OffersTransition);
        form.SetMedium(GrowingMedium.Leca);

        Assert.True(form.OffersTransition);
        Assert.True(form.TrackTransition);

        await form.SaveAsync([]);

        var stored = plants.Plants[saved.Id];
        Assert.True(stored.InTransition);
        Assert.Equal(GrowingMedium.Leca, stored.TransitionTo);
        Assert.Equal(Today, stored.TransitionSince);
    }

    [Fact]
    public async Task Unticking_the_tracking_leaves_the_move_untracked()
    {
        var saved = await Stored();
        var form = (await Forms().EditAsync(saved.Id, Label))!;
        form.SetMedium(GrowingMedium.Leca);
        form.TrackTransition = false;

        await form.SaveAsync([]);

        Assert.False(plants.Plants[saved.Id].InTransition);
    }

    private async Task<OwnField> NumberField()
    {
        var field = new OwnField { Name = "Weight", Kind = OwnFieldKind.Number };
        await fields.SaveAsync(field);
        return field;
    }

    [Fact]
    public async Task An_invalid_own_field_gives_its_error_and_writes_nothing()
    {
        var field = await NumberField();
        var saved = await Stored();
        var form = (await Forms().EditAsync(saved.Id, Label))!;
        form.SellerText = "Garden centre";
        form.Location = "Kitchen";
        form.SetFieldInput(field.Id, "heavy");
        var writes = plants.Saves;
        var entries = timeline.Entries.Count;

        var result = await form.SaveAsync([]);

        var error = Assert.Single(result.Errors);
        Assert.StartsWith("Weight: ", error);
        Assert.Equal(writes, plants.Saves);
        Assert.Equal(entries, timeline.Entries.Count);
        Assert.Empty(sellers.Sellers);
        Assert.Empty(places.Places);
        Assert.Empty(plants.Plants[saved.Id].OwnFields);
    }

    [Fact]
    public async Task A_stored_value_left_as_it_was_does_not_block_the_save()
    {
        var field = await NumberField();
        var saved = await Stored(p => p.OwnFields[field.Id] = "heavy");
        var form = (await Forms().EditAsync(saved.Id, Label))!;
        Assert.Equal("heavy", form.FieldInput(field.Id));
        form.Plant.Nickname = "Renamed";

        var result = await form.SaveAsync([]);

        Assert.Empty(result.Errors);
        Assert.Equal("heavy", plants.Plants[saved.Id].OwnFields[field.Id]);
    }

    [Fact]
    public async Task A_valid_own_field_value_is_stored()
    {
        var field = await NumberField();
        var saved = await Stored();
        var form = (await Forms().EditAsync(saved.Id, Label))!;
        form.SetFieldInput(field.Id, "12");

        var result = await form.SaveAsync([]);

        Assert.Empty(result.Errors);
        Assert.Equal("12", plants.Plants[saved.Id].OwnFields[field.Id]);
    }

    [Fact]
    public async Task Saving_again_after_a_failure_updates_instead_of_creating_and_repeats_no_step()
    {
        var (balcony, _) = OutdoorAndIndoor();
        var saved = await Stored(p => p.PlaceId = balcony.Id);
        await new PlantService(plants, timeline, new FixedTime(Now)).CreateAsync(saved);
        pests = new FailingOncePestRepository();
        var form = (await Forms().EditAsync(saved.Id, Label))!;
        form.Location = "Living room";
        form.BackChecked = true;
        form.SetMedium(GrowingMedium.Leca);

        await Assert.ThrowsAsync<InvalidOperationException>(() => form.SaveAsync([]));
        var result = await form.SaveAsync([]);

        Assert.Empty(result.Errors);
        Assert.Single(timeline.Entries, e => e.Kind == TimelineKind.Created);
        Assert.Single(pests.Treatments);
        Assert.True(plants.Plants[saved.Id].InTransition);
    }

    [Fact]
    public async Task A_new_plant_saved_twice_is_created_once_and_then_updated()
    {
        var form = await Forms().NewAsync(null, null, null, Label);
        form.Plant.Nickname = "Fern";

        var first = await form.SaveAsync([]);
        form.Plant.Notes = "Likes the bathroom";
        var second = await form.SaveAsync([]);

        Assert.True(first.Created);
        Assert.False(second.Created);
        Assert.False(form.IsNew);
        Assert.Equal(form.Plant.Id, form.Original?.Id);
        Assert.Single(timeline.Entries, e => e.Kind == TimelineKind.Created);
        Assert.Equal("Likes the bathroom", plants.Plants[form.Plant.Id].Notes);
    }

    [Fact]
    public async Task The_likes_offer_is_returned_when_another_plant_of_the_species_lacks_them()
    {
        var other = await Stored(p =>
        {
            p.Genus = "Hoya";
            p.Species = "carnosa";
        });
        var form = await Forms().NewAsync(null, null, null, Label);
        form.Plant.Genus = "Hoya";
        form.Plant.Species = "carnosa";
        form.Plant.LikesLight = LightLevel.BrightIndirect;

        var result = await form.SaveAsync([]);

        Assert.Equal([other.Id], result.LikesOffer.Select(p => p.Id));
        Assert.Contains(LikesField.Light, result.LikesFields);
    }

    [Fact]
    public async Task There_is_no_offer_when_nothing_about_what_it_likes_was_set()
    {
        await Stored(p =>
        {
            p.Genus = "Hoya";
            p.Species = "carnosa";
        });
        var form = await Forms().NewAsync(null, null, null, Label);
        form.Plant.Genus = "Hoya";
        form.Plant.Species = "carnosa";

        var result = await form.SaveAsync([]);

        Assert.Empty(result.LikesOffer);
    }

    [Fact]
    public async Task A_deleted_feed_clears_the_watering_feed_when_the_form_is_made()
    {
        var feed = new Feed { Name = "Green", Products = [new ProductDose { Name = "Fertiliser", Amount = 2, Per = 1 }] };
        await feeds.SaveAsync(feed);
        var saved = await Stored(p =>
        {
            p.WateringFeedId = feed.Id;
            p.WateringFeedEvery = 2;
        });
        await feeds.DeleteAsync(feed.Id);

        var form = (await Forms().EditAsync(saved.Id, Label))!;

        Assert.Null(form.Plant.WateringFeedId);
        Assert.Null(form.Plant.WateringFeedEvery);
        Assert.Equal(feed.Id, form.Original?.WateringFeedId);
    }

    [Fact]
    public async Task A_missing_plant_gives_no_form()
    {
        Assert.Null(await Forms().EditAsync(Guid.NewGuid(), Label));
    }

    private sealed class FailingOncePestRepository : FakePestTreatmentRepository
    {
        private bool failed;

        public override Task SaveAsync(PestTreatment treatment)
        {
            if (!failed)
            {
                failed = true;
                throw new InvalidOperationException("Offline");
            }

            return base.SaveAsync(treatment);
        }
    }

    private sealed class ThrowingWishRepository(IWishRepository inner) : IWishRepository
    {
        public Task<IReadOnlyList<Wish>> GetAllAsync() => inner.GetAllAsync();

        public Task<Wish?> GetAsync(Guid id) => inner.GetAsync(id);

        public Task SaveAsync(Wish wish) => throw new InvalidOperationException("Offline");

        public Task DeleteAsync(Guid id) => inner.DeleteAsync(id);
    }
}
