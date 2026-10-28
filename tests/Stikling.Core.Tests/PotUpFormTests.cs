using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Pots;
using Stikling.Core.Propagations;
using Stikling.Core.Rooms;
using Stikling.Core.Timeline;

namespace Stikling.Core.Tests;

public class PotUpFormTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 24);

    private readonly FakePlantRepository plants = new();
    private readonly FakePropagationRepository propagations = new();
    private readonly FakeTimelineRepository timeline = new();
    private readonly FakePlaceRepository places = new();
    private IPlantRepository plantRepository;
    private IPropagationRepository propagationRepository;
    private ITimelineRepository timelineRepository;

    public PotUpFormTests()
    {
        plantRepository = plants;
        propagationRepository = propagations;
        timelineRepository = timeline;
    }

    private static string Label(Enum value) => value.ToString();

    private UnitForms Forms()
    {
        var time = new FixedTime(Now);
        return new UnitForms(
            new PropagationService(propagationRepository, plantRepository, timelineRepository, new FakePropagationCareLogRepository(),
                new FakeCareLogRepository(), new FakePhotoRepository(), time),
            new RoomService(places, plants, propagations),
            plants,
            time);
    }

    private PotUpForm Form(Propagation propagation) => Forms().PotUp(propagation, places.Current, Label);

    private Propagation Stored(Action<Propagation>? change = null)
    {
        var propagation = new Propagation
        {
            Nickname = "Corms",
            StartedOn = Today.AddDays(-10),
            InitialCount = 4,
            Medium = GrowingMedium.Water
        };
        change?.Invoke(propagation);
        propagations.Propagations[propagation.Id] = propagation;
        return propagation;
    }

    private List<Plant> PlantsFrom(Propagation propagation) =>
        plants.Plants.Values.Where(p => p.FromPropagationId == propagation.Id).ToList();

    private int Created(Plant plant) =>
        timeline.Entries.Count(e => e.SubjectId == plant.Id && e.Kind == TimelineKind.Created);

    [Fact]
    public void The_nickname_is_only_passed_on_when_there_is_no_genus()
    {
        Assert.Equal("Corms", Form(Stored()).Nickname);
        Assert.Null(Form(Stored(p => p.Genus = "Alocasia")).Nickname);
    }

    [Fact]
    public void The_defaults_are_today_and_where_it_is()
    {
        var spot = places.Add("Kitchen");
        var form = Form(Stored(p => p.PlaceId = spot.Id));

        Assert.Equal(Today, form.Date);
        Assert.Equal("Kitchen", form.Location);
        Assert.Equal(1, form.Count);
        Assert.Equal(PotUpKind.Each, form.Mode);
    }

    [Fact]
    public void A_rooting_medium_becomes_soil_and_a_plant_medium_is_kept()
    {
        Assert.Equal(GrowingMedium.Soil, Form(Stored()).Medium);

        var leca = Form(Stored(p => p.Medium = GrowingMedium.Leca));
        Assert.Equal(GrowingMedium.Leca, leca.Medium);
    }

    [Fact]
    public void The_mix_is_only_kept_for_soil()
    {
        var mix = Guid.NewGuid();

        Assert.Equal(mix, Form(Stored(p => { p.Medium = GrowingMedium.Soil; p.SoilMixId = mix; })).SoilMixId);
        Assert.Null(Form(Stored(p => { p.Medium = GrowingMedium.Water; p.SoilMixId = mix; })).SoilMixId);
    }

    [Fact]
    public void One_in_a_pot_one_left_counts_as_each()
    {
        var form = Form(Stored(p => p.InitialCount = 1));
        form.Mode = PotUpKind.OnePot;

        Assert.Equal(PotUpKind.Each, form.EffectiveMode);

        var many = Form(Stored());
        many.Mode = PotUpKind.OnePot;
        Assert.Equal(PotUpKind.OnePot, many.EffectiveMode);
    }

    [Fact]
    public void It_can_be_saved_with_a_count_in_range_and_a_plant_to_pot_with()
    {
        var form = Form(Stored());
        Assert.True(form.CanSave);

        form.Count = 0;
        Assert.False(form.CanSave);
        form.Count = 5;
        Assert.False(form.CanSave);
        form.Count = 4;
        Assert.True(form.CanSave);

        form.Mode = PotUpKind.Into;
        Assert.False(form.CanSave);
        form.IntoPlantId = Guid.NewGuid();
        Assert.True(form.CanSave);
    }

    [Fact]
    public void A_pot_sets_the_medium_until_one_was_picked()
    {
        var wick = new Pot { Name = "Self-watering", SelfWatering = PotWatering.Wick };
        var form = Form(Stored());
        Assert.Equal(GrowingMedium.Soil, form.Medium);

        form.PickPot(wick.Id, wick);
        Assert.Equal(GrowingMedium.Leca, form.Medium);
        Assert.Equal(wick.Id, form.PotId);

        form.PickMedium(GrowingMedium.Pon);
        form.PickPot(wick.Id, wick);
        Assert.Equal(GrowingMedium.Pon, form.Medium);
    }

    [Fact]
    public void A_pot_does_not_change_the_medium_of_one_already_in_a_plant_medium()
    {
        var wick = new Pot { Name = "Self-watering", SelfWatering = PotWatering.Wick };
        var form = Form(Stored(p => p.Medium = GrowingMedium.OrchidBark));

        form.PickPot(wick.Id, wick);

        Assert.Equal(GrowingMedium.OrchidBark, form.Medium);
    }

    [Fact]
    public async Task The_mix_is_not_sent_when_the_medium_is_not_soil()
    {
        var mix = Guid.NewGuid();
        var propagation = Stored(p => p.Medium = GrowingMedium.Soil);
        var form = Form(propagation);
        form.SoilMixId = mix;
        form.PickMedium(GrowingMedium.Leca);

        await form.SaveAsync();

        Assert.Null(PlantsFrom(propagation).Single().SoilMixId);
    }

    [Fact]
    public async Task Saving_makes_a_plant_for_each_unit_and_counts_them()
    {
        var propagation = Stored();
        var form = Form(propagation);
        form.Count = 3;
        form.Location = "Kitchen";

        var result = await form.SaveAsync();

        Assert.Null(result.LaterError);
        Assert.Equal(3, result.Plants.Count);
        Assert.Equal(3, PlantsFrom(propagation).Count);
        Assert.Equal(3, propagation.PottedUpCount);
        Assert.All(result.Plants, p => Assert.Equal(places.IdOf("Kitchen"), p.PlaceId));
        Assert.False(form.Retrying);
    }

    [Fact]
    public async Task All_in_one_pot_makes_one_plant_with_the_count()
    {
        var propagation = Stored();
        var form = Form(propagation);
        form.Count = 3;
        form.Mode = PotUpKind.OnePot;

        var result = await form.SaveAsync();

        var plant = Assert.Single(result.Plants);
        Assert.Equal(3, plant.Count);
        Assert.Equal(3, propagation.PottedUpCount);
    }

    [Fact]
    public async Task In_with_a_plant_pots_it_together_with_it()
    {
        var into = new Plant { Nickname = "Big one", Genus = "Aloe" };
        plants.Plants[into.Id] = into;
        var propagation = Stored();
        var form = Form(propagation);
        form.Mode = PotUpKind.Into;
        form.IntoPlantId = into.Id;

        var result = await form.SaveAsync();

        var made = Assert.Single(result.Plants);
        Assert.Equal(into.Id, made.PottedWithPlantId);
        Assert.Equal(1, propagation.PottedUpCount);
    }

    [Fact]
    public async Task A_check_that_fails_before_anything_is_saved_leaves_the_form_free()
    {
        var gone = new Plant { Nickname = "Gone", Genus = "Aloe", Status = PlantStatus.Died };
        plants.Plants[gone.Id] = gone;
        var propagation = Stored();
        var form = Form(propagation);
        form.Mode = PotUpKind.Into;
        form.IntoPlantId = gone.Id;

        await Assert.ThrowsAsync<InvalidOperationException>(() => form.SaveAsync());

        Assert.False(form.Retrying);
        form.Mode = PotUpKind.Each;
        form.Count = 2;
        var result = await form.SaveAsync();

        Assert.Equal(2, result.Plants.Count);
        Assert.Equal(2, PlantsFrom(propagation).Count);
    }

    [Fact]
    public async Task A_retry_keeps_the_mode_its_plants_were_made_for()
    {
        propagationRepository = new FailingOncePropagations(propagations);
        var propagation = Stored();
        var form = Form(propagation);
        form.Count = 2;
        form.Mode = PotUpKind.OnePot;

        await Assert.ThrowsAsync<InvalidOperationException>(() => form.SaveAsync());
        Assert.True(form.Retrying);
        // Another device marked some as failed, so one pot would no longer be offered
        var reloaded = propagation.Copy();
        reloaded.FailedCount = 2;
        form.Propagation = reloaded;

        Assert.Equal(PotUpKind.OnePot, form.EffectiveMode);
        var result = await form.SaveAsync();

        var made = Assert.Single(result.Plants);
        Assert.Equal(2, made.Count);
        Assert.Single(PlantsFrom(propagation));
    }

    [Fact]
    public async Task A_plant_that_fails_to_save_can_be_tried_again_without_doubling_anything()
    {
        plantRepository = new FailingOncePlants(plants, failOnSave: 2);
        var propagation = Stored();
        var form = Form(propagation);
        form.Count = 3;

        await Assert.ThrowsAsync<InvalidOperationException>(() => form.SaveAsync());

        Assert.Equal(0, propagation.PottedUpCount);
        Assert.Equal(0, propagations.Propagations[propagation.Id].PottedUpCount);
        Assert.True(form.Retrying);
        form.Count = 1;
        form.Mode = PotUpKind.OnePot;
        Assert.Equal(3, form.Count);
        Assert.Equal(PotUpKind.Each, form.Mode);

        var result = await form.SaveAsync();

        Assert.Null(result.LaterError);
        Assert.False(form.Retrying);
        var made = PlantsFrom(propagation);
        Assert.Equal(3, made.Count);
        Assert.All(made, p => Assert.Equal(1, Created(p)));
        Assert.Equal(3, propagation.PottedUpCount);
        Assert.Equal(3, propagations.Propagations[propagation.Id].PottedUpCount);
        Assert.Equal(3, result.Plants.Count);
        Assert.Equal(made.Select(p => p.Id).Order(), result.Plants.Select(p => p.Id).Order());
    }

    [Fact]
    public async Task A_propagation_that_fails_to_save_is_counted_once_after_a_retry()
    {
        propagationRepository = new FailingOncePropagations(propagations);
        var propagation = Stored();
        var form = Form(propagation);
        form.Count = 2;

        await Assert.ThrowsAsync<InvalidOperationException>(() => form.SaveAsync());

        Assert.Equal(0, propagation.PottedUpCount);
        Assert.Equal(PropagationStage.Started, propagation.Stage);
        Assert.True(form.Retrying);
        Assert.Equal(2, PlantsFrom(propagation).Count);

        await form.SaveAsync();

        Assert.Equal(2, propagation.PottedUpCount);
        Assert.Equal(2, propagations.Propagations[propagation.Id].PottedUpCount);
        var made = PlantsFrom(propagation);
        Assert.Equal(2, made.Count);
        Assert.All(made, p => Assert.Equal(1, Created(p)));
        Assert.Equal(["Corms 1", "Corms 2"], made.Select(p => p.Nickname).Order());
    }

    [Fact]
    public async Task A_history_line_that_fails_after_the_count_is_a_later_error()
    {
        timelineRepository = new FailingOnceTimeline(timeline,
            e => e.SubjectType == SubjectType.Propagation && e.Kind == TimelineKind.Change);
        var propagation = Stored();
        var form = Form(propagation);
        form.Count = 2;

        var result = await form.SaveAsync();

        Assert.NotNull(result.LaterError);
        Assert.False(form.Retrying);
        Assert.Equal(2, propagation.PottedUpCount);
        var made = PlantsFrom(propagation);
        Assert.Equal(2, made.Count);
        Assert.All(made, p => Assert.Equal(1, Created(p)));
        // The page finds the plants by these, since the result has none
        Assert.Equal(made.Select(p => p.Id).Order(), form.PlantIds.Order());
    }

    [Fact]
    public async Task A_reload_during_the_save_does_not_turn_a_counted_pot_up_into_a_failed_one()
    {
        var propagation = Stored();
        var stale = propagation.Copy();
        PotUpForm? form = null;
        timelineRepository = new FailingOnceTimeline(timeline, e =>
        {
            // A sync reloads the page while the plants are saved, from before the count was stored
            if (e.SubjectType == SubjectType.Plant)
                form!.Propagation = stale;
            return e.SubjectType == SubjectType.Propagation && e.Kind == TimelineKind.Change;
        });
        form = Form(propagation);
        form.Count = 2;

        var result = await form.SaveAsync();

        Assert.NotNull(result.LaterError);
        Assert.False(form.Retrying);
        Assert.False(form.HasStoredPlants);
        Assert.Equal(2, propagations.Propagations[propagation.Id].PottedUpCount);
        Assert.Equal(2, PlantsFrom(propagation).Count);
    }

    [Fact]
    public async Task A_retry_whose_count_no_longer_fits_is_let_go_and_writes_over_the_stored_plants()
    {
        propagationRepository = new FailingOncePropagations(propagations);
        var propagation = Stored();
        var form = Form(propagation);
        form.Count = 3;

        await Assert.ThrowsAsync<InvalidOperationException>(() => form.SaveAsync());
        Assert.True(form.Retrying);
        var firstIds = form.PlantIds.ToList();

        // Another device marked two as failed, so three no longer fit
        var reloaded = propagation.Copy();
        reloaded.FailedCount = 2;
        form.Propagation = reloaded;

        Assert.False(form.Retrying);
        Assert.True(form.HasStoredPlants);
        Assert.False(form.CanSave);
        form.Count = 2;
        Assert.True(form.CanSave);

        var result = await form.SaveAsync();

        Assert.Null(result.LaterError);
        Assert.False(form.HasStoredPlants);
        Assert.Equal(2, propagations.Propagations[propagation.Id].PottedUpCount);
        Assert.Equal(firstIds.Take(2), result.Plants.Select(p => p.Id));
        Assert.All(result.Plants, p => Assert.Equal(1, Created(p)));
    }
}

