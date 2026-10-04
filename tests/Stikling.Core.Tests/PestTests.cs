using Stikling.Core.Models;
using Stikling.Core.Pests;
using Stikling.Core.Rooms;

namespace Stikling.Core.Tests;

public class PestTests
{
    private static readonly DateOnly Today = new(2026, 9, 20);

    private readonly FakePlaceRepository places = new();

    private Places Places => places.Current;

    private Plant Plant(string name, string? location = null, PlantStatus status = PlantStatus.Active) =>
        new() { Nickname = name, PlaceId = places.IdOf(location), Status = status };

    private PestCase Case(
        PestScope scope = PestScope.Everywhere,
        string? room = null,
        int interval = 4,
        PestCaseStatus status = PestCaseStatus.Active,
        DateOnly? started = null) =>
        new()
        {
            Scope = scope,
            PlaceId = places.IdOf(room),
            IntervalDays = interval,
            Status = status,
            StartedOn = started ?? Today.AddDays(-10)
        };

    private static PestTreatment Treatment(PestCase item, DateOnly on, DateOnly? nextDue = null) =>
        new() { CaseId = item.Id, OccurredOn = on, NextDueOn = nextDue };

    // Which plants a case covers

    [Fact]
    public void Everywhere_covers_the_whole_collection()
    {
        var item = Case();
        var plants = new[] { Plant("Monstera", "Living room"), Plant("Basil", "Kitchen") };

        Assert.Equal(2, PestService.PlantsIn(item, plants, Places).Count);
    }

    [Fact]
    public void A_room_covers_the_spots_inside_it()
    {
        var item = Case(PestScope.Room, room: "Living room");
        var plants = new[]
        {
            Plant("Monstera", "Living room"),
            Plant("Alocasia", "Living room / On top of the PC"),
            Plant("Basil", "Kitchen")
        };

        var covered = PestService.PlantsIn(item, plants, Places).Select(p => p.DisplayName);

        Assert.Equal(["Alocasia", "Monstera"], covered);
    }

    [Fact]
    public void A_plant_moved_into_the_room_is_covered_from_then_on()
    {
        var item = Case(PestScope.Room, room: "Living room");
        var plant = Plant("Basil", "Kitchen");

        Assert.Empty(PestService.PlantsIn(item, [plant], Places));

        plant.PlaceId = places.IdOf("Living room / Windowsill");

        Assert.Single(PestService.PlantsIn(item, [plant], Places));
    }

    [Fact]
    public void Picked_plants_covers_only_the_ones_chosen()
    {
        var monstera = Plant("Monstera");
        var item = Case(PestScope.PickedPlants);
        item.PlantIds = [monstera.Id];

        var covered = PestService.PlantsIn(item, [monstera, Plant("Basil")], Places);

        Assert.Equal("Monstera", Assert.Single(covered).DisplayName);
    }

    [Fact]
    public void Plants_that_are_gone_are_not_covered()
    {
        var item = Case();
        var died = Plant("Calathea", status: PlantStatus.Died);
        var deleted = Plant("Fern");
        deleted.DeletedAt = DateTimeOffset.UtcNow;

        Assert.Empty(PestService.PlantsIn(item, [died, deleted], Places));
    }

    // When the next treatment is due

    [Fact]
    public void With_no_treatments_the_first_one_is_due_from_the_day_the_pests_were_found()
    {
        var item = Case(interval: 4, started: Today.AddDays(-2));

        var view = PestService.Describe(item, [], [], Places, Today);

        Assert.Equal(Today.AddDays(-2), view.NextDue);
        Assert.True(view.Untreated);
        Assert.Single(PestService.Due([view]));
    }

    [Fact]
    public void A_trap_count_is_not_a_first_treatment()
    {
        var item = Case(interval: 4, started: Today.AddDays(-2));

        var view = PestService.Describe(item, [], [Trap(item, Today, 3)], Places, Today);

        Assert.True(view.Untreated);
        Assert.Equal(Today.AddDays(-2), view.NextDue);
    }

    [Fact]
    public void Once_treated_a_case_is_no_longer_untreated()
    {
        var item = Case(interval: 4, started: Today.AddDays(-2));

        var view = PestService.Describe(item, [], [Treatment(item, Today)], Places, Today);

        Assert.False(view.Untreated);
        Assert.Equal(Today.AddDays(4), view.NextDue);
    }

