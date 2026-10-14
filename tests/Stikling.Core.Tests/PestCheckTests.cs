using Stikling.Core.Models;
using Stikling.Core.Pests;
using Stikling.Core.Rooms;

namespace Stikling.Core.Tests;

public class PestCheckTests
{
    private static readonly DateOnly Today = new(2026, 9, 20);

    private readonly FakePlaceRepository places = new();

    private Places Places => places.Current;

    private Plant Plant(string name, string? location = null, PlantStatus status = PlantStatus.Active) =>
        new() { Nickname = name, PlaceId = places.IdOf(location), Status = status };

    private PestCase Picked(PestCaseStatus status = PestCaseStatus.Active, params Plant[] plants) =>
        new()
        {
            Scope = PestScope.PickedPlants,
            PlantIds = plants.Select(p => p.Id).ToList(),
            Status = status,
            StartedOn = Today.AddDays(-40)
        };

    private static PestTreatment OnCase(PestCase item, DateOnly on, PestTreatmentKind kind = PestTreatmentKind.Treated, int? onTrap = null) =>
        new() { CaseId = item.Id, OccurredOn = on, Kind = kind, OnTrap = onTrap };

    // Validation

    [Fact]
    public void A_check_on_a_plant_is_valid()
    {
        var check = PestService.StartPlantCheck(Plant("Monstera"), Today);

        Assert.Empty(check.Validate(Today));
        Assert.Equal(Guid.Empty, check.CaseId);
    }

    [Fact]
    public void An_entry_with_both_a_case_and_a_plant_is_an_error()
    {
        var check = PestService.StartPlantCheck(Plant("Monstera"), Today);
        check.CaseId = Guid.NewGuid();

        Assert.Contains("A check belongs to a case or a plant, not both.", check.Validate(Today));
    }

    [Fact]
    public void An_entry_with_neither_is_an_error()
    {
        var check = new PestTreatment { Kind = PestTreatmentKind.Checked, OccurredOn = Today };

        Assert.Contains("A treatment has to belong to a case.", check.Validate(Today));
    }

    [Fact]
    public void Only_a_check_can_be_logged_on_a_plant()
    {
        var treated = new PestTreatment { PlantId = Guid.NewGuid(), OccurredOn = Today, Kind = PestTreatmentKind.Treated };

        Assert.Contains("Only a check can be logged on a single plant.", treated.Validate(Today));
    }

    // When a plant was last looked over

    [Fact]
    public void A_plant_never_checked_has_no_date()
    {
        Assert.Null(PestService.LastChecked(Plant("Monstera"), [], [], Places));
    }

    [Fact]
    public void Its_own_check_counts()
    {
        var plant = Plant("Monstera");
        var checks = new[]
        {
            PestService.StartPlantCheck(plant, Today.AddDays(-9)),
            PestService.StartPlantCheck(plant, Today.AddDays(-3)),
            PestService.StartPlantCheck(Plant("Basil"), Today)
        };

        Assert.Equal(Today.AddDays(-3), PestService.LastChecked(plant, [], checks, Places));
        Assert.Equal(Today.AddDays(-3), PestService.ChecksFor(plant.Id, checks)[0].OccurredOn);
        Assert.Equal(2, PestService.ChecksFor(plant.Id, checks).Count);
    }

    [Fact]
    public void A_check_on_a_case_covering_the_plant_counts()
    {
        var plant = Plant("Monstera");
        var item = Picked(PestCaseStatus.Monitoring, plant);

        var last = PestService.LastChecked(plant, [item], [OnCase(item, Today.AddDays(-2), PestTreatmentKind.Checked)], Places);

        Assert.Equal(Today.AddDays(-2), last);
    }

    [Fact]
    public void A_trap_count_with_a_count_on_the_trap_counts()
    {
        var plant = Plant("Monstera");
        var item = Picked(PestCaseStatus.Active, plant);
        var counted = OnCase(item, Today.AddDays(-1), PestTreatmentKind.TrapCount, onTrap: 3);
        var newTrap = new PestTreatment { CaseId = item.Id, OccurredOn = Today, Kind = PestTreatmentKind.TrapCount, NewTrap = true };

        Assert.Equal(Today.AddDays(-1), PestService.LastChecked(plant, [item], [counted, newTrap], Places));
    }

    [Fact]
    public void A_treatment_does_not_count()
    {
        var plant = Plant("Monstera");
        var item = Picked(PestCaseStatus.Active, plant);

        Assert.Null(PestService.LastChecked(plant, [item], [OnCase(item, Today)], Places));
    }

    [Fact]
    public void A_case_not_covering_the_plant_does_not_count()
    {
        var plant = Plant("Monstera");
        var item = Picked(PestCaseStatus.Monitoring, Plant("Basil"));

        Assert.Null(PestService.LastChecked(plant, [item], [OnCase(item, Today, PestTreatmentKind.Checked)], Places));
    }

