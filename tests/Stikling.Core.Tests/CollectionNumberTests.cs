using System.Text.Json;
using Stikling.Core.Away;
using Stikling.Core.Backup;
using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Propagations;
using Stikling.Core.Sharing;

namespace Stikling.Core.Tests;

public class CollectionNumberTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 15);

    private static Plant Numbered(string? code, string name = "Plant", Action<Plant>? set = null)
    {
        var plant = new Plant { Nickname = name, CollectionNumber = code, CreatedAt = Now };
        set?.Invoke(plant);
        return plant;
    }

    private static Propagation Cutting(string? code, Plant? parent = null, Action<Propagation>? set = null)
    {
        var cutting = new Propagation
        {
            Nickname = "Cutting", Genus = "Hoya", CollectionNumber = code, ParentPlantId = parent?.Id,
            StartedOn = Today, InitialCount = 3, CreatedAt = Now
        };
        set?.Invoke(cutting);
        return cutting;
    }

    // Normalize and IsValid

    [Theory]
    [InlineData("117", "117")]
    [InlineData("  #117A ", "117a")]
    [InlineData("# 117", "117")]
    [InlineData("117C-A", "117c-a")]
    public void Normalize_trims_lower_cases_and_removes_the_hash(string typed, string expected) =>
        Assert.Equal(expected, CollectionNumbers.Normalize(typed));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("#")]
    public void Normalize_gives_null_for_blank(string? typed) => Assert.Null(CollectionNumbers.Normalize(typed));

    [Theory]
    [InlineData("1")]
    [InlineData("117")]
    [InlineData("999999")]
    [InlineData("117a")]
    [InlineData("117z")]
    [InlineData("117aa")]
    [InlineData("117c1")]
    [InlineData("117c2")]
    [InlineData("117c-a")]
    [InlineData("117c2-a")]
    [InlineData("117c-a3")]
    public void Valid_shapes(string code) => Assert.True(CollectionNumbers.IsValid(code));

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("a117")]
    [InlineData("1234567")]
    [InlineData("117-")]
    [InlineData("117-1")]
    [InlineData("117A")]
    [InlineData("117 a")]
    [InlineData("117c--a")]
    [InlineData("-117")]
    [InlineData("117a-")]
    [InlineData("1aaaaaaaaaaaaaaaaaaaaa!")]
    public void Shapes_that_are_not_valid(string code) => Assert.False(CollectionNumbers.IsValid(code));

    [Fact]
    public void A_plant_or_propagation_with_a_bad_number_does_not_validate()
    {
        Assert.Contains(CollectionNumbers.NotValidText, Numbered("abc").Validate(Today));
        Assert.Contains(CollectionNumbers.NotValidText, Cutting("abc").Validate(Today));
        Assert.DoesNotContain(CollectionNumbers.NotValidText, Numbered(" #117a ").Validate(Today));
        Assert.DoesNotContain(CollectionNumbers.NotValidText, Numbered(null).Validate(Today));
    }

    // NextFree

    [Fact]
    public void The_first_number_is_1() => Assert.Equal(1, CollectionNumbers.NextFree([Numbered(null)]));

    [Fact]
    public void The_next_number_is_one_more_than_the_highest_leading_number()
    {
        var plants = new[] { Numbered("5"), Numbered("117c1"), Numbered("12a"), Numbered(null) };

        Assert.Equal(118, CollectionNumbers.NextFree(plants));
    }

    [Fact]
    public void An_old_label_is_never_reused_even_if_the_plant_died_or_left()
    {
        var plants = new[]
        {
            Numbered("3"),
            Numbered("40", set: p => p.Status = PlantStatus.Died),
            Numbered("50", set: p => p.Status = PlantStatus.GivenAway)
        };

        Assert.Equal(51, CollectionNumbers.NextFree(plants));
    }

    [Fact]
    public void A_deleted_plant_does_not_count()
    {
        var plants = new[] { Numbered("3"), Numbered("90", set: p => p.DeletedAt = Now) };

        Assert.Equal(4, CollectionNumbers.NextFree(plants));
    }

    // NextLetter

    [Fact]
    public void A_parent_without_a_number_gives_no_letter() =>
        Assert.Null(CollectionNumbers.NextLetter(Numbered(null), [], []));

    [Fact]
    public void The_first_cutting_gets_a_and_the_next_skips_what_is_used()
    {
        var parent = Numbered("117");

        Assert.Equal("117a", CollectionNumbers.NextLetter(parent, [], [parent]));
        Assert.Equal("117c", CollectionNumbers.NextLetter(parent, [Cutting("117a"), Cutting("117b")], [parent]));
    }

    [Fact]
    public void A_plant_with_the_letter_counts_as_using_it()
    {
        var parent = Numbered("117");
        var pottedUp = Numbered("117a");

        Assert.Equal("117b", CollectionNumbers.NextLetter(parent, [], [parent, pottedUp]));
    }

    [Fact]
    public void A_deleted_cutting_frees_its_letter()
    {
        var parent = Numbered("117");

        Assert.Equal("117a", CollectionNumbers.NextLetter(parent, [Cutting("117a", set: c => c.DeletedAt = Now)], [parent]));
    }

    [Fact]
    public void Letters_go_on_past_z()
    {
        var parent = Numbered("117");
        var used = "abcdefghijklmnopqrstuvwxyz".Select(c => Cutting("117" + c)).ToList();

        Assert.Equal("117aa", CollectionNumbers.NextLetter(parent, used, [parent]));
        used.Add(Cutting("117aa"));
        Assert.Equal("117ab", CollectionNumbers.NextLetter(parent, used, [parent]));
    }

    [Fact]
    public void A_parent_that_has_a_letter_gets_a_hyphen()
    {
        var parent = Numbered("117c");
        var potted = Numbered("117c1");

        Assert.Equal("117c-a", CollectionNumbers.NextLetter(parent, [], [parent]));
        Assert.Equal("117c1-a", CollectionNumbers.NextLetter(potted, [], [potted]));
        Assert.Equal("117c-b", CollectionNumbers.NextLetter(parent, [Cutting("117c-a")], [parent]));
    }

    // PottedUpCodes

    [Fact]
    public void Without_a_number_the_plants_get_none() =>
        Assert.Equal<string?>([null, null], CollectionNumbers.PottedUpCodes(Cutting(null), 2));

    [Fact]
    public void One_plant_from_the_first_pot_up_keeps_the_code() =>
        Assert.Equal<string?>(["117c"], CollectionNumbers.PottedUpCodes(Cutting("117c"), 1));

    [Fact]
    public void Several_plants_get_a_running_digit() =>
        Assert.Equal<string?>(["117c1", "117c2", "117c3"], CollectionNumbers.PottedUpCodes(Cutting("117c"), 3));

    [Fact]
    public void A_later_pot_up_continues_the_digits()
    {
        var cutting = Cutting("117c", set: c => c.PottedUpCount = 3);

        Assert.Equal<string?>(["117c4"], CollectionNumbers.PottedUpCodes(cutting, 1));
        Assert.Equal<string?>(["117c4", "117c5"], CollectionNumbers.PottedUpCodes(cutting, 2));
    }

    [Fact]
    public async Task Potting_up_gives_the_new_plants_their_codes()
    {
        var plants = new FakePlantRepository();
        var propagations = new FakePropagationRepository();
        var service = new PropagationService(propagations, plants, new FakeTimelineRepository(),
            new FakePropagationCareLogRepository(), new FakeCareLogRepository(), new FakePhotoRepository(), new FixedTime(Now));
        var cutting = Cutting("117c", set: c => c.InitialCount = 5);
        await propagations.SaveAsync(cutting);

        var first = await service.PotUpAsync(cutting, new PotUpRequest(1, Today, Medium: GrowingMedium.Soil));
        var more = await service.PotUpAsync(cutting, new PotUpRequest(2, Today, Medium: GrowingMedium.Soil));

        Assert.Equal("117c", first[0].CollectionNumber);
        Assert.Equal(["117c2", "117c3"], more.Select(p => p.CollectionNumber));
    }

    [Fact]
    public async Task Potting_up_a_cutting_without_a_number_gives_plants_without_one()
    {
        var propagations = new FakePropagationRepository();
        var service = new PropagationService(propagations, new FakePlantRepository(), new FakeTimelineRepository(),
            new FakePropagationCareLogRepository(), new FakeCareLogRepository(), new FakePhotoRepository(), new FixedTime(Now));
        var cutting = Cutting(null);
        await propagations.SaveAsync(cutting);

        var made = await service.PotUpAsync(cutting, new PotUpRequest(2, Today, Medium: GrowingMedium.Soil));

        Assert.All(made, p => Assert.Null(p.CollectionNumber));
    }

    [Fact]
    public void Adding_another_like_this_does_not_copy_the_number() =>
        Assert.Null(Numbered("117").Duplicate().CollectionNumber);

    // Clashes

    [Fact]
    public void Clashes_name_the_others_with_the_same_code()
    {
        var me = Numbered("117", "Me");
        var other = Numbered("117", "Big Monstera");
        var deleted = Numbered("117", "Gone", p => p.DeletedAt = Now);
        var cutting = Cutting("117", set: c => c.Nickname = "Jar");

        var clashes = CollectionNumbers.Clashes("#117", me.Id, [me, other, deleted, Numbered("118")], [cutting]);

        Assert.Equal(["Big Monstera", "Jar"], clashes.Select(c => c.Name));
        Assert.Equal([false, true], clashes.Select(c => c.IsPropagation));
        Assert.Equal("Big Monstera, Jar also have #117.", CollectionNumbers.ClashNote("117", clashes));
        Assert.Equal("Big Monstera also has #117.", CollectionNumbers.ClashNote("117", [clashes[0]]));
    }

    [Fact]
    public void No_code_clashes_with_nothing() =>
        Assert.Empty(CollectionNumbers.Clashes(null, Guid.NewGuid(), [Numbered(null)], []));

    [Fact]
    public void A_cutting_and_the_plant_it_became_do_not_clash()
    {
        var cutting = Cutting("117c");
        var plant = Numbered("117c", set: p => p.FromPropagationId = cutting.Id);

        Assert.Empty(CollectionNumbers.Clashes("117c", cutting.Id, [plant], [cutting]));
        Assert.Empty(CollectionNumbers.Clashes("117c", plant.Id, [plant], [cutting]));
    }

    // MatchesSearch

    [Theory]
    [InlineData("117a", "117", true)]
    [InlineData("117a", "#117", true)]
    [InlineData("117c1", "117c", true)]
    [InlineData("117", "11", true)]
    [InlineData("117", "118", false)]
    [InlineData("117", "17", false)]
    [InlineData("117", "monstera", false)]
    [InlineData("117", "", false)]
    [InlineData(null, "117", false)]
    [InlineData("117", "11 7", false)]
    public void Search_matches_the_start_of_a_code(string? code, string search, bool expected) =>
        Assert.Equal(expected, CollectionNumbers.MatchesSearch(code, search));

    [Fact]
    public void The_plant_list_search_finds_a_number()
    {
        var plants = new[] { Numbered("117", "A"), Numbered("117a", "B"), Numbered("12", "C") };

        var found = new PlantFilter(Search: "#117").Apply(plants).Select(p => p.DisplayName);

        Assert.Equal(["A", "B"], found);
    }

    [Fact]
    public void The_propagation_search_finds_a_number()
    {
        var found = new PropagationFilter(Search: "117").Apply([Cutting("117a"), Cutting("12"), Cutting(null)]);

        Assert.Equal("117a", Assert.Single(found).CollectionNumber);
    }

    // Number all

    [Fact]
    public void Plants_are_numbered_oldest_first_from_the_next_free_number()
    {
        var newer = Numbered(null, "Newer", p => p.AcquiredOn = LooseDate.Of(new DateOnly(2025, 5, 1)));
        var older = Numbered(null, "Older", p => p.AcquiredOn = LooseDate.Of(2020));
        var noDate = Numbered(null, "No date", p => p.CreatedAt = new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var taken = Numbered("117", "Taken");

        var plan = CollectionNumbers.Plan([newer, older, noDate, taken], []);

        Assert.Equal([("Older", "118"), ("No date", "119"), ("Newer", "120")],
            plan.Plants.Select(p => (p.Plant.DisplayName, p.Code)));
        Assert.Equal("Numbers 3 plants, 118 to 120.", plan.Summary);
    }

    [Fact]
    public void Only_active_plants_without_a_number_are_numbered()
    {
        var plants = new[]
        {
            Numbered(null, "Died", p => p.Status = PlantStatus.Died),
            Numbered(null, "Gone", p => p.DeletedAt = Now),
            Numbered("4", "Has one"),
            Numbered(null, "Active")
        };

        var plan = CollectionNumbers.Plan(plants, []);

        var item = Assert.Single(plan.Plants);
        Assert.Equal(("Active", "5"), (item.Plant.DisplayName, item.Code));
    }

    [Fact]
    public void Cuttings_get_letters_from_a_plant_numbered_in_the_same_plan()
    {
        var parent = Numbered(null, "Parent");
        var first = Cutting(null, parent, c => c.StartedOn = new DateOnly(2026, 9, 1));
        var second = Cutting(null, parent, c => c.StartedOn = new DateOnly(2026, 9, 20));

        var plan = CollectionNumbers.Plan([parent], [second, first]);

        Assert.Equal("1", Assert.Single(plan.Plants).Code);
        Assert.Equal([(first.Id, "1a"), (second.Id, "1b")], plan.Cuttings.Select(c => (c.Propagation.Id, c.Code)));
        Assert.Equal("Numbers 1 plant, 1, and gives 2 going cuttings a letter.", plan.Summary);
    }

    [Fact]
    public void Letters_skip_what_is_taken_and_only_going_cuttings_with_a_numbered_parent_get_one()
    {
        var parent = Numbered("7", "Parent");
        var other = Numbered("8", "Other", p => p.Status = PlantStatus.Died);
        var plan = CollectionNumbers.Plan(
            [parent, other],
            [
                Cutting("7a", parent),
                Cutting(null, parent),
                Cutting(null, null),
                Cutting(null, parent, c => c.Stage = PropagationStage.Done),
                Cutting(null, parent, c => c.DeletedAt = Now),
                Cutting("8x", other)
            ]);

        Assert.Empty(plan.Plants);
        Assert.Equal("7b", Assert.Single(plan.Cuttings).Code);
        Assert.Equal("Gives 1 going cutting a letter.", plan.Summary);
    }

    [Fact]
    public void An_empty_plan_says_so()
    {
        var plan = CollectionNumbers.Plan([Numbered("1")], []);

        Assert.True(plan.IsEmpty);
        Assert.Equal("Every plant here has a number.", plan.Summary);
    }

    [Fact]
    public async Task Applying_the_plan_saves_through_the_repositories()
    {
        var plants = new FakePlantRepository();
        var propagations = new FakePropagationRepository();
        var parent = Numbered(null, "Parent");
        var cutting = Cutting(null, parent);
        await plants.SaveAsync(parent);
        await propagations.SaveAsync(cutting);
        var service = new CollectionNumberService(plants, propagations);

        var plan = await service.PlanAsync();
        await service.ApplyAsync(plan);

        Assert.Equal("1", plants.Plants[parent.Id].CollectionNumber);
        Assert.Equal("1a", propagations.Propagations[cutting.Id].CollectionNumber);
        Assert.True((await service.PlanAsync()).IsEmpty);
    }

    // Everywhere else it shows

    [Fact]
    public void The_number_is_kept_in_a_backup_and_older_records_read_as_none()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var json = JsonSerializer.Serialize(new BackupData { Plants = [Numbered("117b")], Propagations = [Cutting("117c")] }, options);
        var back = JsonSerializer.Deserialize<BackupData>(json, options)!;

        Assert.Equal("117b", back.Plants[0].CollectionNumber);
        Assert.Equal("117c", back.Propagations[0].CollectionNumber);
        Assert.Null(JsonSerializer.Deserialize<Plant>("""{"nickname":"Old"}""", options)!.CollectionNumber);
    }

    [Fact]
    public void The_export_has_the_number()
    {
        var csv = CsvExport.Plants([Numbered("117", "Basil")], Rooms.Places.None, []);

        Assert.Contains("Basil,117,Basil", csv);
    }

    [Fact]
    public void The_number_sorts_by_its_leading_number_then_the_rest_with_blanks_last()
    {
        var rows = PlantTable.Rows(
            [Numbered("12", "B"), Numbered("117a", "E"), Numbered(null, "F"), Numbered("9", "A"), Numbered("117", "D"), Numbered("117c1", "G")],
            [], Today, _ => null, _ => null);

        var ascending = PlantTable.Sort(rows, new PlantTableSort(PlantColumn.Number));
        var descending = PlantTable.Sort(rows, new PlantTableSort(PlantColumn.Number, Descending: true));

        Assert.Equal(["A", "B", "D", "E", "G", "F"], ascending.Select(r => r.Plant.DisplayName));
        Assert.Equal(["G", "E", "D", "B", "A", "F"], descending.Select(r => r.Plant.DisplayName));
        Assert.Equal("No.", PlantTable.Header(PlantColumn.Number));
        Assert.Equal(PlantColumn.Number, PlantTable.ParseSort("Number").Column);
    }

    [Fact]
    public void Shared_plant_text_puts_the_number_before_the_name()
    {
        string? Everyday(string? g, string? s, string? c) => null;
        var plants = new[]
        {
            Numbered("117", "Fern", p => p.Spare = SpareMark.UpForSwap),
            Numbered(null, "Aloe")
        };

        var text = SwapLists.WritePlants(plants, Everyday, nicknames: false, footer: false);
        var swap = SwapLists.WriteSwap(plants, [], Everyday, nicknames: false, footer: false);

        Assert.Equal($"Aloe{Environment.NewLine}#117 Fern, up for swap", text);
        Assert.Contains("#117 Fern", swap);
    }

    [Fact]
    public void The_sitter_label_has_the_number()
    {
        var subject = new SitterSubject(Guid.NewGuid(), true, "Fern", null, null, null, null, null, null, null, null, null, [], false, null, "117");
        var plain = subject with { Number = null };

        Assert.Equal("#117 Fern", subject.Label);
        Assert.Equal("Fern", plain.Label);
    }
}
