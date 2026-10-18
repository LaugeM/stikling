using Stikling.Core.Models;
using Stikling.Core.Pests;
using Stikling.Core.Search;

namespace Stikling.Core.Tests;

public class SearchTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly FakePlantRepository plants = new();
    private readonly FakePropagationRepository propagations = new();
    private readonly FakeTimelineRepository timeline = new();
    private readonly FakeCareLogRepository careLogs = new();
    private readonly FakePropagationCareLogRepository propagationCareLogs = new();
    private readonly FakeProblemRepository problems = new();
    private readonly FakeProblemUpdateRepository problemUpdates = new();
    private readonly FakePestCaseRepository pestCases = new();
    private readonly FakePestTreatments pestTreatments = new();
    private readonly FakeTreatmentRecipeRepository recipes = new();
    private readonly FakeWishRepository wishes = new();
    private readonly FakePotRepository pots = new();
    private readonly FakeSoilMixRepository mixes = new();
    private readonly FakeFeedRepository feeds = new();
    private readonly FakeProductRepository products = new();
    private readonly FakePlaceRepository places = new();

    private async Task<SearchIndex> Index() =>
        await new SearchService(plants, propagations, timeline, careLogs, propagationCareLogs, problems, problemUpdates,
            pestCases, pestTreatments, recipes, wishes, pots, mixes, feeds, products, places, new FakeOwnCareKindRepository(), new FixedTime(Now))
            .LoadAsync(e => e.ToString());

    private async Task<SearchResults> Find(string? query) => (await Index()).Find(query);

    private Plant AddPlant(string? nickname = null, string? genus = null, string? species = null, Action<Plant>? set = null)
    {
        var plant = new Plant { Nickname = nickname, Genus = genus, Species = species };
        set?.Invoke(plant);
        plants.Plants[plant.Id] = plant;
        return plant;
    }

    private Propagation AddCutting(string? nickname = null, string? genus = null, Action<Propagation>? set = null)
    {
        var cutting = new Propagation { Nickname = nickname, Genus = genus };
        set?.Invoke(cutting);
        propagations.Propagations[cutting.Id] = cutting;
        return cutting;
    }

    private TimelineEntry AddNote(Guid subject, string text, DateTimeOffset at,
        SubjectType type = SubjectType.Plant, TimelineKind kind = TimelineKind.Note)
    {
        var entry = new TimelineEntry { SubjectType = type, SubjectId = subject, Kind = kind, OccurredAt = at, Text = text };
        timeline.Entries.Add(entry);
        return entry;
    }

    private static string[] Titles(IEnumerable<SearchItem> items) => [.. items.Select(i => i.Title)];

    // Folding

    [Theory]
    [InlineData("Café", "cafe")]
    [InlineData("ALOCASIA", "alocasia")]
    [InlineData("Crème brûlée", "creme brulee")]
    [InlineData("é", "e")]
    [InlineData(null, "")]
    public void Fold_drops_accents_and_capitals(string? text, string expected) =>
        Assert.Equal(expected, SearchText.Fold(text));

    [Fact]
    public async Task Accents_and_case_do_not_matter_on_either_side()
    {
        AddPlant("Café plant");
        AddPlant("ALOCASIA");

        Assert.Equal(["Café plant"], Titles((await Find("cafe")).Plants));
        Assert.Equal(["Café plant"], Titles((await Find("CAFÉ")).Plants));
        Assert.Equal(["ALOCASIA"], Titles((await Find("alocasia")).Plants));
    }

    // Matching

    [Fact]
    public async Task Every_word_must_be_found()
    {
        AddPlant(genus: "Monstera", species: "deliciosa", set: p => p.Cultivar = "Thai Constellation");
        AddPlant("Big Monstera", "Monstera", "deliciosa");

        Assert.Equal(2, (await Find("monstera")).Plants.Count);
        Assert.Single((await Find("thai monstera")).Plants);
        Assert.Empty((await Find("thai pothos")).Plants);
    }

    [Fact]
    public async Task A_blank_query_finds_nothing()
    {
        AddPlant("Monstera");

        Assert.True((await Find("")).IsEmpty);
        Assert.True((await Find("   ")).IsEmpty);
        Assert.True((await Find(null)).IsEmpty);
    }

    [Fact]
    public async Task Names_match_from_one_letter_but_notes_need_two()
    {
        var plant = AddPlant("Monstera");
        AddNote(plant.Id, "Found mealybugs on the stem", Now);

        var one = await Find("m");
        Assert.Single(one.Plants);
        Assert.Empty(one.Notes);

        Assert.Single((await Find("mealybugs")).Notes);
        // One short word anywhere keeps notes out
        Assert.Empty((await Find("mealybugs s")).Notes);
    }

    [Fact]
    public async Task A_plant_is_found_by_its_everyday_name_and_its_number()
    {
        var plant = AddPlant(genus: "Dracaena", species: "trifasciata", set: p => p.CollectionNumber = "12");
        var index = await new SearchService(plants, propagations, timeline, careLogs, propagationCareLogs, problems,
            problemUpdates, pestCases, pestTreatments, recipes, wishes, pots, mixes, feeds, products, places, new FakeOwnCareKindRepository(), new FixedTime(Now))
            .LoadAsync(e => e.ToString(), (_, species, _) => species == "trifasciata" ? "Snake plant" : null);

        Assert.Equal([plant.Id], index.Find("snake").Plants.Select(i => i.Id));
        var byNumber = index.Find("12").Plants.Single();
        Assert.Equal("number 12", byNumber.Meta);
    }

    // Order

    [Fact]
    public async Task Titles_starting_with_the_word_come_first_and_gone_ones_last()
    {
        AddPlant("Zed monstera");
        AddPlant("Big monstera");
        AddPlant("Monstera Thai");
        AddPlant("Dead monstera", set: p => p.Status = PlantStatus.Died);
        AddPlant("Monstera Albo", set: p => p.Status = PlantStatus.Sold);

        var found = (await Find("monstera")).Plants;

        Assert.Equal(["Monstera Thai", "Big monstera", "Zed monstera", "Monstera Albo", "Dead monstera"], Titles(found));
        Assert.Equal([false, false, false, true, true], found.Select(i => i.Gone));
    }

    [Fact]
    public async Task A_finished_propagation_and_a_got_wish_are_gone()
    {
        AddCutting("Pothos cutting", set: p => p.Stage = PropagationStage.Done);
        AddCutting("Pothos new");
        var got = new Wish { Name = "Pothos wish", GotOn = new DateOnly(2026, 9, 1) };
        wishes.Wishes[got.Id] = got;

        var results = await Find("pothos");

        Assert.Equal(["Pothos new", "Pothos cutting"], Titles(results.Propagations));
        Assert.True(results.Wishes.Single().Gone);
    }

    [Fact]
    public async Task Groups_come_in_the_fixed_order()
    {
        AddPlant("Fern");
        AddCutting("Fern cutting");
        places.Add("Fern corner");

        var kinds = (await Find("fern")).NonEmpty.Select(g => g.Kind);

        Assert.Equal([SearchKind.Plant, SearchKind.Propagation, SearchKind.Place], kinds);
    }

    // Reasons

    [Fact]
    public async Task A_tag_match_says_which_tag()
    {
        AddPlant("Monstera", set: p => p.Tags = ["Windowsill", "Rare"]);

        var plant = (await Find("windowsill")).Plants.Single();
        Assert.Equal("tag Windowsill", plant.Meta);

        Assert.Equal("tag Windowsill", (await Find("monstera windowsill")).Plants.Single().Meta);
        // Found by the name alone, so nothing to explain
        Assert.Null((await Find("monstera")).Plants.Single().Meta);
    }

    [Fact]
    public async Task The_plants_own_notes_count_for_two_letter_words()
    {
        AddPlant("Monstera", set: p => p.Notes = "Bought at the market");

        Assert.Equal("in the notes", (await Find("market")).Plants.Single().Meta);
        Assert.Empty((await Find("m market")).Plants);
    }

    // What is left out

    [Fact]
    public async Task Deleted_records_and_places_merged_into_another_are_left_out()
    {
        var kept = places.Add("Kitchen");
        var merged = places.Add("Kitchen old");
        merged.MergedIntoId = kept.Id;
        merged.DeletedAt = Now;
        AddPlant("Gone plant", set: p => p.DeletedAt = Now);
        var deletedPot = new Pot { Name = "Kitchen pot", DeletedAt = Now };
        pots.Pots[deletedPot.Id] = deletedPot;
        var deletedWish = new Wish { Name = "Kitchen wish", DeletedAt = Now };
        wishes.Wishes[deletedWish.Id] = deletedWish;

        var results = await Find("kitchen");

        Assert.Equal(["Kitchen"], Titles(results.Places));
        Assert.Empty(results.Pots);
        Assert.Empty(results.Wishes);
        Assert.Empty((await Find("gone plant")).Plants);
    }

    [Fact]
    public async Task Notes_of_deleted_plants_and_deleted_notes_are_left_out()
    {
        var plant = AddPlant("Monstera");
        var deleted = AddPlant("Old monstera", set: p => p.DeletedAt = Now);
        AddNote(plant.Id, "mealybugs here", Now);
        AddNote(deleted.Id, "mealybugs there", Now);
        timeline.Entries.Add(new TimelineEntry
        {
            SubjectId = plant.Id, Kind = TimelineKind.Note, OccurredAt = Now, Text = "mealybugs deleted", DeletedAt = Now
        });
        careLogs.Logs.Add(new CareLog { PlantId = deleted.Id, Kind = CareKind.Watered, OccurredOn = new DateOnly(2026, 10, 1), Notes = "mealybugs care" });

        var notes = (await Find("mealybugs")).Notes;

        Assert.Equal(["mealybugs here"], notes.Select(n => n.Note!.Snippet));
    }

    [Fact]
    public async Task A_problem_is_listed_once_from_its_own_record()
    {
        var plant = AddPlant("Monstera");
        var problem = new Problem { SubjectId = plant.Id, StartedOn = new DateOnly(2026, 10, 2), Note = "Brown edges on the leaves" };
        problems.Problems[problem.Id] = problem;
        problemUpdates.Updates[Guid.NewGuid()] = new ProblemUpdate
        {
            ProblemId = problem.Id, OccurredOn = new DateOnly(2026, 10, 5), Note = "Brown edges are worse"
        };
        timeline.Entries.Add(new TimelineEntry
        {
            SubjectId = plant.Id, Kind = TimelineKind.Change, Event = TimelineEvent.Problem,
            OccurredAt = Now, Text = "Problem: brown edges"
        });

        var notes = (await Find("brown edges")).Notes;

        Assert.Equal(2, notes.Count);
        Assert.All(notes, n => Assert.Equal("Problem", n.Note!.Label));
        Assert.Contains(notes, n => n.Href == $"problems/{problem.Id}");
        Assert.Contains(notes, n => n.Href.StartsWith($"problems/{problem.Id}?entry="));
    }

    [Fact]
    public async Task Older_versions_of_a_corrected_note_are_not_searched()
    {
        var plant = AddPlant("Monstera");
        var entry = AddNote(plant.Id, "Repotted into bark", Now);
        entry.Edits = [new TimelineEdit { Text = "Repotted into pumice" }];

        Assert.Single((await Find("bark")).Notes);
        Assert.Empty((await Find("pumice")).Notes);
    }

    // Notes

    [Fact]
    public async Task Notes_come_newest_first_and_say_what_they_are()
    {
        var plant = AddPlant("Monstera");
        var cutting = AddCutting("Pothos");
        AddNote(plant.Id, "root rot at the base", Now.AddDays(-10));
        AddNote(cutting.Id, "root rot started", Now.AddDays(-2), SubjectType.Propagation, TimelineKind.Change);
        careLogs.Logs.Add(new CareLog { PlantId = plant.Id, Kind = CareKind.Watered, OccurredOn = new DateOnly(2026, 10, 14), Notes = "no root rot seen" });
        propagationCareLogs.Logs.Add(new PropagationCareLog
        {
            PropagationId = cutting.Id, Kind = PropagationCareKind.ChangedWater, OccurredOn = new DateOnly(2026, 10, 1), Notes = "root rot smell"
        });

        var notes = (await Find("root rot")).Notes;

        Assert.Equal(["Watered", "Update", "Note", "ChangedWater"], notes.Select(n => n.Note!.Label));
        Assert.Equal(["Monstera", "Pothos", "Monstera", "Pothos"], Titles(notes));
        Assert.Equal(SubjectType.Propagation, notes[1].Note!.OwnerType);
        Assert.StartsWith("plants/", notes[2].Href);
        Assert.Contains("?tab=care&entry=", notes[0].Href);
        Assert.StartsWith("propagations/", notes[1].Href);
    }

    [Fact]
    public async Task A_pest_note_is_named_for_the_plant_when_there_is_one_else_the_pest()
    {
        var plant = AddPlant("Monstera");
        var single = new PestCase { Pest = Pest.Mealybugs, Scope = PestScope.PickedPlants, PlantIds = [plant.Id], StartedOn = new DateOnly(2026, 10, 1), Notes = "white fluff in the nodes" };
        var everywhere = new PestCase { Pest = Pest.Thrips, StartedOn = new DateOnly(2026, 10, 4), Notes = "white flies near the window" };
        pestCases.Cases[single.Id] = single;
        pestCases.Cases[everywhere.Id] = everywhere;
        pestTreatments.Items.Add(new PestTreatment { CaseId = single.Id, OccurredOn = new DateOnly(2026, 10, 3), What = "Alcohol on a swab", Notes = "white bits gone" });
        pestTreatments.Items.Add(new PestTreatment { CaseId = Guid.NewGuid(), OccurredOn = new DateOnly(2026, 10, 3), Notes = "white of a deleted case" });

        var notes = (await Find("white")).Notes;

        Assert.Equal(["Thrips", "Monstera", "Monstera"], Titles(notes));
        Assert.All(notes, n => Assert.Equal("Pests", n.Note!.Label));
        Assert.Equal(SubjectType.Plant, notes[1].Note!.OwnerType);
        Assert.Null(notes[0].Note!.OwnerType);
        Assert.Equal($"pests/{everywhere.Id}", notes[0].Href);
        Assert.StartsWith($"pests/{single.Id}?entry=", notes[1].Href);
        Assert.Equal($"pests/{single.Id}", notes[2].Href);
    }

    [Fact]
    public async Task A_history_line_that_repeats_a_care_note_is_not_listed_next_to_it()
    {
        var plant = AddPlant("Monstera");
        var day = new DateOnly(2026, 10, 14);
        careLogs.Logs.Add(new CareLog { PlantId = plant.Id, Kind = CareKind.Repotted, OccurredOn = day, Notes = "Roots were  crowded" });
        AddNote(plant.Id, "Repotted: roots were crowded", new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero), kind: TimelineKind.Change);
        // Another day, or a note someone wrote, stays
        AddNote(plant.Id, "Repotted: roots were crowded", new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero), kind: TimelineKind.Change);
        AddNote(plant.Id, "Roots were crowded again", new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero));

        var notes = (await Find("crowded")).Notes;

        Assert.Equal(3, notes.Count);
        Assert.Equal(NoteSource.PlantCare, notes.First(n => n.Note!.Date.Day == 14 && n.Note.Source != NoteSource.Timeline).Note!.Source);
        Assert.Equal(2, notes.Count(n => n.Note!.Source == NoteSource.Timeline));
    }

    // Snippets

    [Fact]
    public async Task A_short_note_is_shown_whole_with_the_matches_marked()
    {
        var plant = AddPlant("Monstera");
        AddNote(plant.Id, "Saw  Mealybugs\non the\tnew leaf", Now);

        var note = (await Find("mealybugs new")).Notes.Single().Note!;

        Assert.Equal("Saw Mealybugs on the new leaf", note.Snippet);
        Assert.Equal(["Mealybugs", "new"], note.Ranges.Select(r => note.Snippet.Substring(r.Start, r.Length)));
    }

    [Fact]
    public async Task A_long_note_is_cut_between_words_with_an_ellipsis_at_each_cut_end()
    {
        var plant = AddPlant("Monstera");
        var before = string.Join(' ', Enumerable.Repeat("before", 30));
        var after = string.Join(' ', Enumerable.Repeat("after", 40));
        AddNote(plant.Id, $"{before} Mealybugs {after}", Now);

        var note = (await Find("mealybugs")).Notes.Single().Note!;

        Assert.StartsWith("…before", note.Snippet);
        Assert.EndsWith("after…", note.Snippet);
        var inner = note.Snippet.Trim('…');
        Assert.InRange(inner.IndexOf("Mealybugs"), 40, 60);
        Assert.InRange(inner.Length - inner.IndexOf("Mealybugs") - "Mealybugs".Length, 60, 80);
        var range = Assert.Single(note.Ranges);
        Assert.Equal("Mealybugs", note.Snippet.Substring(range.Start, range.Length));
    }

    [Fact]
    public async Task Matches_ignore_accents_and_are_marked_where_they_stand_in_the_text()
    {
        var plant = AddPlant("Monstera");
        AddNote(plant.Id, "Café Crème looks fine, the creme colour too", Now);

        var note = (await Find("creme")).Notes.Single().Note!;

        Assert.Equal(["Crème", "creme"], note.Ranges.Select(r => note.Snippet.Substring(r.Start, r.Length)));
    }

    [Fact]
    public async Task Overlapping_matches_are_joined()
    {
        var plant = AddPlant("Monstera");
        AddNote(plant.Id, "mealybugs on the stem", Now);

        var note = (await Find("meal mealybugs")).Notes.Single().Note!;

        var range = Assert.Single(note.Ranges);
        Assert.Equal("mealybugs", note.Snippet.Substring(range.Start, range.Length));
    }

    // The other groups

    [Fact]
    public async Task Pots_mixes_feeds_products_recipes_and_places_are_found_by_name_and_notes()
    {
        var pot = new Pot { Name = "Clear nursery pot", TopCm = 13, Notes = "bought in bulk" };
        pots.Pots[pot.Id] = pot;
        var mix = new SoilMix { Name = "Aroid mix", Notes = "airy", Ingredients = [new MixIngredient { Name = "Bark", Amount = 2 }] };
        mixes.Mixes[mix.Id] = mix;
        var feed = new Feed { Name = "Aroid feed", Notes = "weekly" };
        feeds.Feeds[feed.Id] = feed;
        var product = new Product { Name = "Aroid fertiliser" };
        products.Products[product.Id] = product;
        var recipe = new TreatmentRecipe { Name = "Aroid spray", Method = "Wipe both sides" };
        recipes.Recipes[recipe.Id] = recipe;
        var room = places.Add("Aroid room");
        places.Add("Shelf", room);
        places.Add("Aroid shelf", room);

        var aroid = await Find("aroid");

        Assert.Equal([$"pots?show={pot.Id}"], (await Find("nursery")).Pots.Select(i => i.Href));
        Assert.Equal("Clear nursery pot, 13 cm", (await Find("bulk")).Pots.Single().Title);
        Assert.Equal("in the notes", (await Find("bulk")).Pots.Single().Meta);
        Assert.Equal([$"soil-mixes?show={mix.Id}"], aroid.Mixes.Select(i => i.Href));
        Assert.Equal(mix.Recipe, aroid.Mixes.Single().Meta);
        Assert.Equal([$"feeds?show={feed.Id}"], aroid.Feeds.Select(i => i.Href));
        Assert.Equal([$"products?show={product.Id}"], aroid.Products.Select(i => i.Href));
        Assert.Equal([$"treatment-recipes?show={recipe.Id}"], aroid.TreatmentRecipes.Select(i => i.Href));
        Assert.Single((await Find("wipe")).TreatmentRecipes);
        Assert.Single((await Find("bark")).Mixes);

        Assert.Equal(["Aroid room", "Aroid shelf"], Titles(aroid.Places));
        Assert.Equal(["Room", "Spot in Aroid room"], aroid.Places.Select(p => p.Meta));
        Assert.Equal($"plants?room={room.Id}", aroid.Places[0].Href);
    }

    [Fact]
    public async Task Tags_are_listed_once_with_the_number_of_plants()
    {
        AddPlant("A", set: p => p.Tags = ["Rare", "Windowsill"]);
        AddPlant("B", set: p => p.Tags = ["rare"]);
        AddPlant("C", set: p => { p.Tags = ["Rare"]; p.DeletedAt = Now; });

        var tags = (await Find("rar")).Tags;

        var tag = Assert.Single(tags);
        Assert.Equal("Rare", tag.Title);
        Assert.Equal("2 plants", tag.Meta);
        Assert.Equal("plants?tag=Rare", tag.Href);
    }
}