internal sealed class FailingOncePlants(IPlantRepository inner, int failOnSave) : IPlantRepository
{
    private int saves;

    public Task<IReadOnlyList<Plant>> GetAllAsync() => inner.GetAllAsync();

    public Task<Plant?> GetAsync(Guid id) => inner.GetAsync(id);

    public Task<bool> IsDeletedAsync(Guid id) => inner.IsDeletedAsync(id);

    public Task SaveAsync(Plant plant)
    {
        if (++saves == failOnSave)
            throw new InvalidOperationException("Offline");
        return inner.SaveAsync(plant);
    }

    public Task DeleteAsync(Guid id) => inner.DeleteAsync(id);
}

internal sealed class FailingOncePropagations(IPropagationRepository inner) : IPropagationRepository
{
    private bool failed;

    public Task<IReadOnlyList<Propagation>> GetAllAsync() => inner.GetAllAsync();

    public Task<Propagation?> GetAsync(Guid id) => inner.GetAsync(id);

    public Task<bool> IsDeletedAsync(Guid id) => inner.IsDeletedAsync(id);

    public Task SaveAsync(Propagation propagation)
    {
        if (!failed)
        {
            failed = true;
            throw new InvalidOperationException("Offline");
        }
        return inner.SaveAsync(propagation);
    }

    public Task DeleteAsync(Guid id) => inner.DeleteAsync(id);
}

internal sealed class FailingOnceTimeline(ITimelineRepository inner, Func<TimelineEntry, bool> fails) : ITimelineRepository
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
        if (!failed && fails(entry))
        {
            failed = true;
            throw new InvalidOperationException("Offline");
        }

        return inner.AddAsync(entry);
    }

    public Task UpdateAsync(TimelineEntry entry) => inner.UpdateAsync(entry);

    public Task DeleteAsync(Guid id) => inner.DeleteAsync(id);
}
