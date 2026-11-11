using Stikling.Core.Models;
using Stikling.Core.Pests;
using Stikling.Core.Rooms;

namespace Stikling.Core.Tests;

public class TreatmentChecklistTests
{
    private readonly FakePlaceRepository places = new();

    private Places Places => places.Current;

    private Plant Plant(string name, string? location = null, PlantStatus status = PlantStatus.Active) =>
        new() { Nickname = name, PlaceId = places.IdOf(location), Status = status };

    private PestCase Case(PestScope scope = PestScope.Everywhere, string? room = null, params Plant[] picked) =>
        new()
        {
            Scope = scope,
            PlaceId = places.IdOf(room),
            PlantIds = [.. picked.Select(p => p.Id)]
        };

    private static PestTreatment Treatment(PestCase item, List<Guid>? ticked = null) =>
        new() { CaseId = item.Id, TreatedPlantIds = ticked };

    private static List<string> Names(ChecklistGroup group) => [.. group.Rows.Select(r => r.Plant.DisplayName)];

    [Fact]
    public void A_picked_plants_case_lists_those()
    {
        var monstera = Plant("Monstera", "Living room");
        var basil = Plant("Basil", "Kitchen");
        var fern = Plant("Fern", "Kitchen");
        var item = Case(PestScope.PickedPlants, null, monstera, basil);

        var groups = TreatmentChecklist.Rows(item, Treatment(item, []), [monstera, basil, fern], Places);

        Assert.Equal(["Monstera", "Basil"], groups.SelectMany(Names).OrderByDescending(n => n));
        Assert.Equal(2, groups.Sum(g => g.Rows.Count));
    }

    [Fact]
    public void A_room_case_lists_the_plants_in_the_room_grouped_under_its_name()
    {
        var monstera = Plant("Monstera", "Living room");
        var alocasia = Plant("Alocasia", "Living room / On top of the PC");
        var basil = Plant("Basil", "Kitchen");
        var item = Case(PestScope.Room, "Living room");

        var groups = TreatmentChecklist.Rows(item, Treatment(item, []), [monstera, alocasia, basil], Places);

        var group = Assert.Single(groups);
        Assert.Equal("Living room", group.Name);
        Assert.False(group.FoundOn);
        Assert.Equal(["Alocasia", "Monstera"], Names(group));
    }

    [Fact]
    public void Plants_found_on_come_first_in_a_group_of_their_own()
    {
        var monstera = Plant("Monstera", "Living room");
        var basil = Plant("Basil", "Kitchen");
        var fern = Plant("Fern", "Living room");
        var item = Case();
        item.FoundOnPlantIds = [fern.Id];

        var groups = TreatmentChecklist.Rows(item, Treatment(item, []), [monstera, basil, fern], Places);

        Assert.True(groups[0].FoundOn);
        Assert.Equal(["Fern"], Names(groups[0]));
        Assert.Equal(["Living room", "Kitchen"], groups.Skip(1).Select(g => g.Name).OrderByDescending(n => n));
        Assert.DoesNotContain("Fern", groups.Skip(1).SelectMany(Names));
    }

    [Fact]
    public void Plants_with_no_room_come_last()
    {
        var monstera = Plant("Monstera", "Living room");
        var loose = Plant("Aloe");
        var item = Case();

        var groups = TreatmentChecklist.Rows(item, Treatment(item, []), [loose, monstera], Places);

        Assert.Equal("Living room", groups[0].Name);
        Assert.Equal(["Aloe"], Names(groups[^1]));
    }

    [Fact]
    public void Plants_that_are_dead_or_given_away_are_not_shown()
    {
        var monstera = Plant("Monstera", "Living room");
        var dead = Plant("Dead", "Living room", PlantStatus.Died);
        var gone = Plant("Gone", "Living room", PlantStatus.GivenAway);
        var item = Case();

        var groups = TreatmentChecklist.Rows(item, Treatment(item, []), [monstera, dead, gone], Places);

        Assert.Equal(["Monstera"], groups.SelectMany(Names));
    }

    [Fact]
    public void A_row_says_whether_it_is_ticked()
    {
        var monstera = Plant("Monstera", "Living room");
        var basil = Plant("Basil", "Living room");
        var item = Case();

        var groups = TreatmentChecklist.Rows(item, Treatment(item, [basil.Id]), [monstera, basil], Places);

        var rows = groups.SelectMany(g => g.Rows).ToDictionary(r => r.Plant.DisplayName, r => r.Ticked);
        Assert.True(rows["Basil"]);
        Assert.False(rows["Monstera"]);
    }

    [Fact]
    public void Toggle_makes_a_list_from_null()
    {
        var item = Case();
        var treatment = Treatment(item);
        var id = Guid.NewGuid();

        TreatmentChecklist.Toggle(treatment, id);

        Assert.Equal([id], treatment.TreatedPlantIds);
    }

    [Fact]
    public void Toggling_twice_takes_the_tick_off_again()
    {
        var item = Case();
        var treatment = Treatment(item);
        var id = Guid.NewGuid();

        TreatmentChecklist.Toggle(treatment, id);
        TreatmentChecklist.Toggle(treatment, id);

        Assert.NotNull(treatment.TreatedPlantIds);
        Assert.Empty(treatment.TreatedPlantIds);
    }

    [Fact]
    public void Progress_counts_only_the_plants_shown()
    {
        var monstera = Plant("Monstera", "Living room");
        var basil = Plant("Basil", "Kitchen");
        var item = Case();
        var left = Guid.NewGuid();
        var treatment = Treatment(item, [monstera.Id, left]);

        var groups = TreatmentChecklist.Rows(item, treatment, [monstera, basil], Places);

        Assert.Equal((1, 2), TreatmentChecklist.Progress(groups));
        Assert.Equal((1, 2), TreatmentChecklist.Progress(treatment, PestService.PlantsIn(item, [monstera, basil], Places)));
    }

    [Fact]
    public void A_treatment_without_a_list_has_nothing_ticked()
    {
        var monstera = Plant("Monstera", "Living room");
        var item = Case();

        Assert.Equal((0, 1), TreatmentChecklist.Progress(Treatment(item), [monstera]));
    }

    [Fact]
    public void Copy_does_not_share_the_list()
    {
        var item = Case();
        var treatment = Treatment(item, [Guid.NewGuid()]);

        var copy = treatment.Copy();
        copy.TreatedPlantIds!.Clear();

        Assert.Single(treatment.TreatedPlantIds!);
        Assert.Null(Treatment(item).Copy().TreatedPlantIds);
    }
}
