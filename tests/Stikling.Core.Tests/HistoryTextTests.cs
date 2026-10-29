using System.Globalization;
using System.Text.Json;
using Stikling.Core.Backup;
using Stikling.Core.Care;
using Stikling.Core.Examples;
using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Propagations;
using Stikling.Core.Timeline;

namespace Stikling.Core.Tests;

public class HistoryTextTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 19, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 9, 19);
    private static readonly CultureInfo Danish = CultureInfo.GetCultureInfo("da-DK");
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-GB");

    private static string EnglishLabel(Enum value) => value switch
    {
        GrowingMedium.Leca => "LECA",
        RootingAid.RootingPowder => "Rooting powder",
        RootingAid.HeatMat => "Heat mat",
        CareKind.LeavesCleaned => "Leaves cleaned",
        CareKind.MoistureReading => "Moisture reading",
        PlantStatus.GivenAway => "Given away",
        LightLevel.BrightIndirect => "Bright indirect",
        _ => value.ToString()
    };

    private static string DanishLabel(Enum value) => value switch
    {
        PlantStatus.Active => "Aktiv",
        PlantStatus.Died => "Død",
        GrowingMedium.Soil => "Jord",
        GrowingMedium.Leca => "LECA",
        CareKind.Watered => "Vandet",
        CareKind.Fertilised => "Gødet",
        PropagationType.Corm => "Knold",
        _ => value.ToString()
    };

    // A line from plain values: text, numbers, days, enums and lists of enums
    private static HistoryLine L(string key, params object[] values) => HistoryText.Line(key, [.. values.Select(v => v switch
    {
        string text => HistoryArg.Of(text),
        int number => HistoryArg.Of(number),
        decimal number => HistoryArg.Of(number),
        DateOnly day => HistoryArg.Of(day),
        Enum value => HistoryArg.Of(value),
        Enum[] list => new HistoryArg { Enums = [.. list.Select(HistoryArg.EnumText)] },
        _ => throw new ArgumentException("Not a value", nameof(values))
    })]);

    private static string Json(HistoryLine line) => JsonSerializer.Serialize(line);

    /// <summary>One line for every template, with the English it has always been written as.</summary>
    private static readonly (HistoryLine Line, string English)[] Rows =
    [
        (L("StatusChanged", PlantStatus.Died, PlantStatus.Active), "Status: Died (was Active)"),
        (L("WhatHappened", "Rot"), "What happened: Rot"),
        (L("DepartureSoldToFor", "Anna", "150"), "Sold to Anna for 150"),
        (L("DepartureSoldTo", "Anna"), "Sold to Anna"),
        (L("DepartureSoldFor", "150"), "Sold for 150"),
        (L("DepartureSwappedFor", "a Hoya"), "Swapped for a Hoya"),
        (L("DepartureGivenToSwapped", "Anna", "a Hoya"), "Given to Anna · swapped for a Hoya"),
        (L("DepartureGivenTo", "Anna"), "Given to Anna"),
        (L("RemovedFromRoom"), "Removed from its room"),
        (L("RemovedFrom", "Kitchen"), "Removed from Kitchen"),
        (L("PlacedIn", "Kitchen / Sill"), "Placed in Kitchen / Sill"),
        (L("MovedTo", "Bedroom"), "Moved to Bedroom"),
        (L("MovedFromTo", "Living room", "Bedroom"), "Moved from Living room to Bedroom"),
        (L("MediumChanged", GrowingMedium.Leca, GrowingMedium.Soil), "Now grows in LECA (was Soil)"),
        (L("HowManyChanged", 2, 3), "How many: 2 to 3"),
        (L("LightChangedFromUnset", LightLevel.Low), "Light: Low (was not set)"),
        (L("LightChanged", LightLevel.BrightIndirect, LightLevel.Low), "Light: Bright indirect (was Low)"),
        (L("LightCleared"), "Light setting cleared"),
        (L("PottedInto", "Clear pot"), "Potted into Clear pot"),
        (L("TakenOutOfPot"), "Taken out of its pot"),
        (L("NowStandsIn", "Cache pot"), "Now stands in Cache pot"),
        (L("OuterPotRemoved"), "No longer in an outer pot"),
        (L("NowInMix", "Chunky soil"), "Now in Chunky soil"),
        (L("MixRemoved"), "No longer in a saved mix"),
        (L("FedStopped"), "No longer fed with the watering"),
        (L("FedEverySecond", "Aroid feed"), "Fed with the watering: Aroid feed, every second watering"),
        (L("FedEveryThird", "Aroid feed"), "Fed with the watering: Aroid feed, every third watering"),
        (L("FedEveryWatering", "Aroid feed"), "Fed with the watering: Aroid feed, every watering"),
        (L("PotWentWithIt"), "Its pot went with it"),
        (L("OuterPotWentWithIt"), "Its outer pot went with it"),
        (L("PotsWentWithIt"), "Its pot and outer pot went with it"),
        (L("PotsStayed"), "Its pots stayed here"),
        (L("WateredInOuterPot"), "Now watered in the outer pot"),
        (L("NotWateredInOuterPot"), "No longer watered in the outer pot"),
        (L("Tagged", "Variegated"), "Tagged Variegated"),
        (L("Untagged", "Flowering"), "No longer tagged Flowering"),
        (L("PutInQuarantine"), "Put in quarantine"),
        (L("OutOfQuarantine"), "Out of quarantine"),
        (L("WentDormant"), "Went dormant"),
        (L("WokeUp"), "Woke up from dormancy"),
        (L("StartedMoveToSemiHydro"), "Started the move to semi-hydro"),
        (L("StartedMoveTo", GrowingMedium.Leca), "Started the move to LECA"),
        (L("RootsWashed"), "Roots washed"),
        (L("FirstWaterRoot"), "First new water root"),
        (L("FirstNewLeaf"), "First new leaf"),
        (L("SettledInSemiHydro", 1), "Settled in to semi-hydro after 1 week"),
        (L("SettledInSemiHydro", 3), "Settled in to semi-hydro after 3 weeks"),
        (L("SettledIn", GrowingMedium.Leca, 1), "Settled in to LECA after 1 week"),
        (L("SettledIn", GrowingMedium.Leca, 12), "Settled in to LECA after 12 weeks"),
        (L("StoppedTrackingMoveSemiHydro"), "Stopped tracking the move to semi-hydro"),
        (L("StoppedTrackingMove", GrowingMedium.Leca), "Stopped tracking the move to LECA"),
        (L("StageChanged", PropagationStage.Rooting, PropagationStage.Started), "Stage: Rooting (was Started)"),
        (L("CountChanged", 5, 4), "Count: 5 (was 4)"),
        (L("PottedUpChanged", 2, 1), "Potted up: 2 (was 1)"),
        (L("GivenAwayChanged", 2, 1), "Given away: 2 (was 1)"),
        (L("FailedChanged", 2, 1), "Failed: 2 (was 1)"),
        (L("NewSetup", "Glass jar"), "New setup: Glass jar"),
        (L("FirstRootOnDay", 12), "First root on day 12"),
        (L("FirstRootCleared"), "First root date cleared"),
        (L("FirstLeafOnDay", 20), "First leaf on day 20"),
        (L("FirstLeafCleared"), "First leaf date cleared"),
        (L("SeedsCameUp", 12, 20), "Seeds: 12 of 20 came up"),
        (L("SeedCountCleared"), "Seed count cleared"),
        (L("CormSize", 8m), "Corm size: 8 mm"),
        (L("CormSize", 8.5m), "Corm size: 8.5 mm"),
        (L("CormSizeCleared"), "Corm size cleared"),
        (L("NoRootingAids"), "No rooting aids"),
        (L("RootingAids", (object)new Enum[] { RootingAid.RootingPowder, RootingAid.HeatMat }), "Rooting aids: Rooting powder, Heat mat"),
        (L("CutEndDriedDays", 1), "Cut end dried: 1 day"),
        (L("CutEndDriedDays", 2), "Cut end dried: 2 days"),
        (L("CutEndDriedHours", 1), "Cut end dried: 1 hour"),
        (L("CutEndDriedHours", 6), "Cut end dried: 6 hours"),
        (L("CutEndNotDried"), "Cut end no longer dried first"),
        (L("AddedLivesElsewhere"), "Added as a plant that lives with someone else"),
        (L("AddedToCollection"), "Added to collection"),
        (L("AddedToCollectionYear", 2024), "Added to collection, 2024"),
        (L("AddedToCollectionMonth", new DateOnly(2024, 6, 1)), "Added to collection, June 2024"),
        (L("AddedPropagated"), "Added as a propagated plant"),
        (L("AddedPropagatedYear", 2024), "Added as a propagated plant, 2024"),
        (L("AddedPropagatedMonth", new DateOnly(2024, 6, 1)), "Added as a propagated plant, June 2024"),
        (L("SplitOffFrom", "Big alocasia"), "Split off from Big alocasia"),
        (L("PottedUpFromPropagation"), "Potted up from a propagation"),
        (L("IdentifiedAs", "Hoya carnosa"), "Identified as Hoya carnosa"),
        (L("PottedTogetherWith", "Big alocasia"), "Potted together with Big alocasia"),
        (L("PottedInWithIt", "Small one", 3), "Small one potted in with it, 3 in the pot now"),
        (L("TakenOutOfSharedPot"), "Taken out of the pot it shared"),
        (L("TakenOutOfPotOf", "Big alocasia"), "Taken out of Big alocasia's pot"),
        (L("PlantTakenOut", "Small one", 2), "Small one taken out, 2 in the pot now"),
        (L("SomeDied", 1, 2), "1 died, 2 left"),
        (L("GaveSomeAway", 1, 2), "Gave 1 away, 2 left"),
        (L("GaveSomeTo", 1, "Anna", 2), "Gave 1 to Anna, 2 left"),
        (L("PottedUpOnTheirOwn", 2, "A 2, A 3"), "Potted 2 up on their own: A 2, A 3"),
        (L("Started", 3, PropagationType.Corm, GrowingMedium.Leca), "Started 3× corm in LECA"),
        (L("BatchOf", 3, PropagationType.Corm, GrowingMedium.Leca), "3× corm in LECA"),
        (L("PottedUp", 2, "A, B"), "Potted up 2: A, B"),
        (L("FailedWithReasonNote", 2, FailReason.Rotted, "left too wet"), "2 failed: rotted, left too wet"),
        (L("FailedWithReason", 1, FailReason.DriedOut), "1 failed: dried out"),
        (L("FailedWithNote", 1, "was a weak one"), "1 failed: was a weak one"),
        (L("Failed", 1), "1 failed"),
        (L("FinishedAll", 1, 2, 3), "Finished: 1 potted up, 2 given away, 3 failed"),
        (L("FinishedPottedGiven", 1, 2), "Finished: 1 potted up, 2 given away"),
        (L("FinishedPottedFailed", 1, 2), "Finished: 1 potted up, 2 failed"),
        (L("FinishedGivenFailed", 1, 2), "Finished: 1 given away, 2 failed"),
        (L("FinishedPotted", 1), "Finished: 1 potted up"),
        (L("FinishedGiven", 1), "Finished: 1 given away"),
        (L("FinishedFailed", 1), "Finished: 1 failed"),
        (L("FinishedNothing"), "Finished: nothing yet"),
        (L("GaveToWho", 2, "Anna"), "Gave 2 to Anna"),
        (L("GaveAway", 2), "Gave 2 away"),
        (L("FloweringBuds"), "Spike or buds"),
        (L("FloweringInFlower"), "In flower"),
        (L("FloweringDone"), "Done flowering"),
        (L("ProblemOpened", (object)new Enum[] { ProblemSign.YellowLeaves, ProblemSign.Drooping }), "Something's wrong: yellow leaves, drooping"),
        (L("ProblemUpdateTrendActions", ProblemTrend.Better, (object)new Enum[] { ProblemAction.MovedIt, ProblemAction.WateredLess }), "Better · Moved it, watered less"),
        (L("ProblemUpdateTrend", ProblemTrend.Same), "About the same"),
        (L("ProblemUpdateActions", (object)new Enum[] { ProblemAction.MovedIt }), "Moved it"),
        (L("ProblemUpdateEmpty"), "Update on the problem"),
        (L("ProblemClosed", ProblemOutcome.GotBetter), "Problem over: got better"),
        (L("ProblemReopened"), "The problem came back"),
        (L("LookedOver"), "Looked over"),
        (L("ReceivedFrom", "Anna"), "From Anna"),
        (L("TextLine", "A note about it"), "A note about it"),
        (HistoryText.CareLine(new CareLog { Kind = CareKind.Watered }, null, null), "Watered"),
        (HistoryText.CareLine(new CareLog { Kind = CareKind.MoistureReading, Moisture = 4, Ec = 1.2m, Ph = 6m }, null, null), "Moisture reading: 4/10 · EC 1.2 · pH 6.0"),
        (HistoryText.CareLine(new CareLog { Kind = CareKind.Fertilised, FeedName = "Aroid feed", WaterLitres = 2m, Notes = "Strong" }, null, "Silica, 0.5 ml/L"),
            "Fertilised with Aroid feed (2 L): Silica, 0.5 ml/L · Strong"),
        (HistoryText.CareLine(new CareLog { Kind = CareKind.Watered, WaterLitres = 0.5m, Notes = "Rainwater" }, null, null), "Watered (500 ml): Rainwater"),
        (HistoryText.CareLine(new CareLog { Kind = CareKind.Rotated }, "Mist", null), "Mist")
    ];

    public static IEnumerable<object[]> AllRows => Rows.Select(r => new object[] { r.Line, r.English });

    [Theory]
    [MemberData(nameof(AllRows))]
    public void A_line_is_written_in_the_English_it_always_had(HistoryLine line, string english) =>
        Assert.Equal(english, HistoryText.English(line, EnglishLabel));

    [Theory]
    [MemberData(nameof(AllRows))]
    public void English_text_reads_back_into_the_same_line(HistoryLine line, string english)
    {
        var parsed = HistoryText.Parse(english, EnglishLabel);
        var read = Assert.Single(parsed);
        // A line the person wrote, and the name of an own kind of care, are not something to read back into a template
        if (line.Key == "TextLine" || line.Key == HistoryText.CareKey && line.Args[0].Text is not null)
        {
            Assert.Null(read);
            return;
        }
        Assert.NotNull(read);
        if (line.Key == HistoryText.CareKey)
        {
            // Products and notes read the same, so what follows the colon comes back as the note
            Assert.Equal(english, HistoryText.English(read, EnglishLabel));
            return;
        }
        Assert.Equal(Json(line), Json(read));
    }

    [Fact]
    public void Every_template_has_a_row_and_its_wording_in_English_and_Danish()
    {
        var keys = Rows.Select(r => r.Line.Key).ToHashSet();
        Assert.All(HistoryText.Keys, key => Assert.Contains(key, keys));

        var resources = new System.Resources.ResourceManager("Stikling.Core.Resources.History", typeof(HistoryText).Assembly);
        foreach (var key in HistoryText.Keys)
        foreach (var name in HistoryText.ResourceNames(key))
        {
            Assert.False(string.IsNullOrEmpty(resources.GetString(name, CultureInfo.InvariantCulture)), $"{name} has no English");
            Assert.False(string.IsNullOrEmpty(resources.GetString(name, Danish)), $"{name} has no Danish");
        }
    }

    [Fact]
    public void Every_line_can_be_shown_in_Danish()
    {
        foreach (var (line, _) in Rows)
        {
            var shown = HistoryText.Render(line, DanishLabel, Danish);
            Assert.NotEqual(line.Key, shown);
            Assert.DoesNotContain("{", shown);
        }
    }

    [Fact]
    public void A_line_is_shown_in_the_language_asked_for()
    {
        Assert.Equal("Status: Død (var Aktiv)", HistoryText.Render(L("StatusChanged", PlantStatus.Died, PlantStatus.Active), DanishLabel, Danish));
        Assert.Equal("Har vænnet sig til LECA efter 1 uge", HistoryText.Render(L("SettledIn", GrowingMedium.Leca, 1), DanishLabel, Danish));
        Assert.Equal("Har vænnet sig til LECA efter 3 uger", HistoryText.Render(L("SettledIn", GrowingMedium.Leca, 3), DanishLabel, Danish));
        Assert.Equal("Startede 3× knold i LECA", HistoryText.Render(L("Started", 3, PropagationType.Corm, GrowingMedium.Leca), DanishLabel, Danish));
        Assert.Equal("Tilføjet til samlingen, juni 2024", HistoryText.Render(L("AddedToCollectionMonth", new DateOnly(2024, 6, 1)), DanishLabel, Danish));
        Assert.Equal("Vandet med Aroid feed (2 L)",
            HistoryText.Render(HistoryText.CareLine(new CareLog { Kind = CareKind.Watered, FeedName = "Aroid feed", WaterLitres = 2m }, null, null), DanishLabel, Danish));
        Assert.Equal("2 mislykkedes: skimmel", HistoryText.Render(L("FailedWithReason", 2, FailReason.Mould), DanishLabel, Danish));
    }

    [Fact]
    public void Rendering_leaves_the_language_of_the_app_as_it_was()
    {
        var was = CultureInfo.CurrentUICulture;
        HistoryText.Render(L("LookedOver"), DanishLabel, Danish);
        HistoryText.English([L("LookedOver")], EnglishLabel);
        Assert.Equal(was, CultureInfo.CurrentUICulture);
    }

    // Show

    private static TimelineEntry Entry(string? text, TimelineKind kind = TimelineKind.Change, List<HistoryLine>? lines = null) =>
        new() { Kind = kind, Text = text, Lines = lines };

    [Fact]
    public void An_old_entry_is_read_from_its_English_text_and_shown_in_Danish()
    {
        var entry = Entry("Moved from Living room to Bedroom\nStatus: Died (was Active)");

        Assert.Equal("Flyttet fra Living room til Bedroom\nStatus: Død (var Aktiv)",
            HistoryText.Show(entry, DanishLabel, EnglishLabel, Danish));
    }

    [Fact]
    public void A_line_that_matches_no_template_is_shown_as_it_was_saved()
    {
        var entry = Entry("Roots washed\nSomething the app never wrote");

        Assert.Equal("Rødder vasket\nSomething the app never wrote", HistoryText.Show(entry, DanishLabel, EnglishLabel, Danish));
    }

    [Fact]
    public void English_shows_the_saved_text_for_old_entries_and_new_ones()
    {
        var old = Entry("Moved from A to B\nStatus: Died (was Active)");
        var current = Entry("Went dormant", lines: [L("WentDormant")]);

        Assert.Equal(old.Text, HistoryText.Show(old, EnglishLabel, EnglishLabel, English));
        Assert.Equal("Went dormant", HistoryText.Show(current, EnglishLabel, EnglishLabel, English));
        Assert.Equal("Went dormant", HistoryText.Show(current, EnglishLabel, EnglishLabel, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void An_entry_with_lines_is_shown_from_them_and_a_key_this_version_doesnt_know_falls_back_to_the_text()
    {
        var entry = Entry("Went dormant\nA line from the future",
            lines: [L("WentDormant"), new HistoryLine { Key = "FromTheFuture", Args = [HistoryArg.Of("x")] }]);
        Assert.Equal("Gik i hvile\nA line from the future", HistoryText.Show(entry, DanishLabel, EnglishLabel, Danish));

        // The counts differ, so there is no telling which line is which
        var shifted = Entry("One\nTwo\nThree", lines: [L("WentDormant"), new HistoryLine { Key = "FromTheFuture" }]);
        Assert.Equal("One\nTwo\nThree", HistoryText.Show(shifted, DanishLabel, EnglishLabel, Danish));
    }

    [Fact]
    public void A_value_of_an_enum_this_version_doesnt_have_falls_back_to_the_text()
    {
        var line = new HistoryLine { Key = "StatusChanged", Args = [new HistoryArg { Enum = "PlantStatus.Floated" }, HistoryArg.Of(PlantStatus.Active)] };
        var entry = Entry("Status: Floated (was Active)", lines: [line]);

        Assert.Equal("Status: Floated (was Active)", HistoryText.Show(entry, DanishLabel, EnglishLabel, Danish));
    }

    [Fact]
    public void Notes_and_photo_captions_are_left_alone()
    {
        Assert.Equal("Roots washed", HistoryText.Show(Entry("Roots washed", TimelineKind.Note), DanishLabel, EnglishLabel, Danish));
        Assert.Equal("Looked over", HistoryText.Show(Entry("Looked over", TimelineKind.Photo), DanishLabel, EnglishLabel, Danish));
        Assert.Equal("", HistoryText.Show(Entry(null), DanishLabel, EnglishLabel, Danish));
    }

    [Fact]
    public void Only_the_first_line_of_a_problem_entry_is_read_because_the_rest_is_the_persons_note()
    {
        var entry = Entry("Worse\nLooked over", lines: null);
        entry.Event = TimelineEvent.Problem;

        var shown = HistoryText.Show(entry, DanishLabel, EnglishLabel, Danish).Split('\n');
        Assert.Equal("Værre", shown[0]);
        Assert.Equal("Looked over", shown[1]);
    }

    [Fact]
    public void An_old_care_entry_is_shown_in_Danish()
    {
        var entry = Entry("Fertilised with Aroid feed (2 L): Silica, 0.5 ml/L · EC 1.2");

        Assert.Equal("Gødet med Aroid feed (2 L): Silica, 0.5 ml/L · EC 1.2", HistoryText.Show(entry, DanishLabel, EnglishLabel, Danish));
    }

    // The writers

    private static string Label(Enum value) => value switch
    {
        GrowingMedium.Leca => "LECA",
        _ => value.ToString()
    };

    // Like the labels of the app, which follow the language it is in
    private static string InAppLanguage(Enum value) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "da" ? DanishLabel(value) : Label(value);

    private static void AssertInEnglishAndWithLines(TimelineEntry entry)
    {
        Assert.NotNull(entry.Lines);
        Assert.NotEmpty(entry.Lines);
        Assert.Equal(entry.Text, HistoryText.English(entry.Lines, Label));
    }

    [Fact]
    public async Task A_plant_update_saves_lines_and_the_English_text_even_when_the_app_is_in_Danish()
    {
        var plants = new FakePlantRepository();
        var timeline = new FakeTimelineRepository();
        var service = new PlantService(plants, timeline, new FixedTime(Now));
        var plant = new Plant { Nickname = "A" };
        await service.CreateAsync(plant);
        var before = plant.Copy();
        plant.Status = PlantStatus.Died;
        plant.Tags = ["Rare"];

        var was = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = Danish;
        try
        {
            // The label of the page is in Danish; the saved text still has to be English
            await service.UpdateAsync(before, plant, InAppLanguage);
        }
        finally
        {
            CultureInfo.CurrentUICulture = was;
        }

        var created = timeline.Entries.Single(e => e.Kind == TimelineKind.Created);
        Assert.Equal("Added to collection", created.Text);
        Assert.Equal("AddedToCollection", Assert.Single(created.Lines!).Key);

        var change = timeline.Entries.Single(e => e.Kind == TimelineKind.Change);
        Assert.Equal("Status: Died (was Active)\nTagged Rare", change.Text);
        Assert.Equal(["StatusChanged", "Tagged"], change.Lines!.Select(l => l.Key));
        Assert.Equal("PlantStatus.Died", change.Lines![0].Args[0].Enum);
        Assert.Equal("Status: Død (var Aktiv)\nTagget med Rare", HistoryText.Show(change, DanishLabel, Label, Danish));
    }

    [Fact]
    public async Task A_new_propagation_and_the_plant_it_came_from_get_lines()
    {
        var plants = new FakePlantRepository();
        var timeline = new FakeTimelineRepository();
        var parent = new Plant { Nickname = "Big alocasia" };
        plants.Plants[parent.Id] = parent;
        var service = new PropagationService(new FakePropagationRepository(), plants, timeline, new FakePropagationCareLogRepository(),
            new FakeCareLogRepository(), new FakePhotoRepository(), new FixedTime(Now));
        var propagation = new Propagation { Nickname = "Corms", ParentPlantId = parent.Id, Type = PropagationType.Corm, Medium = GrowingMedium.Leca, InitialCount = 3, StartedOn = Today };

        await service.CreateAsync(propagation, Label);

        var started = timeline.Entries.Single(e => e.SubjectId == propagation.Id);
        Assert.Equal("Started 3× corm in LECA", started.Text);
        AssertInEnglishAndWithLines(started);
        var taken = timeline.Entries.Single(e => e.SubjectId == parent.Id);
        Assert.Equal("3× corm in LECA", taken.Text);
        AssertInEnglishAndWithLines(taken);
        Assert.Equal("Startede 3× knold i LECA", HistoryText.Show(started, DanishLabel, Label, Danish));
    }

    [Fact]
    public async Task Failing_and_finishing_a_propagation_saves_lines()
    {
        var timeline = new FakeTimelineRepository();
        var service = new PropagationService(new FakePropagationRepository(), new FakePlantRepository(), timeline, new FakePropagationCareLogRepository(),
            new FakeCareLogRepository(), new FakePhotoRepository(), new FixedTime(Now));
        var propagation = new Propagation { Nickname = "Cutting", Type = PropagationType.Cutting, Medium = GrowingMedium.Water, InitialCount = 1, StartedOn = Today.AddDays(-5) };
        await service.CreateAsync(propagation, Label);

        await service.MarkFailedAsync(propagation, 1, FailReason.Rotted, "left too wet");

        var entry = timeline.Entries.Single(e => e.Event == TimelineEvent.Failed);
        Assert.Equal("1 failed: rotted, left too wet\nFinished: 1 failed", entry.Text);
        Assert.Equal(["FailedWithReasonNote", "FinishedFailed"], entry.Lines!.Select(l => l.Key));
        Assert.Equal("1 mislykkedes: rådnede, left too wet\nFærdig: 1 mislykkedes", HistoryText.Show(entry, DanishLabel, Label, Danish));
    }

    [Fact]
    public async Task A_care_entry_on_the_history_saves_lines_and_the_care_list_follows_the_language()
    {
        var timeline = new FakeTimelineRepository();
        var service = new CareService(new FakeCareLogRepository(), timeline, new FakePlantRepository(), new FakeFeedRepository(),
            new FakeProductRepository(), new FakeOwnCareKindRepository(), new FixedTime(Now));
        var entry = new CareLog { PlantId = Guid.NewGuid(), Kind = CareKind.Repotted, OccurredOn = Today, Notes = "New pot" };

        await service.LogAsync(entry, Label);

        var recorded = Assert.Single(timeline.Entries);
        Assert.Equal("Repotted: New pot", recorded.Text);
        Assert.Equal(HistoryText.CareKey, Assert.Single(recorded.Lines!).Key);

        var watered = new CareLog { Kind = CareKind.Watered, FeedName = "Aroid feed" };
        var was = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = Danish;
        try
        {
            Assert.Equal("Vandet med Aroid feed", CareService.Describe(watered, DanishLabel));
        }
        finally
        {
            CultureInfo.CurrentUICulture = was;
        }
        Assert.Equal("Watered with Aroid feed", CareService.Describe(watered, Label));
    }

    [Fact]
    public void The_example_gets_its_names_and_notes_in_the_language_of_the_app_and_lines_in_its_history()
    {
        var time = new FixedTime(Now);
        var english = ExampleFamily.Build(time);
        ExampleRecords danish;
        var was = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = Danish;
        try
        {
            danish = ExampleFamily.Build(time);
            Assert.Equal("Stor coleus", ExampleFamily.MotherNickname);
        }
        finally
        {
            CultureInfo.CurrentUICulture = was;
        }

        Assert.Equal("Big Coleus", ExampleFamily.MotherNickname);
        Assert.Equal("Big Coleus", english.Plants[0].Nickname);
        Assert.Equal("Stor coleus", danish.Plants[0].Nickname);
        // The same ids, so devices never end up with two copies
        Assert.Equal(english.Plants.Select(p => p.Id), danish.Plants.Select(p => p.Id));
        Assert.Equal(english.Entries.Select(e => e.Id), danish.Entries.Select(e => e.Id));

        foreach (var entry in danish.Entries.Where(e => e.Kind != TimelineKind.Note && e.Kind != TimelineKind.Photo))
            AssertInEnglishAndWithLines(entry);
        Assert.Equal(
            ["Added to collection, " + new DateOnly(Today.Year, Today.Month, 1).AddMonths(-5).ToString("MMMM yyyy", CultureInfo.InvariantCulture),
                "1× cutting in Water", "1× cutting in Water", "Started 1× cutting in Water", "Potted up 1: Lille coleus\nFinished: 1 potted up",
                "Potted up from a propagation", "Started 1× cutting in Water"],
            danish.Entries.Where(e => e.Lines is not null).Select(e => e.Text));
    }

    // Backups

    [Fact]
    public void Lines_survive_a_backup_and_an_older_backup_without_them_restores()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var entry = new TimelineEntry { SubjectId = Guid.NewGuid(), Kind = TimelineKind.Change, UpdatedAt = Now };
        HistoryText.Set(entry, [L("MovedFromTo", "A", "B"), L("StatusChanged", PlantStatus.Died, PlantStatus.Active), L("CormSize", 8.5m)], Label);

        var json = JsonSerializer.Serialize(new BackupData { Timeline = [entry] }, options);
        var back = JsonSerializer.Deserialize<BackupData>(json, options)!;

        var restored = Assert.Single(back.Timeline);
        Assert.Equal(entry.Text, restored.Text);
        Assert.Equal(JsonSerializer.Serialize(entry.Lines, options), JsonSerializer.Serialize(restored.Lines, options));
        Assert.Contains("\"enum\":\"PlantStatus.Died\"", json);
        Assert.Equal(1, BackupMerge.Merge<TimelineEntry>([], back.Timeline).Added);

        var old = JsonSerializer.Deserialize<BackupData>(
            """{"version":1,"app":"Stikling","timeline":[{"id":"7d2d5a2e-0c63-4b5e-8a4c-5e1c0b9a1111","kind":"Change","text":"Went dormant"}]}""", options)!;
        var oldEntry = Assert.Single(old.Timeline);
        Assert.Null(oldEntry.Lines);
        Assert.Equal("Gik i hvile", HistoryText.Show(oldEntry, DanishLabel, Label, Danish));
    }
}
