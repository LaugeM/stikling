using Stikling.Core.Care;
using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Pots;
using Stikling.Core.Propagations;
using Stikling.Core.Rooms;
using Stikling.Core.Today;

namespace Stikling.Core.Tests;

public class PlantsInOnePotTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 19);
    private static readonly TimeProvider Time = new FixedTime(Now);

    private readonly FakePlantRepository plants = new();
    private readonly FakePropagationRepository propagations = new();
    private readonly FakeTimelineRepository timeline = new();
    private readonly PlantService service;

    public PlantsInOnePotTests() => service = new PlantService(plants, timeline, Time);

    private static string Label(Enum value) => value.ToString();

    private Plant Add(string name, int? count = null, Guid? inner = null)
    {
        var plant = new Plant { Nickname = name, Genus = "Aloe", Count = count, InnerPotId = inner };
        plants.Plants[plant.Id] = plant;
        return plant;
    }

    private IEnumerable<string> Texts(Plant plant) => timeline.Entries.Where(e => e.SubjectId == plant.Id).Select(e => e.Text);

    // The model

    [Fact]
    public void A_plant_has_one_in_the_pot_unless_it_has_a_count()
    {
        Assert.Equal(1, new Plant().HowMany);
        Assert.Equal(5, new Plant { Count = 5 }.HowMany);
    }

    [Fact]
    public void A_plant_potted_with_another_is_not_in_the_collection_but_keeps_its_status()
    {
        var plant = new Plant { Nickname = "Pup", PottedWithPlantId = Guid.NewGuid() };

        Assert.Equal(PlantStatus.Active, plant.Status);
        Assert.False(plant.InCollection);
        Assert.False(plant.IsOffered);
        Assert.True(new Plant().InCollection);
        Assert.False(new Plant { Status = PlantStatus.Died }.InCollection);
    }

    [Fact]
    public void Validate_wants_at_least_one_plant_and_no_plant_potted_with_itself()
    {
        var plant = new Plant { Nickname = "Aloe", Count = 0 };
        plant.PottedWithPlantId = plant.Id;

        var errors = plant.Validate(Today);

        Assert.Contains("There's at least one plant in a pot.", errors);
        Assert.Contains("A plant can't be potted with itself.", errors);
        Assert.Empty(new Plant { Nickname = "Aloe", Count = 3 }.Validate(Today));
    }

    [Fact]
    public void Duplicate_leaves_the_count_and_the_pot_behind()
    {
        var copy = new Plant { Nickname = "Aloe", Count = 4, PottedWithPlantId = Guid.NewGuid() }.Duplicate();

        Assert.Null(copy.Count);
        Assert.Null(copy.PottedWithPlantId);
    }

    [Fact]
    public void A_plant_potted_with_another_frees_its_pot_and_counts_in_no_pot_use()
    {
        var pot = new Pot { Name = "Clear", Group = PotGroup.Inner, Owned = 3 };
        var own = new Plant { Nickname = "A", InnerPotId = pot.Id };
        var potted = new Plant { Nickname = "B", InnerPotId = pot.Id, PottedWithPlantId = own.Id };

        Assert.False(potted.Uses(pot.Id));
        var use = Assert.Single(PotUse.List([pot], [own, potted]));
        Assert.Equal(1, use.InUse);
    }

    // Filter

    [Fact]
    public void Active_leaves_out_plants_potted_with_another_and_Gone_has_them_only_without_a_reason()
    {
        var a = new Plant { Nickname = "A" };
        var b = new Plant { Nickname = "B", PottedWithPlantId = a.Id };
        var died = new Plant { Nickname = "C", Status = PlantStatus.Died };
        Plant[] all = [a, b, died];

        Assert.Equal(["A"], new PlantFilter().Apply(all).Select(p => p.Nickname));
        Assert.Equal(["B", "C"], new PlantFilter(Status: StatusFilter.Gone).Apply(all).Select(p => p.Nickname).Order());
        Assert.Equal(["C"], new PlantFilter(Status: StatusFilter.Gone, GoneAs: PlantStatus.Died).Apply(all).Select(p => p.Nickname));
    }

    // Today

    [Fact]
    public void The_watering_round_skips_a_plant_potted_with_another()
    {
        var plants = new List<Plant>();
        var logs = new List<CareLog>();
        foreach (var name in new[] { "Own", "Potted" })
        {
            var plant = new Plant { Nickname = name };
            plants.Add(plant);
            for (var i = 0; i < 4; i++)
                logs.Add(new CareLog { PlantId = plant.Id, Kind = CareKind.Watered, OccurredOn = Today.AddDays(-10 - 7 * i) });
        }
        plants[1].PottedWithPlantId = plants[0].Id;

        var due = WateringRound.Due(plants, logs, [], null, [], Places.None, [], Hemisphere.Northern, Time, PutOffs.Empty());

        Assert.Equal(["Own"], due.Select(d => d.Plant.Nickname));
    }

    // Pot together

    [Fact]
    public async Task Potting_together_adds_the_counts_copies_the_pot_and_writes_both_histories()
    {
        var pot = Guid.NewGuid();
        var a = Add("Big aloe", 2, pot);
        a.Medium = GrowingMedium.Leca;
        var b = Add("Pup", 3);

        await service.PotTogetherAsync(b, a, Today, Label);

        Assert.Equal(5, a.Count);
        Assert.Equal(a.Id, b.PottedWithPlantId);
        Assert.Equal(Today, b.PottedWithSince);
        Assert.Equal(pot, b.InnerPotId);
        Assert.Equal(GrowingMedium.Leca, b.Medium);
        Assert.False(b.InCollection);
        Assert.Contains("Potted together with Big aloe", Texts(b));
        Assert.Contains("Pup potted in with it, 5 in the pot now", Texts(a));
        var line = timeline.Entries.Single(e => e.SubjectId == a.Id);
        Assert.Equal(b.Id, line.RelatedId);
        Assert.Equal(SubjectType.Plant, line.RelatedType);
    }

    [Fact]
    public async Task Potting_together_points_plants_already_in_the_pot_at_the_new_one()
    {
        var a = Add("A");
        var b = Add("B", 2);
        var c = Add("C");
        await service.PotTogetherAsync(c, b, Today, Label);

        await service.PotTogetherAsync(b, a, Today, Label);

        Assert.Equal(a.Id, c.PottedWithPlantId);
        Assert.Equal(a.Id, b.PottedWithPlantId);
        Assert.Equal(4, a.Count);
    }

    [Fact]
    public async Task Potting_together_refuses_itself_a_plant_outside_the_collection_and_a_future_date()
    {
        var a = Add("A");
        var b = Add("B");
        var dead = Add("Dead");
        dead.Status = PlantStatus.Died;

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PotTogetherAsync(a, a, Today, Label));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PotTogetherAsync(b, dead, Today, Label));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PotTogetherAsync(dead, a, Today, Label));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PotTogetherAsync(b, a, Today.AddDays(1), Label));
        Assert.Null(a.Count);
        Assert.Null(b.PottedWithPlantId);
    }

    [Fact]
    public async Task Taking_out_gives_the_count_and_a_pot_back()
    {
        var a = Add("A");
        var b = Add("B", 3);
        await service.PotTogetherAsync(b, a, Today, Label);
        var pot = Guid.NewGuid();

        await service.TakeOutAsync(b, Today, pot, Label);

        Assert.Null(a.Count);
        Assert.True(b.InCollection);
        Assert.Null(b.PottedWithSince);
        Assert.Equal(pot, b.InnerPotId);
        Assert.Null(b.OuterPotId);
        Assert.Contains("Taken out of A's pot", Texts(b));
        Assert.Contains("B taken out, 1 in the pot now", Texts(a));
    }

    [Fact]
    public async Task Taking_out_when_the_other_plant_is_deleted_only_writes_the_plant_s_own_history()
    {
        var a = Add("A");
        var b = Add("B");
        await service.PotTogetherAsync(b, a, Today, Label);
        a.DeletedAt = Now;

        await service.TakeOutAsync(b, Today, null, Label);

        Assert.True(b.InCollection);
        Assert.Contains("Taken out of the pot it shared", Texts(b));
    }

    [Fact]
    public async Task Taking_out_a_plant_potted_with_itself_does_not_loop()
    {
        var b = Add("B");
        b.PottedWithPlantId = b.Id;

        await service.TakeOutAsync(b, Today, null, Label);

        Assert.True(b.InCollection);
    }

    // Some of them

    [Fact]
    public async Task Some_dying_or_going_away_lowers_the_count_and_is_written_down()
    {
        var plant = Add("Pups", 5);

        await service.SomeDiedAsync(plant, 2, Today);
        await service.GaveSomeAwayAsync(plant, 1, " Anna ", Today);
        await service.GaveSomeAwayAsync(plant, 1, null, Today);

        Assert.Null(plant.Count);
        Assert.Equal(["2 died, 3 left", "Gave 1 to Anna, 2 left", "Gave 1 away, 1 left"], Texts(plant));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task Some_of_them_has_to_leave_at_least_one(int count)
    {
        var plant = Add("Pups", 3);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SomeDiedAsync(plant, count, Today));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GaveSomeAwayAsync(plant, count, null, Today));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SplitOffAsync(plant, count, null, Today));
        Assert.Equal(3, plant.Count);
    }

    [Fact]
    public async Task Splitting_off_makes_numbered_plants_with_the_pot_plant_as_parent()
    {
        var plant = Add("Pups", 4);

        var made = await service.SplitOffAsync(plant, 2, "Pup", Today);

        Assert.Equal(2, plant.Count);
        Assert.Equal(["Pup 1", "Pup 2"], made.Select(p => p.Nickname));
        Assert.All(made, p =>
        {
            Assert.Equal(plant.Id, p.ParentPlantId);
            Assert.Equal(LooseDate.Of(Today), p.AcquiredOn);
            Assert.Equal("Aloe", p.Genus);
            Assert.Null(p.Count);
            Assert.Contains(plants.Plants.Values, x => x.Id == p.Id);
            Assert.Contains("Split off from Pups", Texts(p));
        });
        Assert.Contains("Potted 2 up on their own: Pup 1, Pup 2", Texts(plant));
    }

    [Fact]
    public async Task Splitting_off_one_without_a_nickname_leaves_it_unnamed_and_links_it_in_the_history()
    {
        var plant = Add("Pups", 2);

        var made = Assert.Single(await service.SplitOffAsync(plant, 1, null, Today));

        Assert.Null(made.Nickname);
        Assert.Null(plant.Count);
        Assert.Equal(made.Id, timeline.Entries.Single(e => e.SubjectId == plant.Id).RelatedId);
    }

    // Status

    [Fact]
    public async Task A_plant_s_status_goes_to_the_plants_potted_with_it_and_back()
    {
        var a = Add("A");
        var b = Add("B");
        await service.PotTogetherAsync(b, a, Today, Label);

        await service.SetStatusAsync([a], PlantStatus.Died, Label);
        Assert.Equal(PlantStatus.Died, b.Status);

        await service.SetStatusAsync([a], PlantStatus.Active, Label);
        Assert.Equal(PlantStatus.Active, b.Status);
        Assert.Equal(a.Id, b.PottedWithPlantId);
        Assert.False(b.InCollection);
    }

    [Fact]
    public async Task Status_details_go_along_and_only_plants_that_shared_the_status_come_back()
    {
        var a = Add("A");
        var b = Add("B");
        var c = Add("C");
        await service.PotTogetherAsync(b, a, Today, Label);
        await service.PotTogetherAsync(c, a, Today, Label);
        c.Status = PlantStatus.Died;

        a.Status = PlantStatus.GivenAway;
        a.LeftTo = "Anna";
        var before = PlantStatus.Active;
        await service.CarryStatusToPottedWithAsync(a, before, Label);

        Assert.Equal(PlantStatus.GivenAway, b.Status);
        Assert.Equal("Anna", b.LeftTo);
        Assert.Equal(PlantStatus.Died, c.Status);

        a.Status = PlantStatus.Active;
        await service.CarryStatusToPottedWithAsync(a, PlantStatus.GivenAway, Label);

        Assert.Equal(PlantStatus.Active, b.Status);
        Assert.Null(b.LeftTo);
        Assert.Equal(PlantStatus.Died, c.Status);
    }

    // Delete

    [Fact]
    public async Task Deleting_a_plant_takes_out_the_plants_potted_with_it()
    {
        var a = Add("A");
        var b = Add("B", 2);
        await service.PotTogetherAsync(b, a, Today, Label);

        await service.DeleteAsync(a, Label);

        Assert.True(b.InCollection);
        Assert.Null(b.PottedWithPlantId);
        Assert.True(plants.Plants[a.Id].IsDeleted);
    }

    [Fact]
    public async Task Deleting_a_plant_that_is_potted_with_another_lowers_the_other_plant_s_count()
    {
        var a = Add("A");
        var b = Add("B", 2);
        await service.PotTogetherAsync(b, a, Today, Label);
        Assert.Equal(3, a.Count);

        await service.DeleteAsync(b, Label);

        Assert.Null(a.Count);
        Assert.True(plants.Plants[b.Id].IsDeleted);
    }

    // CSV

    [Fact]
    public async Task The_export_names_the_plant_a_plant_is_potted_with()
    {
        var a = Add("Big aloe");
        var b = Add("Pup");
        var c = Add("Orphan");
        var gone = Add("Gone");
        await service.PotTogetherAsync(b, a, Today, Label);
        c.PottedWithPlantId = gone.Id;
        gone.DeletedAt = Now;

        var csv = Backup.CsvExport.Plants(plants.Plants.Values, Places.None, []);
        var sheet = Import.SheetReader.Read(csv);
        var column = sheet.Headings.ToList().IndexOf("Potted with");
        var named = sheet.Rows.ToDictionary(r => r[0], r => r[column]);

        Assert.Equal(sheet.Headings.ToList().IndexOf("How many") + 1, column);
        Assert.Equal("Big aloe", named["Pup"]);
        Assert.Equal("", named["Orphan"]);
        Assert.Equal("", named["Big aloe"]);
    }

    [Fact]
    public void Importing_keeps_potted_with_as_a_line_in_About_it_and_older_exports_still_import()
    {
        var headings = Import.SheetReader.Read(Backup.CsvExport.Plants([], Places.None, [])).Headings;
        var guess = Import.ImportFields.Guess(headings);
        Assert.Equal(Import.ImportField.PottedWith, guess[headings.ToList().IndexOf("Potted with")]);

        string[] before = ["Name", "Number", "Nickname", "Genus", "Species", "Cultivar", "Name sure", "Locality or code", "Status", "Room", "Acquired", "Origin", "Raised", "Source", "Price paid", "How many", "Tags", "Notes", "Added", "Clone name"];
        var older = Import.ImportFields.Guess(before);
        Assert.Equal(before.Length, older.Count);
        Assert.DoesNotContain(Import.ImportField.PottedWith, older);
        Assert.Equal(Import.ImportField.AboutLine, older[^1]);
    }

    // Changes

    [Fact]
    public async Task Changing_the_count_on_the_edit_form_is_written_down()
    {
        var plant = Add("Pups", 5);
        var before = plant.Copy();
        plant.Count = 3;

        await service.UpdateAsync(before, plant, Label);

        Assert.Contains("How many: 5 to 3", Texts(plant));
    }

    // Pot up

    private PropagationService Propagations() =>
        new(propagations, plants, timeline, new FakePropagationCareLogRepository(), new FakeCareLogRepository(), new FakePhotoRepository(), Time);

    private async Task<Propagation> StartAsync(int count)
    {
        var parent = Add("Parent");
        var propagations = Propagations();
        var started = propagations.StartFrom(parent);
        started.InitialCount = count;
        started.Genus = "Aloe";
        started.StartedOn = Today.AddDays(-30);
        await propagations.CreateAsync(started, Label);
        return started;
    }

    [Fact]
    public async Task Potting_up_into_one_pot_makes_one_plant_with_the_count()
    {
        var started = await StartAsync(4);

        var made = Assert.Single(await Propagations().PotUpAsync(started, new PotUpRequest(3, Today, "Pups", OnePot: true)));

        Assert.Equal(3, made.Count);
        Assert.Equal("Pups", made.Nickname);
        Assert.Equal(3, started.PottedUpCount);
    }

    [Fact]
    public async Task Potting_up_one_with_one_pot_stores_no_count()
    {
        var started = await StartAsync(2);

        var made = Assert.Single(await Propagations().PotUpAsync(started, new PotUpRequest(1, Today, OnePot: true)));

        Assert.Null(made.Count);
    }

    [Fact]
    public async Task Potting_up_in_with_a_plant_pots_the_new_plant_together_with_it()
    {
        var started = await StartAsync(3);
        var into = Add("Host", 2, Guid.NewGuid());

        var made = Assert.Single(await Propagations().PotUpAsync(started, new PotUpRequest(2, Today, IntoPlantId: into.Id), Label));

        Assert.Equal(2, made.Count);
        Assert.Equal(into.Id, made.PottedWithPlantId);
        Assert.Equal(into.InnerPotId, made.InnerPotId);
        Assert.Equal(4, into.Count);
        Assert.Equal(2, started.PottedUpCount);
    }

    [Fact]
    public async Task Potting_up_in_with_a_plant_outside_the_collection_changes_nothing()
    {
        var started = await StartAsync(2);
        var dead = Add("Dead");
        dead.Status = PlantStatus.Died;
        var before = plants.Plants.Count;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Propagations().PotUpAsync(started, new PotUpRequest(1, Today, IntoPlantId: dead.Id), Label));

        Assert.Equal(0, started.PottedUpCount);
        Assert.Equal(before, plants.Plants.Count);
    }
}