    [Fact]
    public void Deleted_entries_and_cases_are_ignored()
    {
        var plant = Plant("Monstera");
        var item = Picked(PestCaseStatus.Monitoring, plant);
        var onCase = OnCase(item, Today, PestTreatmentKind.Checked);
        var own = PestService.StartPlantCheck(plant, Today);
        own.DeletedAt = DateTimeOffset.UtcNow;

        Assert.Null(PestService.LastChecked(plant, [item], [own], Places));

        item.DeletedAt = DateTimeOffset.UtcNow;
        Assert.Null(PestService.LastChecked(plant, [item], [onCase], Places));
    }

    // The neighbours

    [Fact]
    public void Neighbours_are_the_other_plants_in_the_rooms_of_the_picked_ones_spots_included()
    {
        var sick = Plant("Monstera", "Living room");
        var inRoom = Plant("Alocasia", "Living room");
        var onSpot = Plant("Fern", "Living room / On top of the PC");
        var elsewhere = Plant("Basil", "Kitchen");
        var gone = Plant("Dead one", "Living room", PlantStatus.Died);
        var item = Picked(PestCaseStatus.Active, sick);

        var names = PestService.Neighbours(item, [sick, inRoom, onSpot, elsewhere, gone], [item], [], Places)
            .Select(n => n.Plant.DisplayName);

        Assert.Equal(["Alocasia", "Fern"], names.Order());
    }

    [Fact]
    public void Plants_the_case_already_covers_are_not_neighbours()
    {
        var one = Plant("Monstera", "Living room");
        var two = Plant("Alocasia", "Living room");
        var item = Picked(PestCaseStatus.Active, one, two);

        Assert.Empty(PestService.Neighbours(item, [one, two], [item], [], Places));
    }

    [Fact]
    public void A_picked_plant_without_a_place_adds_no_room()
    {
        var sick = Plant("Monstera");
        var other = Plant("Alocasia", "Living room");
        var item = Picked(PestCaseStatus.Active, sick);

        Assert.Empty(PestService.Neighbours(item, [sick, other], [item], [], Places));
    }

    [Theory]
    [InlineData(PestScope.Room)]
    [InlineData(PestScope.Everywhere)]
    public void A_case_that_is_not_on_picked_plants_has_none(PestScope scope)
    {
        var sick = Plant("Monstera", "Living room");
        var other = Plant("Alocasia", "Living room");
        var item = Picked(PestCaseStatus.Active, sick);
        item.Scope = scope;

        Assert.Empty(PestService.Neighbours(item, [sick, other], [item], [], Places));
    }

    [Fact]
    public void A_case_being_watched_has_none()
    {
        var sick = Plant("Monstera", "Living room");
        var other = Plant("Alocasia", "Living room");
        var item = Picked(PestCaseStatus.Monitoring, sick);

        Assert.Empty(PestService.Neighbours(item, [sick, other], [item], [], Places));
    }

    [Fact]
    public void Never_checked_comes_first_then_the_longest_ago()
    {
        var sick = Plant("Monstera", "Living room");
        var recent = Plant("A recent", "Living room");
        var older = Plant("B older", "Living room");
        var never = Plant("C never", "Living room");
        var item = Picked(PestCaseStatus.Active, sick);
        var checks = new[]
        {
            PestService.StartPlantCheck(recent, Today.AddDays(-1)),
            PestService.StartPlantCheck(older, Today.AddDays(-8))
        };

        var order = PestService.Neighbours(item, [sick, recent, older, never], [item], checks, Places)
            .Select(n => n.Plant.DisplayName);

        Assert.Equal(["C never", "B older", "A recent"], order);
    }

    // Clean for how long

    private PestCase Watched(DateOnly started) =>
        new() { Scope = PestScope.Everywhere, Status = PestCaseStatus.Monitoring, StartedOn = started };

    [Fact]
    public void Clean_since_is_the_last_treatment()
    {
        var item = Watched(Today.AddDays(-40));
        var treatments = new[]
        {
            OnCase(item, Today.AddDays(-30)),
            OnCase(item, Today.AddDays(-12)),
            OnCase(item, Today.AddDays(-5), PestTreatmentKind.Checked)
        };

        Assert.Equal(Today.AddDays(-12), PestService.CleanSince(item, treatments));
        Assert.Equal(12, PestService.Describe(item, [], treatments, Places, Today).CleanDays);
    }

