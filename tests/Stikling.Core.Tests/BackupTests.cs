using System.Text.Json;
using Stikling.Core.Backup;
using Stikling.Core.Models;
using Stikling.Core.Sellers;

namespace Stikling.Core.Tests;

public class BackupTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    private static Plant Plant(string name, DateTimeOffset updated, Guid? id = null) =>
        new() { Id = id ?? Guid.NewGuid(), Nickname = name, UpdatedAt = updated };

    [Fact]
    public void Plants_only_on_the_backup_are_added()
    {
        var incoming = Plant("Coleus", Now);

        var result = BackupMerge.Merge<Plant>([], [incoming]);

        Assert.Equal([incoming], result.ToSave);
        Assert.Equal(1, result.Added);
        Assert.Equal(0, result.Updated);
    }

    [Fact]
    public void A_newer_version_in_the_backup_wins()
    {
        var id = Guid.NewGuid();
        var mine = Plant("Coleus", Now.AddDays(-2), id);
        var theirs = Plant("Coleus in the kitchen", Now, id);

        var result = BackupMerge.Merge([mine], [theirs]);

        Assert.Equal([theirs], result.ToSave);
        Assert.Equal(1, result.Updated);
    }

    [Fact]
    public void Newer_changes_on_the_device_are_kept()
    {
        var id = Guid.NewGuid();
        var mine = Plant("Coleus in the kitchen", Now, id);
        var theirs = Plant("Coleus", Now.AddDays(-2), id);

        var result = BackupMerge.Merge([mine], [theirs]);

        Assert.Empty(result.ToSave);
        Assert.Equal(1, result.Skipped);
    }

    [Fact]
    public void The_same_plant_twice_in_a_file_keeps_the_newest()
    {
        var id = Guid.NewGuid();
        var older = Plant("Coleus", Now.AddDays(-1), id);
        var newer = Plant("Coleus cutting", Now, id);

        var result = BackupMerge.Merge<Plant>([], [older, newer]);

        Assert.Equal([newer], result.ToSave);
        Assert.Equal(1, result.Added);
    }

    [Fact]
    public void Deleted_plants_in_the_backup_delete_them_here_too()
    {
        var id = Guid.NewGuid();
        var mine = Plant("Coleus", Now.AddDays(-1), id);
        var theirs = Plant("Coleus", Now, id);
        theirs.DeletedAt = Now;

        var result = BackupMerge.Merge([mine], [theirs]);

        var saved = Assert.Single(result.ToSave);
        Assert.True(saved.IsDeleted);
    }

    [Fact]
    public void A_plant_deleted_here_comes_back_from_the_backup()
    {
        var id = Guid.NewGuid();
        var mine = Plant("Coleus", Now, id);
        mine.DeletedAt = Now;
        var theirs = Plant("Coleus", Now.AddDays(-2), id);

        var result = BackupMerge.Merge([mine], [theirs]);

        var saved = Assert.Single(result.ToSave);
        Assert.False(saved.IsDeleted);
        Assert.Equal(1, result.BroughtBack);
        Assert.Equal(0, result.Skipped);
    }

    [Fact]
    public void A_plant_deleted_in_both_places_stays_deleted()
    {
        var id = Guid.NewGuid();
        var mine = Plant("Coleus", Now, id);
        mine.DeletedAt = Now;
        var theirs = Plant("Coleus", Now.AddDays(-2), id);
        theirs.DeletedAt = Now.AddDays(-2);

        var result = BackupMerge.Merge([mine], [theirs]);

        Assert.Empty(result.ToSave);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(0, result.BroughtBack);
    }

    [Fact]
    public void Counts_describe_what_a_file_holds()
    {
        var gone = Plant("Basil", Now);
        gone.DeletedAt = Now;
        var goneCase = new PestCase { DeletedAt = Now };
        var living = new Place { Name = "Living room" };

        var data = new BackupData
        {
            ExportedAt = Now,
            Plants = [Plant("Coleus", Now), gone],
            Photos = [new Photo(), new Photo()],
            PestCases = [new PestCase(), goneCase],
            PestTreatments = [new PestTreatment()],
            Pots = [new Pot { Name = "Clear nursery pot" }],
            SoilMixes = [new SoilMix { Name = "Chunky soil" }],
            Products = [new Product { Name = "Hydro fertiliser" }, new Product { Name = "Silica" }],
            Feeds = [new Feed { Name = "Aroid feed" }],
            TreatmentRecipes = [new TreatmentRecipe { Name = "Alcohol spray" }, new TreatmentRecipe { DeletedAt = Now }],
            Places = [living, new Place { Name = "Windowsill", RoomId = living.Id }, new Place { Name = "Stue", DeletedAt = Now }],
            Settings = [new UserSettings()],
            PutOffs = [new PutOff { Key = "photos" }, new PutOff { Key = "old", DeletedAt = Now }],
            GrowLights = [new GrowLight { Name = "Grow light" }, new GrowLight { Name = "Old", DeletedAt = Now }],
            ReceivedHistories = [new ReceivedHistory { Name = "Mona" }, new ReceivedHistory { Name = "Old", DeletedAt = Now }],
            AwayStretches = [new AwayStretch(), new AwayStretch { DeletedAt = Now }],
            SitterSheets = [new SitterSheet()],
            PropagationCareLogs = [new PropagationCareLog(), new PropagationCareLog { DeletedAt = Now }],
            FloweringMarks = [new FloweringMark(), new FloweringMark { DeletedAt = Now }],
            Problems = [new Problem(), new Problem { DeletedAt = Now }],
            ProblemUpdates = [new ProblemUpdate(), new ProblemUpdate(), new ProblemUpdate { DeletedAt = Now }],
            Wishes = [new Wish { Name = "Pink syngonium" }, new Wish { DeletedAt = Now }],
            Sellers = [new Seller { Name = "Plantorama" }, new Seller { Name = "Old", DeletedAt = Now }],
            OwnCareKinds = [new OwnCareKind { Name = "Misted" }, new OwnCareKind { Name = "Old", DeletedAt = Now }]
        };

        // The deleted plant is in the file, but it isn't something the restore brings back.
        // Spots aren't counted as rooms.
        Assert.Equal(new BackupCounts(1, 0, 0, 2, 0, 1, 1, 1, 1, 2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 1, 1, 1), data.Counts);
        Assert.Equal(BackupData.CurrentVersion, data.Version);
    }

    [Fact]
    public void A_backup_from_before_received_histories_reads_with_none()
    {
        var data = JsonSerializer.Deserialize<BackupData>("""{"version":1,"app":"Stikling","plants":[]}""", new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(data);
        Assert.Empty(data.ReceivedHistories);
        Assert.Empty(data.GrowLights);
        Assert.Empty(data.AwayStretches);
        Assert.Empty(data.SitterSheets);
        Assert.Empty(data.PropagationCareLogs);
        Assert.Empty(data.FloweringMarks);
        Assert.Empty(data.Problems);
        Assert.Empty(data.ProblemUpdates);
        Assert.Empty(data.Wishes);
        Assert.Empty(data.Sellers);
        Assert.Empty(data.OwnCareKinds);
    }

    [Fact]
    public void Own_kinds_of_care_and_the_entries_that_use_them_survive_a_backup_and_a_restore_merges_them()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var kind = new OwnCareKind { Name = "Bottom watered", CountsAs = CareKind.Watered, OnHistory = true, UpdatedAt = Now };
        var entry = new CareLog
        {
            PlantId = Guid.NewGuid(), Kind = CareKind.Watered, OwnKindId = kind.Id, OwnKindName = "Bottom watered", UpdatedAt = Now
        };

        var json = JsonSerializer.Serialize(new BackupData { OwnCareKinds = [kind], CareLogs = [entry] }, options);
        Assert.Contains("\"countsAs\":\"Watered\"", json);
        Assert.Contains("\"ownKindName\":\"Bottom watered\"", json);

        var back = JsonSerializer.Deserialize<BackupData>(json, options)!;

        var restored = Assert.Single(back.OwnCareKinds);
        Assert.Equal(("Bottom watered", CareKind.Watered, true), (restored.Name, restored.CountsAs, restored.OnHistory));
        var log = Assert.Single(back.CareLogs);
        Assert.Equal((kind.Id, "Bottom watered"), (log.OwnKindId, log.OwnKindName));
        Assert.Equal(1, BackupMerge.Merge<OwnCareKind>([], back.OwnCareKinds).Added);
        Assert.Equal(0, BackupMerge.Merge([kind], back.OwnCareKinds).Added);
    }

    [Fact]
    public void Sellers_and_the_arrival_of_a_plant_survive_a_backup_and_a_restore_merges_them()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var seller = new Seller { Name = "Plantorama", Kind = SellerKind.OnlineShop, Notes = "Ships on Mondays", UpdatedAt = Now };
        var merged = new Seller { Name = "Old name", MergedIntoId = seller.Id, DeletedAt = Now, UpdatedAt = Now };
        var plant = Plant("Coleus", Now);
        plant.SellerId = seller.Id;
        plant.ArrivedIn = ArrivalRating.Poor;
        plant.ArrivalProblems = [ArrivalProblem.Pests, ArrivalProblem.RootRot];
        plant.ArrivalNote = "Leaves were limp";

        var json = JsonSerializer.Serialize(new BackupData { Sellers = [seller, merged], Plants = [plant] }, options);
        Assert.Contains("\"kind\":\"OnlineShop\"", json);
        Assert.Contains("\"arrivedIn\":\"Poor\"", json);

        var back = JsonSerializer.Deserialize<BackupData>(json, options)!;

        Assert.Equal(2, back.Sellers.Count);
        Assert.Equal(SellerKind.OnlineShop, back.Sellers[0].Kind);
        Assert.Equal(seller.Id, back.Sellers[1].MergedIntoId);
        var restored = Assert.Single(back.Plants);
        Assert.Equal((seller.Id, ArrivalRating.Poor, "Leaves were limp"), (restored.SellerId, restored.ArrivedIn, restored.ArrivalNote));
        Assert.Equal([ArrivalProblem.Pests, ArrivalProblem.RootRot], restored.ArrivalProblems);
        Assert.Equal(2, BackupMerge.Merge<Seller>([], back.Sellers).Added);
        Assert.Equal(0, BackupMerge.Merge([seller, merged], back.Sellers).Added);
    }

    [Fact]
    public async Task A_backup_from_before_sellers_restores_and_adopting_makes_them()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var data = JsonSerializer.Deserialize<BackupData>(
            """{"version":1,"app":"Stikling","plants":[{"id":"7d2d5a2e-0c63-4b5e-8a4c-5e1c0b9a1111","genus":"Coleus","source":"Plantorama","origin":"Purchased"}]}""", options)!;
        Assert.Empty(data.Sellers);

        var plants = new FakePlantRepository();
        foreach (var plant in data.Plants)
            plants.Plants[plant.Id] = plant;
        var sellers = new FakeSellerRepository();

        Assert.Equal(1, await new SellerService(sellers, plants, new FakePropagationRepository()).AdoptSourcesAsync());

        var seller = Assert.Single(sellers.Sellers.Values);
        Assert.Equal("Plantorama", seller.Name);
        var adopted = Assert.Single(plants.Plants.Values);
        Assert.Equal(0, plants.Saves);
        Assert.Equal(seller.Id, (await sellers.GetSellersAsync()).For(adopted.SellerId, adopted.Source)?.Id);
    }

    [Fact]
    public void Flowering_marks_survive_a_backup_and_a_restore_merges_them()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var mark = new FloweringMark
        {
            PlantId = Guid.NewGuid(), Stage = FloweringStage.InFlower, On = LooseDate.Of(2025, 5), UpdatedAt = Now
        };

        var json = JsonSerializer.Serialize(new BackupData { FloweringMarks = [mark] }, options);
        var back = JsonSerializer.Deserialize<BackupData>(json, options)!;

        Assert.Contains("\"stage\":\"InFlower\"", json);
        Assert.Contains("\"on\":\"2025-05\"", json);
        var restored = Assert.Single(back.FloweringMarks);
        Assert.Equal(LooseDate.Of(2025, 5), restored.On);
        Assert.Equal(1, BackupMerge.Merge<FloweringMark>([], back.FloweringMarks).Added);
        Assert.Equal(0, BackupMerge.Merge([mark], back.FloweringMarks).Added);
    }

    [Fact]
    public void Problems_and_their_updates_survive_a_backup_and_a_restore_merges_them()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var problem = new Problem
        {
            SubjectId = Guid.NewGuid(), StartedOn = new DateOnly(2026, 10, 1), Signs = ["YellowLeaves", "Thrips"],
            ClosedOn = new DateOnly(2026, 10, 9), Outcome = ProblemOutcome.GotBetter, UpdatedAt = Now
        };
        var update = new ProblemUpdate
        {
            ProblemId = problem.Id, OccurredOn = new DateOnly(2026, 10, 3), Trend = ProblemTrend.Better,
            Did = ["MovedIt", "Misted"], UpdatedAt = Now
        };

        var json = JsonSerializer.Serialize(new BackupData { Problems = [problem], ProblemUpdates = [update] }, options);
        var back = JsonSerializer.Deserialize<BackupData>(json, options)!;

        Assert.Contains("\"signs\":[\"YellowLeaves\",\"Thrips\"]", json);
        Assert.Contains("\"outcome\":\"GotBetter\"", json);
        Assert.Equal(["YellowLeaves", "Thrips"], Assert.Single(back.Problems).Signs);
        Assert.Equal(["MovedIt", "Misted"], Assert.Single(back.ProblemUpdates).Did);
        Assert.Equal(ProblemTrend.Better, back.ProblemUpdates[0].Trend);
        Assert.Equal(1, BackupMerge.Merge<Problem>([], back.Problems).Added);
        Assert.Equal(0, BackupMerge.Merge([problem], back.Problems).Added);
        Assert.Equal(1, BackupMerge.Merge<ProblemUpdate>([], back.ProblemUpdates).Added);
        Assert.Equal(0, BackupMerge.Merge([update], back.ProblemUpdates).Added);
    }

    [Fact]
    public async Task A_large_backup_read_from_a_stream_keeps_values_this_version_reads_as_null()
    {
        // The app reads data.json from the zip as a stream, in parts. A real collection is bigger
        // than one part, and most history entries are notes without an event.
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { DefaultBufferSize = 256 };
        var problem = new Problem { SubjectId = Guid.NewGuid(), StartedOn = new DateOnly(2026, 10, 1), UpdatedAt = Now };
        var data = new BackupData
        {
            Timeline = [.. Enumerable.Range(0, 200).Select(i => new TimelineEntry { SubjectId = Guid.NewGuid(), Text = $"note {i}", UpdatedAt = Now })],
            FloweringMarks = [new FloweringMark { PlantId = Guid.NewGuid(), On = LooseDate.Of(2025, 5), UpdatedAt = Now }],
            Problems = [problem],
            ProblemUpdates = [new ProblemUpdate { ProblemId = problem.Id, OccurredOn = new DateOnly(2026, 10, 3), UpdatedAt = Now }]
        };
        using var stream = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(data, options));

        var back = await JsonSerializer.DeserializeAsync<BackupData>(stream, options);

        Assert.Equal(200, back!.Timeline.Count);
        Assert.All(back.Timeline, e => Assert.Null(e.Event));
        Assert.Null(Assert.Single(back.FloweringMarks).Stage);
        Assert.Null(Assert.Single(back.Problems).Outcome);
        Assert.Null(Assert.Single(back.ProblemUpdates).Trend);
    }

    [Fact]
    public void Wishes_survive_a_backup_and_a_restore_merges_them()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var wish = new Wish
        {
            Genus = "Syngonium", Name = "that pink one", FoundAt = "Plant fair", MostToPay = 150.5m,
            GotOn = new DateOnly(2026, 10, 1), PlantId = Guid.NewGuid(), UpdatedAt = Now
        };

        var back = JsonSerializer.Deserialize<BackupData>(JsonSerializer.Serialize(new BackupData { Wishes = [wish] }, options), options)!;

        var restored = Assert.Single(back.Wishes);
        Assert.Equal((wish.Id, 150.5m, wish.PlantId, wish.GotOn), (restored.Id, restored.MostToPay, restored.PlantId, restored.GotOn));
        Assert.Equal(1, BackupMerge.Merge<Wish>([], back.Wishes).Added);
        Assert.Equal(0, BackupMerge.Merge([wish], back.Wishes).Added);
    }

    [Fact]
    public void A_flowering_stage_from_a_later_version_reads_as_null()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        var mark = JsonSerializer.Deserialize<FloweringMark>("""{"stage":"Seeding","on":"2025-05-02"}""", options)!;

        Assert.Null(mark.Stage);
        Assert.Equal(LooseDate.Of(new DateOnly(2025, 5, 2)), mark.On);
        Assert.Contains("\"stage\":null", JsonSerializer.Serialize(mark, options));
    }

    [Fact]
    public void Propagation_care_survives_a_backup_and_merges_like_the_rest()
    {
        var watered = new PropagationCareLog
        {
            PropagationId = Guid.NewGuid(),
            Kind = PropagationCareKind.MoistureReading,
            Moisture = 4,
            OccurredOn = new DateOnly(2026, 10, 1),
            UpdatedAt = Now
        };
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        var json = JsonSerializer.Serialize(new BackupData { PropagationCareLogs = [watered] }, options);
        var back = JsonSerializer.Deserialize<BackupData>(json, options)!;

        var restored = Assert.Single(back.PropagationCareLogs);
        Assert.Equal(PropagationCareKind.MoistureReading, restored.Kind);
        Assert.Equal(4, restored.Moisture);
        // Stored as text, so reordering the kinds can't change what old data means
        Assert.Contains("\"MoistureReading\"", json);
        Assert.Equal(1, back.Counts.PropagationCareEntries);
        Assert.Equal(1, BackupMerge.Merge<PropagationCareLog>([], back.PropagationCareLogs).Added);
    }

    [Fact]
    public void Away_stretches_and_the_sitter_sheet_survive_a_backup_and_a_restore()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var stretch = new AwayStretch
        {
            From = new DateOnly(2026, 12, 20), Until = new DateOnly(2027, 1, 4), Kind = AwayKind.Quiet, UpdatedAt = Now
        };
        var sheet = new SitterSheet
        {
            Layout = SheetLayout.Chart, Visits = VisitPattern.Weekly, DefaultMethod = WaterMethod.Soak,
            UseOwnWords = true, OwnWords = "A glass each", HasMeter = true, MeterWhere = "In the drawer",
            GeneralNote = "Spare key under the pot", HideNotes = true, LeftOff = [Guid.NewGuid()], UpdatedAt = Now
        };
        var plant = Plant("Coleus", Now);
        plant.WaterBy = WaterMethod.Top;
        plant.SitterNote = "Turn it a quarter";
        var cutting = new Propagation { Nickname = "Pilea", SitterNote = "Change the water", UpdatedAt = Now };

        var json = JsonSerializer.Serialize(
            new BackupData { Plants = [plant], Propagations = [cutting], AwayStretches = [stretch], SitterSheets = [sheet] }, options);
        var back = JsonSerializer.Deserialize<BackupData>(json, options)!;

        Assert.Contains("\"kind\":\"Quiet\"", json);
        Assert.Contains("\"waterBy\":\"Top\"", json);
        Assert.Equal(stretch.Until, Assert.Single(back.AwayStretches).Until);
        Assert.Equal(AwayKind.Quiet, back.AwayStretches[0].Kind);
        var restored = Assert.Single(back.SitterSheets);
        Assert.Equal(SitterSheet.SheetId, restored.Id);
        Assert.Equal(SheetLayout.Chart, restored.Layout);
        Assert.Equal(sheet.LeftOff, restored.LeftOff);
        Assert.Equal("A glass each", restored.OwnWords);
        Assert.Equal(WaterMethod.Top, back.Plants[0].WaterBy);
        Assert.Equal("Turn it a quarter", back.Plants[0].SitterNote);
        Assert.Equal("Change the water", back.Propagations[0].SitterNote);

        // A restore merges them like any record: the newest wins
        var older = new SitterSheet { Layout = SheetLayout.Visits, UpdatedAt = Now.AddDays(-2) };
        Assert.Equal(SheetLayout.Chart, Assert.Single(BackupMerge.Merge([older], back.SitterSheets).ToSave).Layout);
        Assert.Equal(1, BackupMerge.Merge<AwayStretch>([], back.AwayStretches).Added);
    }

    [Fact]
    public void Older_plants_and_sheets_read_with_the_new_fields_unset()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        var plant = JsonSerializer.Deserialize<Plant>("""{"nickname":"Coleus"}""", options)!;
        var cutting = JsonSerializer.Deserialize<Propagation>("""{"nickname":"Pilea"}""", options)!;
        var sheet = JsonSerializer.Deserialize<SitterSheet>("""{"id":"b3c8d1e6-7a42-4f09-8e15-2d6a9c4f7b30"}""", options)!;
        var settings = JsonSerializer.Deserialize<UserSettings>("""{"theme":"Dark"}""", options)!;

        Assert.Null(plant.WaterBy);
        Assert.Null(plant.SitterNote);
        Assert.Null(cutting.SitterNote);
        Assert.Equal(SheetLayout.Labels, sheet.Layout);
        Assert.Equal(VisitPattern.TwiceAWeek, sheet.Visits);
        Assert.Equal(WaterMethod.Bottom, sheet.DefaultMethod);
        Assert.False(sheet.HidePhotos || sheet.HidePlaces || sheet.HideHowWatered || sheet.HidePace || sheet.HideNotes);
        Assert.Empty(sheet.LeftOff);
        Assert.Null(settings.BackListLength);
    }

    [Fact]
    public void A_pest_case_and_its_treatments_survive_a_restore()
    {
        var caseId = Guid.NewGuid();
        var mine = new PestCase { Id = caseId, UpdatedAt = Now.AddDays(-3) };
        var theirs = new PestCase { Id = caseId, UpdatedAt = Now, Status = PestCaseStatus.Resolved };
        var treatment = new PestTreatment { CaseId = caseId, UpdatedAt = Now, What = "Alcohol spray" };

        var cases = BackupMerge.Merge([mine], [theirs]);
        var treatments = BackupMerge.Merge<PestTreatment>([], [treatment]);

        Assert.Equal(PestCaseStatus.Resolved, Assert.Single(cases.ToSave).Status);
        Assert.Equal(caseId, Assert.Single(treatments.ToSave).CaseId);
        Assert.Equal(1, treatments.Added);
    }

    [Fact]
    public void A_photo_framed_since_the_backup_keeps_its_frame_and_a_newer_one_in_the_backup_wins()
    {
        var id = Guid.NewGuid();
        var framedHere = new Photo { Id = id, UpdatedAt = Now, Frame = new PhotoFrame(0.3, 0.5, 2) };
        var inOldBackup = new Photo { Id = id, UpdatedAt = Now.AddDays(-3) };

        Assert.Empty(BackupMerge.Merge([framedHere], [inOldBackup]).ToSave);

        var centredHere = new Photo { Id = id, UpdatedAt = Now.AddDays(-3) };
        var framedInBackup = new Photo { Id = id, UpdatedAt = Now, Frame = new PhotoFrame(0.3, 0.5, 2) };

        Assert.Equal(framedInBackup.Frame, Assert.Single(BackupMerge.Merge([centredHere], [framedInBackup]).ToSave).Frame);
    }

    [Fact]
    public void Treatment_recipes_survive_a_restore()
    {
        var id = Guid.NewGuid();
        var mine = new TreatmentRecipe { Id = id, Name = "Alcohol spray", UpdatedAt = Now.AddDays(-3) };
        var theirs = new TreatmentRecipe
        {
            Id = id,
            Name = "Alcohol spray",
            UpdatedAt = Now,
            Ingredients = [new RecipeIngredient { Name = "Isopropyl alcohol", Amount = 250 }]
        };

        var merged = BackupMerge.Merge([mine], [theirs]);

        Assert.Equal("Isopropyl alcohol", Assert.Single(Assert.Single(merged.ToSave).Ingredients).Name);
        Assert.Equal(1, merged.Updated);
    }

    [Fact]
    public void Settings_survive_a_restore_and_the_newest_wins()
    {
        var mine = new UserSettings { Theme = ThemeMode.Light, UpdatedAt = Now.AddDays(-3) };
        var theirs = new UserSettings { Theme = ThemeMode.Dark, PhotoReminder = true, GooglePhotos = true, UpdatedAt = Now };

        var merged = BackupMerge.Merge([mine], [theirs]);
        var older = BackupMerge.Merge([theirs], [mine]);

        var saved = Assert.Single(merged.ToSave);
        Assert.Equal(ThemeMode.Dark, saved.Theme);
        Assert.True(saved.PhotoReminder);
        Assert.True(saved.GooglePhotos);
        Assert.Empty(older.ToSave);
        Assert.Equal(1, BackupMerge.Merge<UserSettings>([], [theirs]).Added);
    }

    [Fact]
    public void Put_offs_survive_a_restore_and_the_newest_wins()
    {
        var mine = new PutOff { Id = PutOff.IdFor("photos"), Key = "photos", Until = new DateOnly(2026, 10, 1), UpdatedAt = Now.AddDays(-1) };
        var theirs = new PutOff { Id = PutOff.IdFor("photos"), Key = "photos", Until = new DateOnly(2026, 10, 8), UpdatedAt = Now };

        var merged = BackupMerge.Merge([mine], [theirs]);
        var older = BackupMerge.Merge([theirs], [mine]);

        Assert.Equal(new DateOnly(2026, 10, 8), Assert.Single(merged.ToSave).Until);
        Assert.Empty(older.ToSave);
        Assert.Equal(1, BackupMerge.Merge<PutOff>([], [theirs]).Added);
    }

    [Fact]
    public void Pots_survive_a_restore()
    {
        var id = Guid.NewGuid();
        var mine = new Pot { Id = id, Name = "Clear nursery pot", Owned = 5, UpdatedAt = Now.AddDays(-3) };
        var theirs = new Pot { Id = id, Name = "Clear nursery pot", Owned = 6, UpdatedAt = Now, TopCm = 13 };

        var merged = BackupMerge.Merge([mine], [theirs]);

        var restored = Assert.Single(merged.ToSave);
        Assert.Equal(6, restored.Owned);
        Assert.Equal(13, restored.TopCm);
        Assert.Equal(1, merged.Updated);
    }

    [Fact]
    public void A_renamed_room_and_where_a_merged_one_went_survive_a_restore()
    {
        var living = new Place { Name = "living room", UpdatedAt = Now.AddDays(-3) };
        var renamed = new Place { Id = living.Id, Name = "Living room", UpdatedAt = Now };
        var stue = new Place { Name = "Stue", MergedIntoId = living.Id, DeletedAt = Now, UpdatedAt = Now };

        var merged = BackupMerge.Merge([living], [renamed, stue]);

        Assert.Equal("Living room", merged.ToSave.Single(p => p.Id == living.Id).Name);
        Assert.Equal(living.Id, merged.ToSave.Single(p => p.Id == stue.Id).MergedIntoId);
    }

    [Fact]
    public void A_mix_and_the_order_of_its_ingredients_survive_a_restore()
    {
        var id = Guid.NewGuid();
        var mine = new SoilMix { Id = id, Name = "Chunky soil", UpdatedAt = Now.AddDays(-3) };
        var theirs = new SoilMix
        {
            Id = id,
            Name = "Chunky soil",
            UpdatedAt = Now,
            Ingredients =
            [
                new MixIngredient { Name = "Potting soil" },
                new MixIngredient { Name = "Bark" },
                new MixIngredient { Name = "Perlite" }
            ]
        };

        var merged = BackupMerge.Merge([mine], [theirs]);

        var restored = Assert.Single(merged.ToSave);
        Assert.Equal(["Potting soil", "Bark", "Perlite"], restored.Ingredients.Select(i => i.Name));
    }
}