public class RecentSearchesTests
{
    [Fact]
    public void A_new_search_goes_first_and_only_five_are_kept()
    {
        IReadOnlyList<string> recent = [];
        foreach (var query in new[] { "a", "b", "c", "d", "e", "f" })
            recent = RecentSearches.Add(recent, query);

        Assert.Equal(["f", "e", "d", "c", "b"], recent);
    }

    [Fact]
    public void The_same_search_in_other_capitals_moves_up_instead_of_repeating()
    {
        var recent = RecentSearches.Add(["Mealybugs", "café"], "  CAFE ");

        Assert.Equal(["CAFE", "Mealybugs"], recent);
    }

    [Fact]
    public void A_blank_search_is_not_kept_and_a_broken_list_reads_as_empty()
    {
        Assert.Equal(["a"], RecentSearches.Add(["a"], "   "));
        Assert.Empty(RecentSearches.Parse("not json"));
        Assert.Empty(RecentSearches.Parse(null));
        Assert.Equal(["a", "b"], RecentSearches.Parse(RecentSearches.ToJson(["a", "b"])));
    }
}

internal sealed class FakePestTreatments : IPestTreatmentRepository
{
    public List<PestTreatment> Items { get; } = [];

    public Task<IReadOnlyList<PestTreatment>> GetAllAsync() =>
        Task.FromResult<IReadOnlyList<PestTreatment>>(Items.Where(t => !t.IsDeleted).ToList());

    public Task<PestTreatment?> GetAsync(Guid id) =>
        Task.FromResult(Items.FirstOrDefault(t => t.Id == id && !t.IsDeleted));

    public Task SaveAsync(PestTreatment treatment)
    {
        Items.RemoveAll(t => t.Id == treatment.Id);
        Items.Add(treatment);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id)
    {
        if (Items.FirstOrDefault(t => t.Id == id) is { } treatment)
            treatment.DeletedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }
}
