using Stikling.Core.Models;
using Stikling.Core.Pests;

namespace Stikling.Core.Tests;

public class PestTests
{
    private static readonly DateOnly Today = new(2026, 9, 20);

    private static Plant Plant(string name, string? location = null, PlantStatus status = PlantStatus.Active) =>
        new() { Nickname = name, Location = location, Status = status };

    private static PestCase Case(
        PestScope scope = PestScope.Everywhere,
        string? room = null,
        int interval = 4,
        PestCaseStatus status = PestCaseStatus.Active,
        DateOnly? started = null) =>
        new()
        {
            Scope = scope,
            Room = room,
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

        Assert.Equal(2, PestService.PlantsIn(item, plants).Count);
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

        var covered = PestService.PlantsIn(item, plants).Select(p => p.DisplayName);

        Assert.Equal(["Alocasia", "Monstera"], covered);
    }

    [Fact]
    public void A_plant_moved_into_the_room_is_covered_from_then_on()
    {
        var item = Case(PestScope.Room, room: "Living room");
        var plant = Plant("Basil", "Kitchen");

        Assert.Empty(PestService.PlantsIn(item, [plant]));

        plant.Location = "Living room / Windowsill";

        Assert.Single(PestService.PlantsIn(item, [plant]));
    }

    [Fact]
    public void Picked_plants_covers_only_the_ones_chosen()
    {
        var monstera = Plant("Monstera");
        var item = Case(PestScope.PickedPlants);
        item.PlantIds = [monstera.Id];

        var covered = PestService.PlantsIn(item, [monstera, Plant("Basil")]);

        Assert.Equal("Monstera", Assert.Single(covered).DisplayName);
    }

    [Fact]
    public void Plants_that_are_gone_are_not_covered()
    {
        var item = Case();
        var died = Plant("Calathea", status: PlantStatus.Died);
        var deleted = Plant("Fern");
        deleted.DeletedAt = DateTimeOffset.UtcNow;

        Assert.Empty(PestService.PlantsIn(item, [died, deleted]));
    }

    // When the next treatment is due

    [Fact]
    public void With_no_treatments_the_first_one_is_due_an_interval_after_the_case_started()
    {
        var item = Case(interval: 4, started: Today.AddDays(-2));

        Assert.Equal(Today.AddDays(2), PestService.NextDue(item, []));
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

        Assert.Equal(Today.AddDays(3), PestService.NextDue(item, [other]));
    }

    [Fact]
    public void A_resolved_case_is_never_due()
    {
        var item = Case(status: PestCaseStatus.Resolved);

        Assert.Null(PestService.NextDue(item, []));
    }

    [Fact]
    public void A_case_being_monitored_is_still_due()
    {
        var item = Case(status: PestCaseStatus.Monitoring, interval: 4, started: Today.AddDays(-4));

        Assert.Equal(Today, PestService.NextDue(item, []));
    }

    // What Today shows

    [Fact]
    public void Due_lists_the_most_overdue_first_and_leaves_out_what_is_not_due()
    {
        var overdue = Case(interval: 2, started: Today.AddDays(-9));
        var dueToday = Case(interval: 4, started: Today.AddDays(-4));
        var later = Case(interval: 4, started: Today.AddDays(-1));

        var views = PestService.Describe([overdue, dueToday, later], [], [], Today);
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

        Assert.Empty(PestService.Describe([item], [], [], Today));
    }

    // The badge on a plant

    [Fact]
    public void A_plant_shows_the_open_cases_that_cover_it()
    {
        var plant = Plant("Monstera", "Living room");
        var mites = Case(PestScope.Room, room: "Living room");
        var elsewhere = Case(PestScope.Room, room: "Kitchen");
        var done = Case(status: PestCaseStatus.Resolved);

        var found = PestService.CasesFor(plant, [mites, elsewhere, done]);

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