    [Fact]
    public void A_case_being_watched_with_nothing_logged_waits_a_week_for_its_check()
    {
        var item = Case(status: PestCaseStatus.Monitoring, started: Today.AddDays(-2));

        var view = PestService.Describe(item, [], [], Places, Today);

        Assert.False(view.Untreated);
        Assert.Equal(Today.AddDays(5), view.NextDue);
    }

    [Fact]
    public void After_a_treatment_the_next_is_due_an_interval_later()
    {
        var item = Case(interval: 4);
        var treatment = Treatment(item, Today.AddDays(-1));

        Assert.Equal(Today.AddDays(3), PestService.NextDue(item, [treatment]));
    }

    [Fact]
    public void A_date_set_on_the_treatment_beats_the_interval()
    {
        var item = Case(interval: 4);
        var treatment = Treatment(item, Today.AddDays(-1), nextDue: Today.AddDays(6));

        Assert.Equal(Today.AddDays(6), PestService.NextDue(item, [treatment]));
    }

    [Fact]
    public void The_newest_treatment_is_the_one_that_counts()
    {
        var item = Case(interval: 4);
        var older = Treatment(item, Today.AddDays(-9));
        var newer = Treatment(item, Today.AddDays(-2));

        Assert.Equal(Today.AddDays(2), PestService.NextDue(item, [older, newer]));
    }

    [Fact]
    public void Another_cases_treatments_are_ignored()
    {
        var item = Case(interval: 4, started: Today.AddDays(-1));
        var other = Treatment(Case(), Today);

        Assert.Equal(Today.AddDays(-1), PestService.NextDue(item, [other]));
    }

    [Fact]
    public void A_resolved_case_is_never_due()
    {
        var item = Case(status: PestCaseStatus.Resolved);

        Assert.Null(PestService.NextDue(item, []));
    }

    [Fact]
    public void A_case_being_watched_is_due_a_check_a_week_after_the_last_treatment()
    {
        var item = Case(status: PestCaseStatus.Monitoring, interval: 4, started: Today.AddDays(-20));
        var last = Treatment(item, Today.AddDays(-7), nextDue: Today.AddDays(-3));

        var view = PestService.Describe(item, [], [last], Places, Today);

        Assert.Equal(Today, view.NextDue);
        Assert.True(view.IsCheck);
        Assert.Single(PestService.Due([view]));
    }

    [Fact]
    public void Logging_a_check_moves_the_next_one_a_week_along()
    {
        var item = Case(status: PestCaseStatus.Monitoring, started: Today.AddDays(-20));
        var treated = Treatment(item, Today.AddDays(-9));
        var check = PestService.StartCheck(item, Today.AddDays(-2));

        Assert.Equal(Today.AddDays(5), PestService.NextDue(item, [treated, check]));
    }

    [Fact]
    public void A_check_is_not_offered_as_the_last_recipe()
    {
        var item = Case();
        var treated = Treatment(item, Today.AddDays(-9));
        treated.What = "Neem oil";
        var check = PestService.StartCheck(item, Today.AddDays(-2));

        Assert.Equal("Neem oil", PestService.StartTreatment(item, [treated, check], Today).What);
    }

    [Fact]
    public void A_check_only_has_a_date_and_notes()
    {
        var check = PestService.StartCheck(Case(), Today);
        check.What = "Neem oil";

        Assert.Contains("A check only has a date and notes.", check.Validate(Today));
    }

    // Sticky traps

    [Theory]
    [InlineData(Pest.Thrips, true)]
    [InlineData(Pest.FungusGnats, true)]
    [InlineData(Pest.Whitefly, true)]
    [InlineData(Pest.SpiderMites, false)]
    [InlineData(Pest.Mealybugs, false)]
    [InlineData(Pest.Scale, false)]
    [InlineData(Pest.Aphids, false)]
    [InlineData(Pest.Other, false)]
    public void Only_flying_pests_are_caught_on_a_sticky_trap(Pest pest, bool caught)
    {
        Assert.Equal(caught, pest.CaughtOnTrap());
    }

    private static PestTreatment Trap(PestCase item, DateOnly on, int? onTrap, bool newTrap = false)
    {
        var count = PestService.StartTrapCount(item, on);
        count.OnTrap = onTrap;
        count.NewTrap = newTrap;
        return count;
    }

