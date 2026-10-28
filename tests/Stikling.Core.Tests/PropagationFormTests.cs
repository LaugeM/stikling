using Stikling.Core.Models;
using Stikling.Core.Names;
using Stikling.Core.Plants;
using Stikling.Core.Pots;
using Stikling.Core.Propagations;
using Stikling.Core.Rooms;
using Stikling.Core.Sellers;
using Stikling.Core.Settings;
using Stikling.Core.Timeline;
using Stikling.Core.Wishlist;

namespace Stikling.Core.Tests;

public class PropagationFormTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 24);

    private readonly FakePlantRepository plants = new();
    private readonly FakePropagationRepository propagations = new();
    private readonly FakeTimelineRepository timeline = new();
    private readonly FakePlaceRepository places = new();
    private readonly FakeSellerRepository sellers = new();
    private readonly FakePotRepository pots = new();
    private readonly FakeSettingsRepository settingsRepository = new();
    private readonly FakeWishRepository wishes = new();
    private IWishRepository wishRepository;
    private ITimelineRepository timelineRepository;

    public PropagationFormTests()
    {
        wishRepository = wishes;
        timelineRepository = timeline;
    }

    private static Func<Enum, string> Label(Propagation _) => value => value.ToString();

    private PropagationForms Forms()
    {
        var time = new FixedTime(Now);
        var settings = new SettingsService(settingsRepository);
        var photos = new FakePhotoRepository();
        return new PropagationForms(
            plants,
            propagations,
            new FakeSoilMixRepository(),
            new PropagationService(propagations, plants, timelineRepository, new FakePropagationCareLogRepository(), new FakeCareLogRepository(), photos, time),
            new WishService(wishRepository, photos, time),
            new PlantNameService(new FakePlantNameSource(PlantNameData.Empty), plants, propagations, settings, new FakeDeviceLanguages()),
            new RoomService(places, plants, propagations),
            new SellerService(sellers, plants, propagations),
            new PotService(pots, plants, photos, propagations),
            time);
    }

    private async Task<Plant> StoredPlant(Action<Plant>? change = null)
    {
        var plant = new Plant { Genus = "Alocasia", Species = "zebrina" };
        change?.Invoke(plant);
        await plants.SaveAsync(plant);
        return plant;
    }

    private async Task<Propagation> StoredPropagation(Action<Propagation>? change = null)
    {
        var propagation = new Propagation { Nickname = "Corms", StartedOn = Today.AddDays(-5) };
        change?.Invoke(propagation);
        await propagations.SaveAsync(propagation);
        return propagation;
    }

    [Fact]
    public async Task A_parent_gives_its_names_place_and_the_next_letter_of_its_number()
    {
        var spot = places.Add("Living room");
        var parent = await StoredPlant(p =>
        {
            p.CollectionNumber = "117";
            p.PlaceId = spot.Id;
        });

        var form = await Forms().NewAsync(parent.Id, null, Label);

        Assert.True(form.IsNew);
        Assert.Equal(parent.Id, form.Propagation.ParentPlantId);
        Assert.Equal("Alocasia", form.Propagation.Genus);
        Assert.Equal("zebrina", form.Propagation.Species);
        Assert.Equal("117a", form.Propagation.CollectionNumber);
        Assert.Equal(Today, form.Propagation.StartedOn);
        Assert.Equal("Living room", form.Location);
    }

    [Fact]
    public async Task A_wish_gives_the_names_and_source_and_saving_makes_the_seller_and_marks_it_got()
    {
        var wish = new Wish { Genus = "Hoya", Species = "carnosa", FoundAt = "Plant fair" };
        await wishes.SaveAsync(wish);

        var form = await Forms().NewAsync(null, wish.Id, Label);

        Assert.Equal("Hoya", form.Propagation.Genus);
        Assert.Equal("carnosa", form.Propagation.Species);
        Assert.Null(form.Propagation.Nickname);
        Assert.Equal("Plant fair", form.Propagation.Source);
        Assert.Equal("Plant fair", form.SellerText);

        var result = await form.SaveAsync([]);

        Assert.Empty(result.Errors);
        Assert.True(result.Created);
        Assert.False(form.IsNew);
        Assert.Equal(sellers.Sellers.Values.Single().Id, propagations.Propagations[form.Propagation.Id].SellerId);
        Assert.NotNull(wishes.Wishes[wish.Id].GotOn);
        Assert.Equal(form.Propagation.Id, wishes.Wishes[wish.Id].PropagationId);
    }

    [Fact]
    public async Task A_wish_without_a_genus_gives_its_name_as_the_nickname()
    {
        var wish = new Wish { Name = "Pink thing" };
        await wishes.SaveAsync(wish);

        var form = await Forms().NewAsync(null, wish.Id, Label);

        Assert.Equal("Pink thing", form.Propagation.Nickname);
    }

    [Fact]
    public async Task A_wish_that_fails_to_move_is_reported_apart_from_the_save()
    {
        var wish = new Wish { Genus = "Hoya" };
        await wishes.SaveAsync(wish);
        wishRepository = new ThrowingWishRepository(wishes);

        var form = await Forms().NewAsync(null, wish.Id, Label);
        var result = await form.SaveAsync([]);

        Assert.Empty(result.Errors);
        Assert.True(result.Created);
        Assert.NotNull(result.LaterError);
        Assert.True(propagations.Propagations.ContainsKey(form.Propagation.Id));
    }

    [Fact]
    public async Task A_missing_propagation_gives_no_form()
    {
        Assert.Null(await Forms().EditAsync(Guid.NewGuid(), Label));
    }

    [Fact]
    public async Task Choosing_a_parent_replaces_a_suggested_number_but_keeps_a_typed_one()
    {
        var first = await StoredPlant(p => p.CollectionNumber = "117");
        var second = await StoredPlant(p => p.CollectionNumber = "200");
        var form = await Forms().NewAsync(first.Id, null, Label);
        Assert.Equal("117a", form.Propagation.CollectionNumber);

        form.Propagation.ParentPlantId = second.Id;
        form.ParentChanged();
        Assert.Equal("200a", form.Propagation.CollectionNumber);

        form.Propagation.CollectionNumber = "42";
        form.Propagation.ParentPlantId = first.Id;
        form.ParentChanged();
        Assert.Equal("42", form.Propagation.CollectionNumber);
    }

    [Fact]
    public async Task Choosing_a_parent_fills_empty_names_and_place_and_leaves_filled_ones()
    {
        var spot = places.Add("Living room");
        var parent = await StoredPlant(p => p.PlaceId = spot.Id);

        var empty = await Forms().NewAsync(null, null, Label);
        empty.Propagation.ParentPlantId = parent.Id;
        empty.ParentChanged();
        Assert.Equal("Alocasia", empty.Propagation.Genus);
        Assert.Equal("zebrina", empty.Propagation.Species);
        Assert.Equal("Living room", empty.Location);

        var filled = await Forms().NewAsync(null, null, Label);
        filled.Propagation.Nickname = "Mine";
        filled.Location = "Kitchen";
        filled.Propagation.ParentPlantId = parent.Id;
        filled.ParentChanged();
        Assert.Equal("Mine", filled.Propagation.Nickname);
        Assert.Null(filled.Propagation.Genus);
        Assert.Equal("Kitchen", filled.Location);
    }

    [Fact]
    public async Task Drying_is_written_as_hours_and_cleared_when_unticked()
    {
        var form = await Forms().NewAsync(null, null, Label);

        form.DryFirst = true;
        Assert.Equal(1, form.Propagation.DriedForHours);
        form.DryAmount = 6;
        Assert.Equal(6, form.Propagation.DriedForHours);
        form.DryAmount = 2;
        form.DryInDays = true;
        Assert.Equal(48, form.Propagation.DriedForHours);
        form.DryFirst = false;
        Assert.Null(form.Propagation.DriedForHours);
    }

    [Fact]
    public async Task A_stored_drying_time_is_shown_in_days_when_it_divides_evenly()
    {
        var saved = await StoredPropagation(p => p.DriedForHours = 72);

        var form = (await Forms().EditAsync(saved.Id, Label))!;

        Assert.True(form.DryFirst);
        Assert.Equal(3, form.DryAmount);
        Assert.True(form.DryInDays);
    }

    [Fact]
    public async Task A_drying_time_that_is_not_a_number_blocks_the_save()
    {
        var form = await Forms().NewAsync(null, null, Label);
        form.Propagation.Nickname = "Corms";
        form.DryFirst = true;
        form.DryAmount = null;

        var result = await form.SaveAsync([]);

        Assert.True(form.DryInvalid);
        Assert.Contains(PropagationForm.DryMessage, result.Errors);
        Assert.Empty(propagations.Propagations);
        Assert.Empty(timeline.Entries);
    }

    [Fact]
    public async Task Like_last_time_copies_the_setup_and_the_container_text()
    {
        var parent = await StoredPlant();
        await StoredPropagation(p =>
        {
            p.ParentPlantId = parent.Id;
            p.Genus = "Alocasia";
            p.Type = PropagationType.Corm;
            p.Medium = GrowingMedium.Leca;
            p.Container = "Jar";
            p.RootingAids = [RootingAid.Cinnamon];
            p.DriedForHours = 24;
        });
        var form = await Forms().NewAsync(parent.Id, null, Label);
        var last = Assert.IsType<Propagation>(form.LastTimeMatch);

        form.UseLastTime(last);

        Assert.Equal(PropagationType.Corm, form.Propagation.Type);
        Assert.Equal(GrowingMedium.Leca, form.Propagation.Medium);
        Assert.Equal("Jar", form.ContainerText);
        Assert.True(form.DryFirst);
        Assert.Equal(1, form.DryAmount);
        Assert.True(form.DryInDays);
        Assert.Null(form.LastTimeMatch);
    }

    [Fact]
    public async Task Changing_the_type_clears_seed_fields_and_corm_size_on_save()
    {
        var form = await Forms().NewAsync(null, null, Label);
        form.Propagation.Nickname = "Mixed";
        form.Propagation.InitialCount = 5;
        form.Propagation.Type = PropagationType.Seed;
        form.Propagation.SeedsGerminated = 2;
        form.Propagation.FirstGerminatedOn = Today;
        form.Propagation.CormSizeMm = 20;
        form.Propagation.Type = PropagationType.Cutting;

        var result = await form.SaveAsync([]);

        Assert.Empty(result.Errors);
        var stored = propagations.Propagations[form.Propagation.Id];
        Assert.Null(stored.SeedsGerminated);
        Assert.Null(stored.FirstGerminatedOn);
        Assert.Null(stored.CormSizeMm);
    }

    [Fact]
    public async Task A_potted_up_count_below_the_plants_still_from_it_is_an_error()
    {
        var saved = await StoredPropagation(p =>
        {
            p.InitialCount = 3;
            p.PottedUpCount = 2;
        });
        await plants.SaveAsync(new Plant { Nickname = "One", FromPropagationId = saved.Id });
        await plants.SaveAsync(new Plant { Nickname = "Two", FromPropagationId = saved.Id });
        var form = (await Forms().EditAsync(saved.Id, Label))!;
        Assert.Equal(2, form.PlantsFromIt);
        form.Propagation.PottedUpCount = 1;

        var result = await form.SaveAsync([]);

        var error = Assert.Single(result.Errors);
        Assert.Equal("2 plants potted up from it are still in your collection, so the potted up count can't be lower.", error);
        Assert.Empty(timeline.Entries);
    }

    [Fact]
    public async Task A_seller_is_only_looked_up_when_its_text_was_changed()
    {
        var gone = new Seller { Name = "Old shop" };
        await sellers.SaveAsync(gone);
        await sellers.DeleteAsync(gone.Id);
        var saved = await StoredPropagation(p => p.SellerId = gone.Id);
        var form = (await Forms().EditAsync(saved.Id, Label))!;
        form.Propagation.Nickname = "Renamed";

        await form.SaveAsync([]);

        Assert.Equal(gone.Id, propagations.Propagations[saved.Id].SellerId);
        Assert.Single(sellers.Sellers);

        form.SellerText = "New shop";
        await form.SaveAsync([]);

        Assert.Equal(2, sellers.Sellers.Count);
        Assert.Equal("New shop", sellers.Sellers[propagations.Propagations[saved.Id].SellerId!.Value].Name);
    }

    [Fact]
    public async Task A_container_is_only_looked_up_when_its_text_was_changed()
    {
        var gone = new Pot { Name = "Old jar", Group = PotGroup.Standalone, ForPropagation = true, Owned = 1, Material = PotMaterial.Plastic };
        await pots.SaveAsync(gone);
        await pots.DeleteAsync(gone.Id);
        var saved = await StoredPropagation(p =>
        {
            p.ContainerPotId = gone.Id;
            p.Container = "Old jar";
        });
        var form = (await Forms().EditAsync(saved.Id, Label))!;
        form.Propagation.Nickname = "Renamed";

        await form.SaveAsync([]);

        Assert.Equal(gone.Id, propagations.Propagations[saved.Id].ContainerPotId);
        Assert.Single(pots.Pots);
    }

    [Fact]
    public async Task Saving_an_edit_records_what_changed_in_one_entry()
    {
        var saved = await StoredPropagation(p => p.Medium = GrowingMedium.Soil);
        var form = (await Forms().EditAsync(saved.Id, Label))!;
        form.Propagation.Medium = GrowingMedium.Water;

        var result = await form.SaveAsync([]);

        Assert.Empty(result.Errors);
        Assert.False(result.Created);
        Assert.Equal(GrowingMedium.Water, propagations.Propagations[saved.Id].Medium);
        var entry = Assert.Single(timeline.Entries, e => e.SubjectId == saved.Id);
        Assert.Equal(TimelineKind.Change, entry.Kind);
    }

    [Fact]
    public async Task A_save_that_failed_after_the_record_was_written_carries_on_without_doubling_anything()
    {
        var parent = await StoredPlant();
        var wish = new Wish { Genus = "Alocasia" };
        await wishes.SaveAsync(wish);
        timelineRepository = new FailingOncePhotoTimeline(timeline);
        var photo = new Photo { SubjectType = SubjectType.Propagation, TakenAt = Now.AddHours(-1) };

        var form = await Forms().NewAsync(parent.Id, wish.Id, Label);
        Assert.False(form.Stored);
        await Assert.ThrowsAsync<InvalidOperationException>(() => form.SaveAsync([photo]));
        Assert.True(form.IsNew);
        // It is stored, so leaving the page keeps the photo
        Assert.True(form.Stored);

        var result = await form.SaveAsync([photo]);

        Assert.Empty(result.Errors);
        Assert.True(result.Created);
        var id = form.Propagation.Id;
        Assert.Single(propagations.Propagations);
        Assert.Equal(1, timeline.Entries.Count(e => e.SubjectId == id && e.Kind == TimelineKind.Created));
        Assert.Equal(1, timeline.Entries.Count(e => e.SubjectId == id && e.Kind == TimelineKind.Photo));
        Assert.Equal(1, timeline.Entries.Count(e => e.SubjectId == parent.Id && e.Kind == TimelineKind.Propagated));
        Assert.Equal(id, wishes.Wishes[wish.Id].PropagationId);

        // Saved once more it is an edit, so nothing is created again
        var again = await form.SaveAsync([]);
        Assert.False(again.Created);
        Assert.Single(propagations.Propagations);
    }

    [Fact]
    public async Task An_edit_whose_history_failed_after_the_save_is_saved_and_not_recorded_twice()
    {
        var saved = await StoredPropagation(p =>
        {
            p.Medium = GrowingMedium.Soil;
            p.Genus = "Alocasia";
            p.Species = "zebrina";
            p.NameQualifier = NameQualifier.Cf;
        });
        timelineRepository = new FailingOnceTimeline(timeline, e => e.Event == TimelineEvent.Identified);
        var form = (await Forms().EditAsync(saved.Id, Label))!;
        form.Propagation.Medium = GrowingMedium.Water;
        form.Propagation.NameQualifier = null;

        var result = await form.SaveAsync([]);

        Assert.Empty(result.Errors);
        Assert.NotNull(result.LaterError);
        Assert.Equal(GrowingMedium.Water, propagations.Propagations[saved.Id].Medium);
        Assert.Single(timeline.Entries, e => e.SubjectId == saved.Id && e.Kind == TimelineKind.Change);

        await form.SaveAsync([]);

        Assert.Single(timeline.Entries, e => e.SubjectId == saved.Id && e.Kind == TimelineKind.Change);
    }

    private sealed class FailingOncePhotoTimeline(ITimelineRepository inner) : ITimelineRepository
    {
        private bool failed;

        public Task<IReadOnlyList<TimelineEntry>> GetForAsync(Guid subjectId) => inner.GetForAsync(subjectId);

        public Task<IReadOnlyDictionary<Guid, TimelineEntry>> GetLatestPerSubjectAsync() => inner.GetLatestPerSubjectAsync();

        public Task<IReadOnlyList<TimelineEntry>> GetRecentAsync(
            int count, IReadOnlySet<Guid> subjectIds, DateTimeOffset since, IReadOnlySet<Guid>? referencePhotoIds = null) =>
            inner.GetRecentAsync(count, subjectIds, since, referencePhotoIds);

        public Task<IReadOnlyList<TimelineEntry>> GetPlantChangesAsync() => inner.GetPlantChangesAsync();

        public Task<IReadOnlyList<TimelineEntry>> GetComingBackAsync() => inner.GetComingBackAsync();

        public Task<IReadOnlyDictionary<Guid, DateTimeOffset>> GetLastActivityBeforeAsync(DateTimeOffset before) =>
            inner.GetLastActivityBeforeAsync(before);

        public Task<IReadOnlyList<TimelineEntry>> GetSinceAsync(DateTimeOffset since) => inner.GetSinceAsync(since);

        public Task AddAsync(TimelineEntry entry)
        {
            if (!failed && entry.Kind == TimelineKind.Photo)
            {
                failed = true;
                throw new InvalidOperationException("Offline");
            }

            return inner.AddAsync(entry);
        }

        public Task UpdateAsync(TimelineEntry entry) => inner.UpdateAsync(entry);

        public Task DeleteAsync(Guid id) => inner.DeleteAsync(id);
    }

    private sealed class ThrowingWishRepository(IWishRepository inner) : IWishRepository
    {
        public Task<IReadOnlyList<Wish>> GetAllAsync() => inner.GetAllAsync();

        public Task<Wish?> GetAsync(Guid id) => inner.GetAsync(id);

        public Task SaveAsync(Wish wish) => throw new InvalidOperationException("Offline");

        public Task DeleteAsync(Guid id) => inner.DeleteAsync(id);
    }
}