    [Fact]
    public void Going_back_to_treating_restarts_the_count_without_a_new_treatment()
    {
        var item = Watched(Today.AddDays(-60));
        var treatments = new[] { OnCase(item, Today.AddDays(-40)) };

        item.Status = PestCaseStatus.Active;
        PestService.StatusChanged(item, PestCaseStatus.Monitoring, Today.AddDays(-3));
        item.Status = PestCaseStatus.Monitoring;
        PestService.StatusChanged(item, PestCaseStatus.Active, Today.AddDays(-2));

        Assert.Equal(Today.AddDays(-3), item.TreatingAgainOn);
        Assert.Equal(Today.AddDays(-3), PestService.CleanSince(item, treatments));
    }

    [Fact]
    public void A_treatment_after_going_back_to_treating_counts_from_the_treatment()
    {
        var item = Watched(Today.AddDays(-60));
        item.TreatingAgainOn = Today.AddDays(-10);
        var treatments = new[] { OnCase(item, Today.AddDays(-40)), OnCase(item, Today.AddDays(-6)) };

        Assert.Equal(Today.AddDays(-6), PestService.CleanSince(item, treatments));
    }

    [Fact]
    public void Opening_a_resolved_case_again_restarts_the_count()
    {
        var item = Watched(Today.AddDays(-60));
        item.Status = PestCaseStatus.Monitoring;
        PestService.StatusChanged(item, PestCaseStatus.Resolved, Today);

        Assert.Equal(Today, item.TreatingAgainOn);
    }

    [Fact]
    public void Starting_to_watch_or_resolving_leaves_the_count_alone()
    {
        var item = Watched(Today.AddDays(-60));
        PestService.StatusChanged(item, PestCaseStatus.Active, Today);
        item.Status = PestCaseStatus.Resolved;
        PestService.StatusChanged(item, PestCaseStatus.Monitoring, Today);

        Assert.Null(item.TreatingAgainOn);
    }

    [Fact]
    public void Without_a_treatment_it_counts_from_the_start()
    {
        var item = Watched(Today.AddDays(-9));

        Assert.Equal(Today.AddDays(-9), PestService.CleanSince(item, []));
        Assert.Equal(9, PestService.Describe(item, [], [], Places, Today).CleanDays);
    }

    [Fact]
    public void A_case_being_treated_is_not_counted()
    {
        var item = Watched(Today.AddDays(-9));
        item.Status = PestCaseStatus.Active;

        var view = PestService.Describe(item, [], [], Places, Today);

        Assert.Null(PestService.CleanSince(item, []));
        Assert.Null(view.CleanDays);
        Assert.False(view.ReadyToResolve);
    }

    [Fact]
    public void Four_weeks_clean_is_not_ready_without_a_recent_check()
    {
        var item = Watched(Today.AddDays(-28));
        var oldCheck = OnCase(item, Today.AddDays(-7), PestTreatmentKind.Checked);

        var view = PestService.Describe(item, [], [oldCheck], Places, Today);

        Assert.Equal(28, view.CleanDays);
        Assert.Equal(28, view.CleanTarget);
        Assert.False(view.ReadyToResolve);
        Assert.Empty(PestService.ReadyToResolve([view]));
    }

    [Fact]
    public void A_check_in_the_last_week_makes_it_ready()
    {
        var item = Watched(Today.AddDays(-28));
        var check = OnCase(item, Today.AddDays(-6), PestTreatmentKind.Checked);

        var view = PestService.Describe(item, [], [check], Places, Today);

        Assert.True(view.ReadyToResolve);
        Assert.Single(PestService.ReadyToResolve([view]));
    }

    [Fact]
    public void A_trap_count_counts_as_the_check()
    {
        var item = Watched(Today.AddDays(-30));
        var count = OnCase(item, Today.AddDays(-1), PestTreatmentKind.TrapCount, onTrap: 0);

        Assert.True(PestService.Describe(item, [], [count], Places, Today).ReadyToResolve);
    }

    [Fact]
    public void A_check_before_the_clean_count_started_does_not_count()
    {
        var item = Watched(Today.AddDays(-60));
        var treated = OnCase(item, Today.AddDays(-28));
        // Same day as the treatment is on or after the start; a day earlier is not
        var before = OnCase(item, Today.AddDays(-29), PestTreatmentKind.Checked);

        Assert.False(PestService.Describe(item, [], [treated, before], Places, Today).ReadyToResolve);
    }

    [Fact]
    public void The_weeks_in_settings_are_honoured()
    {
        var item = Watched(Today.AddDays(-14));
        var check = OnCase(item, Today, PestTreatmentKind.Checked);

        var two = PestService.Describe(item, [], [check], Places, Today, cleanWeeks: 2);
        var four = PestService.Describe(item, [], [check], Places, Today);

        Assert.True(two.ReadyToResolve);
        Assert.Equal(2, two.CleanWeeks);
        Assert.False(four.ReadyToResolve);
        Assert.Equal(28, four.CleanTarget);
    }
}