    [Fact]
    public void The_first_count_is_spread_over_the_days_since_the_case_started()
    {
        var item = Case(started: Today.AddDays(-14));

        var caught = Assert.Single(PestService.Catches(item, [Trap(item, Today, 20)]));

        Assert.Equal(20, caught.Caught);
        Assert.Equal(14, caught.Days);
        Assert.Equal(10, caught.PerWeek);
    }

    [Fact]
    public void A_count_on_the_same_trap_only_counts_what_is_new()
    {
        var item = Case(started: Today.AddDays(-14));
        var counts = new[] { Trap(item, Today.AddDays(-7), 12), Trap(item, Today, 15) };

        var caught = PestService.Catches(item, counts);

        Assert.Equal([12, 3], caught.Select(c => c.Caught));
        Assert.Equal([7, 7], caught.Select(c => c.Days));
    }

    [Fact]
    public void A_new_trap_after_counting_starts_the_next_count_from_nothing()
    {
        var item = Case(started: Today.AddDays(-14));
        var counts = new[] { Trap(item, Today.AddDays(-7), 12, newTrap: true), Trap(item, Today, 5) };

        Assert.Equal([12, 5], PestService.Catches(item, counts).Select(c => c.Caught));
    }

    [Fact]
    public void Putting_up_a_trap_without_a_count_is_where_the_days_start()
    {
        var item = Case(started: Today.AddDays(-20));
        var counts = new[] { Trap(item, Today.AddDays(-10), null, newTrap: true), Trap(item, Today, 4) };

        var caught = Assert.Single(PestService.Catches(item, counts));

        Assert.Equal(10, caught.Days);
    }

    [Fact]
    public void A_lower_count_means_the_trap_was_swapped_and_all_of_it_is_new()
    {
        var item = Case(started: Today.AddDays(-14));
        var counts = new[] { Trap(item, Today.AddDays(-7), 12), Trap(item, Today, 4) };

        Assert.Equal(4, PestService.Catches(item, counts)[1].Caught);
    }

    [Fact]
    public void A_count_on_the_day_the_case_started_has_no_weekly_rate()
    {
        var item = Case(started: Today);

        Assert.Null(Assert.Single(PestService.Catches(item, [Trap(item, Today, 8)])).PerWeek);
    }

    [Fact]
    public void A_trap_count_never_moves_the_next_treatment()
    {
        var item = Case(interval: 4, started: Today.AddDays(-10));
        var treated = Treatment(item, Today.AddDays(-3));

        var view = PestService.Describe(item, [], [treated, Trap(item, Today, 8)], Places, Today);

        Assert.Equal(Today.AddDays(1), view.NextDue);
        Assert.Same(treated, view.Last);
    }

    [Fact]
    public void On_a_case_being_watched_a_trap_count_is_the_weekly_check()
    {
        var item = Case(status: PestCaseStatus.Monitoring, started: Today.AddDays(-20));
        var treated = Treatment(item, Today.AddDays(-9));

        Assert.Equal(Today.AddDays(5), PestService.NextDue(item, [treated, Trap(item, Today.AddDays(-2), 0)]));
    }

    [Fact]
    public void Only_putting_up_a_new_trap_is_not_a_check()
    {
        var item = Case(status: PestCaseStatus.Monitoring, started: Today.AddDays(-20));
        var treated = Treatment(item, Today.AddDays(-9));

        Assert.Equal(Today.AddDays(-2), PestService.NextDue(item, [treated, Trap(item, Today.AddDays(-2), null, newTrap: true)]));
    }

    [Fact]
    public void A_trap_count_needs_a_count_or_a_new_trap()
    {
        var item = Case();

        Assert.Contains("Fill in how many are on the trap, or that a new one went up.", Trap(item, Today, null).Validate(Today));
        Assert.Empty(Trap(item, Today, 0).Validate(Today));
        Assert.Empty(Trap(item, Today, null, newTrap: true).Validate(Today));
    }

    [Fact]
    public void A_trap_count_has_nothing_from_a_treatment()
    {
        var count = Trap(Case(), Today, 3);
        count.What = "Neem oil";

        Assert.Contains("A trap count only has the count, a new trap, a date and notes.", count.Validate(Today));
    }

    // What Today shows

    [Fact]
    public void Due_lists_the_most_overdue_first_and_leaves_out_what_is_not_due()
    {
        var overdue = Case(interval: 2, started: Today.AddDays(-20));
        var dueToday = Case(interval: 4, started: Today.AddDays(-20));
        var later = Case(interval: 4, started: Today.AddDays(-20));
        var treatments = new[]
        {
            Treatment(overdue, Today.AddDays(-9)),
            Treatment(dueToday, Today.AddDays(-4)),
            Treatment(later, Today.AddDays(-1))
        };

        var views = PestService.Describe([overdue, dueToday, later], [], treatments, Places, Today);
        var due = PestService.Due(views);

        Assert.Equal([overdue.Id, dueToday.Id], due.Select(v => v.Case.Id));
        Assert.Equal(-7, due[0].DaysUntilDue);
        Assert.Equal(0, due[1].DaysUntilDue);
    }

    [Fact]
    public void A_deleted_case_is_left_out_altogether()
    {
        var item = Case(interval: 1, started: Today.AddDays(-5));
        item.DeletedAt = DateTimeOffset.UtcNow;

        Assert.Empty(PestService.Describe([item], [], [], Places, Today));
    }

    // The badge on a plant

    [Fact]
    public void A_plant_shows_the_open_cases_that_cover_it()
    {
        var plant = Plant("Monstera", "Living room");
        var mites = Case(PestScope.Room, room: "Living room");
        var elsewhere = Case(PestScope.Room, room: "Kitchen");
        var done = Case(status: PestCaseStatus.Resolved);

        var found = PestService.CasesFor(plant, [mites, elsewhere, done], Places);

        Assert.Equal(mites.Id, Assert.Single(found).Id);
    }

    // Logging a treatment

    [Fact]
    public void A_new_treatment_offers_what_was_used_last_time()
    {
        var item = Case();
        var last = Treatment(item, Today.AddDays(-4));
        last.What = "Alcohol spray";

        var next = PestService.StartTreatment(item, [last], Today);

        Assert.Equal("Alcohol spray", next.What);
        Assert.Equal(Today, next.OccurredOn);
        Assert.Equal(item.Id, next.CaseId);
    }

    // Validation

    [Fact]
    public void A_case_cannot_start_in_the_future()
    {
        var item = Case(started: Today.AddDays(1));

        Assert.Contains("That start date is in the future.", item.Validate(Today));
    }

    [Fact]
    public void An_interval_of_zero_is_refused()
    {
        var item = Case(interval: 0);

        Assert.Contains("Treat at least every day, so the interval has to be 1 or more.", item.Validate(Today));
    }

    [Fact]
    public void A_room_case_needs_a_room()
    {
        var item = Case(PestScope.Room);

        Assert.Contains("Pick the room the case covers.", item.Validate(Today));
    }

    [Fact]
    public void A_picked_case_needs_at_least_one_plant()
    {
        var item = Case(PestScope.PickedPlants);

        Assert.Contains("Pick at least one plant.", item.Validate(Today));
    }

    [Fact]
    public void A_case_cannot_be_resolved_before_it_started()
    {
        var item = Case(started: Today.AddDays(-3));
        item.ResolvedOn = Today.AddDays(-5);

        Assert.Contains("A case can't be resolved before it started.", item.Validate(Today));
    }

    [Fact]
    public void A_valid_case_has_nothing_to_complain_about()
    {
        Assert.Empty(Case().Validate(Today));
    }

    [Fact]
    public void A_treatment_has_to_belong_to_a_case()
    {
        var treatment = new PestTreatment { OccurredOn = Today };

        Assert.Contains("A treatment has to belong to a case.", treatment.Validate(Today));
    }

    [Fact]
    public void The_next_treatment_cannot_be_due_before_this_one_happened()
    {
        var item = Case();
        var treatment = Treatment(item, Today, nextDue: Today.AddDays(-1));

        Assert.Contains("The next treatment can't be due before this one happened.", treatment.Validate(Today));
    }

    [Fact]
    public void Copying_a_case_does_not_share_its_plant_list()
    {
        var item = Case(PestScope.PickedPlants);
        item.PlantIds = [Guid.NewGuid()];

        var copy = item.Copy();
        copy.PlantIds.Add(Guid.NewGuid());

        Assert.Single(item.PlantIds);
        Assert.Equal(2, copy.PlantIds.Count);
    }
}
