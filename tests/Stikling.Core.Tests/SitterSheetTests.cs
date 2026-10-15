using Stikling.Core.Away;
using Stikling.Core.Care;
using Stikling.Core.Models;
using Stikling.Core.Rooms;
using Stikling.Core.Today;

namespace Stikling.Core.Tests;

public class SitterSheetTests
{
    private static readonly DateOnly Today = new(2026, 10, 12);
    private static readonly TimeProvider Time = new FixedTime(new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero));

    private readonly List<Plant> plants = [];
    private readonly List<Propagation> propagations = [];
    private readonly List<CareLog> logs = [];
    private readonly List<Place> placeList = [];
    private readonly List<AwayStretch> stretches =
    [
        new AwayStretch { From = new DateOnly(2026, 10, 20), Until = new DateOnly(2026, 11, 3), Kind = AwayKind.Away }
    ];

    // Three days apart: 23 October, 26, 29 and 1 November
    private readonly SitterSheet sheet = new() { Visits = VisitPattern.EveryThreeDays };

    private static readonly DateOnly[] Visits =
        [new(2026, 10, 23), new(2026, 10, 26), new(2026, 10, 29), new(2026, 11, 1)];

    private Plant Plant(string name, Place? place = null, Action<Plant>? set = null)
    {
        var plant = new Plant { Nickname = name, PlaceId = place?.Id };
        set?.Invoke(plant);
        plants.Add(plant);
        return plant;
    }

    private Propagation Cutting(string name, Place? place = null, GrowingMedium medium = GrowingMedium.Water)
    {
        var cutting = new Propagation { Nickname = name, Medium = medium, PlaceId = place?.Id, StartedOn = Today.AddDays(-10) };
        propagations.Add(cutting);
        return cutting;
    }

    private Place Room(string name)
    {
        var room = new Place { Name = name };
        placeList.Add(room);
        return room;
    }

    private Place Spot(Place room, string name)
    {
        var spot = new Place { Name = name, RoomId = room.Id };
        placeList.Add(spot);
        return spot;
    }

    // Four waterings the given days apart, the last one this many days ago, so it learns a pace
    private Plant Paced(string name, int gap, int lastDaysAgo, Place? place = null)
    {
        var plant = Plant(name, place);
        for (var i = 0; i < 4; i++)
            logs.Add(new CareLog { PlantId = plant.Id, Kind = CareKind.Watered, OccurredOn = Today.AddDays(-lastDaysAgo - gap * i) });
        return plant;
    }

    private WateringGuess GuessOf(Plant plant) =>
        WateringRound.GuessFor(plant, plants, logs, [], null, [], new Places(placeList), stretches, Hemisphere.Northern, Time);

    private SitterSheetView Build() =>
        SitterSheetBuilder.Build(sheet, stretches, plants, propagations, logs, [], null, [], new Places(placeList),
            Hemisphere.Northern, Time);

    private static SitterSubject Find(SitterSheetView view, string name) =>
        view.Subjects.Single(s => s.Name == name);

    [Fact]
    public void A_collection_number_goes_on_the_card_and_in_the_text()
    {
        Plant("Hoya", set: p => p.CollectionNumber = "117");
        var cutting = Cutting("Jar");
        cutting.CollectionNumber = "117a";
        Plant("Fern");

        var view = Build();

        Assert.Equal("#117 Hoya", Find(view, "Hoya").Label);
        Assert.Equal("#117a Jar", Find(view, "Jar").Label);
        Assert.Equal("Fern", Find(view, "Fern").Label);
        var text = SitterSheetText.Write(view);
        Assert.Contains("#117 Hoya", text);
        Assert.Contains("#117a Jar", text);
    }

    [Fact]
    public void With_a_meter_a_visit_is_the_one_nearest_the_day_and_a_tie_goes_to_the_later()
    {
        var visits = new[] { new DateOnly(2026, 10, 10), new DateOnly(2026, 10, 14), new DateOnly(2026, 10, 18) };

        // 12 is two days from both 10 and 14
        Assert.Equal([visits[1], visits[2]], SitterSheetBuilder.Map(visits, new DateOnly(2026, 10, 12), 4, hasMeter: true));
        Assert.Equal([visits[0]], SitterSheetBuilder.Map(visits, new DateOnly(2026, 10, 9), 20, hasMeter: true));
        Assert.Equal([visits[2]], SitterSheetBuilder.Map(visits, new DateOnly(2026, 10, 17), 20, hasMeter: true));
    }

    [Fact]
    public void Without_a_meter_a_visit_is_the_first_on_or_after_the_day_never_before()
    {
        var visits = new[] { new DateOnly(2026, 10, 10), new DateOnly(2026, 10, 14), new DateOnly(2026, 10, 18) };

        Assert.Equal([visits[1]], SitterSheetBuilder.Map(visits, new DateOnly(2026, 10, 11), 20, hasMeter: false));
        Assert.Equal([visits[1]], SitterSheetBuilder.Map(visits, new DateOnly(2026, 10, 14), 20, hasMeter: false));
        Assert.Equal([visits[0], visits[2]], SitterSheetBuilder.Map(visits, new DateOnly(2026, 10, 10), 7, hasMeter: false));
    }

    [Fact]
    public void A_pace_shorter_than_the_visits_are_apart_goes_on_every_visit()
    {
        var visits = new[] { new DateOnly(2026, 10, 10), new DateOnly(2026, 10, 17), new DateOnly(2026, 10, 24) };

        Assert.Equal(visits, SitterSheetBuilder.Map(visits, visits[0], 1, hasMeter: true));
        Assert.Equal(visits, SitterSheetBuilder.Map(visits, visits[0], 1, hasMeter: false));
    }

    [Fact]
    public void A_learned_pace_puts_a_plant_on_the_visits_from_its_day()
    {
        var plant = Paced("Hoya", gap: 14, lastDaysAgo: 0);
        var guess = GuessOf(plant);
        Assert.Equal(14, guess.UsualDays);
        Assert.Equal(new DateOnly(2026, 10, 26), guess.CheckOn);

        var subject = Find(Build(), "Hoya");

        Assert.Equal([new DateOnly(2026, 10, 26)], subject.Visits);
        Assert.False(subject.DueBeforeYouGo);
        Assert.Null(subject.CheckNote);
    }

    [Fact]
    public void A_plant_due_before_you_go_is_counted_from_the_first_day_away()
    {
        // Due on the 19th, the day before leaving, so the sheet counts from the 20th: due on the 27th
        var plant = Paced("Fern", gap: 7, lastDaysAgo: 0);
        Assert.Equal(new DateOnly(2026, 10, 19), GuessOf(plant).CheckOn);

        var view = Build();
        var subject = Find(view, "Fern");

        Assert.True(subject.DueBeforeYouGo);
        Assert.Equal([new DateOnly(2026, 10, 29)], subject.Visits);
        Assert.Equal(["Fern"], view.DueBeforeYouGo);

        sheet.HasMeter = true;
        // Nearest to the 27th is the 26th, and the next day, 2 November, is after the last visit
        Assert.Equal([new DateOnly(2026, 10, 26)], Find(Build(), "Fern").Visits);
    }

    [Fact]
    public void A_plant_due_after_the_last_visit_is_on_none_and_says_it_should_be_fine()
    {
        stretches[0].Until = new DateOnly(2026, 10, 25);
        sheet.Visits = VisitPattern.EveryDay;
        Paced("Cactus", gap: 7, lastDaysAgo: 0);

        var subject = Find(Build(), "Cactus");

        Assert.Empty(subject.Visits);
        Assert.Equal("Should be fine until you're back", subject.CheckNote);
    }

    [Fact]
    public void Plants_still_learning_and_propagations_go_on_every_visit()
    {
        Plant("New");
        Cutting("Pothos");
        Cutting("Rooted in soil", medium: GrowingMedium.Soil);

        var view = Build();

        Assert.Equal(Visits, view.Visits);
        Assert.All(view.Subjects, s => Assert.Equal(Visits, s.Visits));
        Assert.All(view.ByVisit, v => Assert.Equal(3, v.Count));
        Assert.Equal("Not learned yet, so check it every visit.", Find(view, "New").Pace);
        Assert.Equal("Check the water every visit.", Find(view, "Pothos").Pace);
        Assert.Equal("Check it every visit.", Find(view, "Rooted in soil").Pace);
        Assert.False(view.AnyPace);
    }

    [Fact]
    public void A_plant_with_the_reminder_off_and_no_pace_goes_on_every_visit()
    {
        Plant("Quiet", set: p => p.WateringReminder = false);

        Assert.Equal(Visits, Find(Build(), "Quiet").Visits);
    }

    [Fact]
    public void The_pace_line_says_how_often_it_usually_needs_water_and_to_check_first()
    {
        Paced("Hoya", gap: 14, lastDaysAgo: 0);

        Assert.Equal(
            "Usually dry enough to need water about every 14 days. Check it first, and leave it if it's still damp.",
            Find(Build(), "Hoya").Pace);
    }

    [Fact]
    public void With_no_stretch_there_are_no_dates_and_every_plant_is_checked_each_visit()
    {
        stretches.Clear();
        Paced("Hoya", gap: 14, lastDaysAgo: 0);
        Cutting("Pothos");

        var view = Build();

        Assert.Empty(view.Visits);
        Assert.Empty(view.ByVisit);
        Assert.False(view.HasDates);
        Assert.Null(view.From);
        Assert.All(view.Subjects, s =>
        {
            Assert.Empty(s.Visits);
            Assert.Equal("Check it each time you visit", s.CheckNote);
        });
        Assert.Equal("1 plant · 1 cutting", view.Header);
    }

    [Fact]
    public void A_stretch_that_is_on_now_is_used_and_a_quiet_one_is_not()
    {
        stretches.Clear();
        stretches.Add(new AwayStretch { From = Today.AddDays(-2), Until = Today.AddDays(5), Kind = AwayKind.Quiet });
        stretches.Add(new AwayStretch { From = Today.AddDays(30), Until = Today.AddDays(40), Kind = AwayKind.Away });
        Plant("Hoya");

        var view = Build();

        Assert.Equal(Today.AddDays(30), view.From);
        Assert.Equal(Today.AddDays(40), view.Until);

        stretches.Add(new AwayStretch { From = Today.AddDays(-1), Until = Today.AddDays(3), Kind = AwayKind.Away });
        // The quiet days that touch the trip are left off the sheet
        Assert.Equal(Today.AddDays(-1), Build().From);
    }

    [Fact]
    public void Plants_and_propagations_left_off_are_not_on_the_sheet_but_can_be_chosen_again()
    {
        var kept = Plant("Kept");
        var left = Plant("Left off");
        var cutting = Cutting("Cutting left off");
        Plant("Gone", set: p => p.Status = PlantStatus.Died);
        sheet.LeftOff = [left.Id, cutting.Id];

        var view = Build();

        Assert.Equal(["Kept"], view.Subjects.Select(s => s.Name));
        Assert.Equal("1 plant", view.Header.Split(" · ")[^2]);
        Assert.Equal(
            [kept.Id, left.Id, cutting.Id], view.Candidates.Select(c => c.Id));
        Assert.Equal([true, false, false], view.Candidates.Select(c => c.OnSheet));
    }

    [Fact]
    public void The_walk_goes_room_by_room_with_plants_first_and_what_has_no_place_last()
    {
        var living = Room("Living room");
        var kitchen = Room("Kitchen");
        var shelf = Spot(living, "Shelf");
        Plant("Zed", living);
        Plant("Ann", shelf);
        Plant("Bea", living);
        Plant("Loose");
        Plant("Sink", kitchen);
        Cutting("Glass", kitchen);
        Cutting("Jar");

        var view = Build();

        Assert.Equal(["Kitchen", "Living room", "Elsewhere"], view.Groups.Select(g => g.Heading));
        Assert.Equal(["Sink", "Glass"], view.Groups[0].Subjects.Select(s => s.Name));
        Assert.Equal(["Bea", "Zed", "Ann"], view.Groups[1].Subjects.Select(s => s.Name));
        Assert.Equal(["Loose", "Jar"], view.Groups[2].Subjects.Select(s => s.Name));
        Assert.Equal("Living room · Shelf", Find(view, "Ann").Place);
        Assert.Equal("Living room", Find(view, "Bea").Place);
        Assert.Null(Find(view, "Loose").Place);
    }

    [Fact]
    public void How_is_only_on_a_card_when_it_differs_from_the_usual_way()
    {
        sheet.DefaultMethod = WaterMethod.Bottom;
        Plant("Usual");
        Plant("Same", set: p => p.WaterBy = WaterMethod.Bottom);
        Plant("Top", set: p => p.WaterBy = WaterMethod.Top);
        Plant("Soak", set: p => p.WaterBy = WaterMethod.Soak);

        var view = Build();

        Assert.Null(Find(view, "Usual").How);
        Assert.Null(Find(view, "Same").How);
        Assert.Equal("From the top", Find(view, "Top").How);
        Assert.Equal("Soak", Find(view, "Soak").How);
    }

    [Fact]
    public void With_the_own_words_as_the_usual_way_a_plant_that_says_how_shows_its_own()
    {
        sheet.UseOwnWords = true;
        sheet.OwnWords = "Use the green can.";
        Plant("Usual");
        Plant("Bottom", set: p => p.WaterBy = WaterMethod.Bottom);

        var view = Build();

        Assert.Null(Find(view, "Usual").How);
        Assert.Equal("From the bottom", Find(view, "Bottom").How);
        Assert.Equal(["Usual way", "From the bottom"], view.Box.Steps.Select(s => s.Name));
        Assert.Equal("Use the green can.", view.Box.Steps[0].Text);
    }

    [Fact]
    public void Own_words_that_are_blank_fall_back_to_the_chosen_method()
    {
        sheet.UseOwnWords = true;
        sheet.OwnWords = "  ";
        sheet.DefaultMethod = WaterMethod.Soak;
        Plant("Usual");

        var steps = Build().Box.Steps;

        Assert.Equal(SitterSheetText.MethodSteps(WaterMethod.Soak), Assert.Single(steps).Text);
    }

    [Fact]
    public void A_reservoir_and_a_cutting_in_water_are_topped_up_not_watered()
    {
        Plant("Leca", set: p => p.Medium = GrowingMedium.Leca);
        Plant("Water", set: p => p.Medium = GrowingMedium.Water);
        Cutting("Pothos");
        Cutting("Soil", medium: GrowingMedium.Soil);

        var view = Build();

        Assert.Equal("Top up the reservoir", Find(view, "Leca").How);
        Assert.Equal("Top up the water", Find(view, "Water").How);
        Assert.Equal("Top up the water", Find(view, "Pothos").How);
        Assert.Null(Find(view, "Soil").How);
        Assert.Equal("Plants in water or on a reservoir only need topping up.", view.Box.TopUp);
    }

    [Fact]
    public void The_meter_line_names_the_level_for_a_plant_watered_from_the_top()
    {
        sheet.HasMeter = true;
        Plant("Default");
        Plant("Moist", set: p => p.WaterAt = 5);
        Plant("Leca", set: p => p.Medium = GrowingMedium.Leca);
        Cutting("Pothos");

        var view = Build();

        Assert.Equal("Water only at 3 or below on the meter.", Find(view, "Default").Meter);
        Assert.Equal("Water only at 5 or below on the meter.", Find(view, "Moist").Meter);
        Assert.Null(Find(view, "Leca").Meter);
        Assert.Null(Find(view, "Pothos").Meter);

        sheet.HasMeter = false;
        Assert.Null(Find(Build(), "Default").Meter);
    }

    [Fact]
    public void The_box_prints_the_steps_for_the_usual_way_and_for_each_other_way_a_card_uses()
    {
        sheet.DefaultMethod = WaterMethod.Top;
        Plant("Usual");

        var alone = Build().Box.Steps;
        Assert.Null(Assert.Single(alone).Name);
        Assert.Equal(SitterSheetText.MethodSteps(WaterMethod.Top), alone[0].Text);

        Plant("Soak", set: p => p.WaterBy = WaterMethod.Soak);
        Plant("Same", set: p => p.WaterBy = WaterMethod.Top);

        var several = Build().Box.Steps;
        Assert.Equal(["From the top", "Soak"], several.Select(s => s.Name));
        Assert.Null(Build().Box.TopUp);
    }

    [Fact]
    public void A_way_that_only_a_plant_left_off_uses_is_not_printed()
    {
        sheet.DefaultMethod = WaterMethod.Top;
        Plant("Usual");
        var soak = Plant("Soak", set: p => p.WaterBy = WaterMethod.Soak);
        sheet.LeftOff = [soak.Id];

        Assert.Single(Build().Box.Steps);
    }

    [Fact]
    public void The_check_is_always_in_the_box_and_the_steps_come_after_it()
    {
        Plant("Usual");

        var finger = Build().Box;
        Assert.Contains("Push a finger 2 to 3 cm into the soil. Water only if it's dry all the way down.", finger.Check[0]);
        Assert.Contains("safe to wait until your next visit", finger.Check[0]);

        sheet.HasMeter = true;
        sheet.MeterWhere = "in the hall";
        var meter = Build().Box;
        Assert.StartsWith("There's a moisture meter, in the hall. Push it into the soil about halfway down", meter.Check[0]);
        Assert.Contains("at or below the number on the plant's card", meter.Check[0]);
        Assert.StartsWith("No reading?", meter.Check[1]);

        sheet.HideHowWatered = true;
        var hidden = Build().Box;
        Assert.Empty(hidden.Steps);
        Assert.Null(hidden.TopUp);
        Assert.Equal(2, hidden.Check.Count);
    }

    [Fact]
    public void The_hide_options_take_the_lines_off_the_cards()
    {
        var room = Room("Hall");
        var plant = Plant("Hoya", room, p =>
        {
            p.WaterBy = WaterMethod.Top;
            p.SitterNote = "Likes a quiet corner";
            p.CoverPhotoId = Guid.NewGuid();
        });
        sheet.HasMeter = true;

        var shown = Find(Build(), "Hoya");
        Assert.Equal(plant.CoverPhotoId, shown.PhotoId);
        Assert.Equal("Hall", shown.Place);
        Assert.Equal("From the top", shown.How);
        Assert.NotNull(shown.Pace);
        Assert.Equal("Likes a quiet corner", shown.Note);

        sheet.HidePhotos = sheet.HidePlaces = sheet.HideHowWatered = sheet.HidePace = sheet.HideNotes = true;
        var view = Build();
        var hidden = Find(view, "Hoya");
        Assert.False(view.ShowPhotos);
        Assert.Null(hidden.PhotoId);
        Assert.Null(hidden.Place);
        Assert.Null(hidden.How);
        Assert.Null(hidden.Pace);
        Assert.Null(hidden.Note);
        Assert.NotNull(hidden.Meter);
    }

    [Fact]
    public void The_species_is_shown_under_a_nickname()
    {
        Plant("Marble Queen", set: p => { p.Genus = "Epipremnum"; p.Species = "aureum"; });
        Plant("", set: p => { p.Genus = "Hoya"; p.Species = "carnosa"; });

        var view = Build();

        Assert.Equal("Epipremnum aureum", Find(view, "Marble Queen").Species);
        Assert.Null(Find(view, "Hoya carnosa").Species);
    }

    [Fact]
    public void The_header_has_the_dates_the_counts_and_the_visits()
    {
        Plant("A");
        Plant("B");
        Cutting("Jar");

        Assert.Equal("20 October to 3 November · 2 plants · 1 cutting · a visit every 3 days", Build().Header);

        propagations[0].Type = PropagationType.Seed;
        sheet.Visits = VisitPattern.TwiceAWeek;
        Assert.Equal("20 October to 3 November · 2 plants · 1 propagation · a visit twice a week", Build().Header);

        sheet.Visits = VisitPattern.None;
        var none = Build();
        Assert.Equal("20 October to 3 November · 2 plants · 1 propagation", none.Header);
        Assert.Empty(none.Visits);
    }

    [Fact]
    public void The_general_note_is_kept_and_a_blank_one_is_not()
    {
        sheet.GeneralNote = "  Thank you!  ";
        Assert.Equal("Thank you!", Build().GeneralNote);

        sheet.GeneralNote = " ";
        Assert.Null(Build().GeneralNote);
    }

    [Fact]
    public void The_text_has_the_box_the_visits_and_every_subject()
    {
        var living = Room("Living room");
        Paced("Hoya", gap: 14, lastDaysAgo: 0, place: living);
        Plant("Fern", living, p => p.SitterNote = "Mind the leaves");
        Cutting("Pothos");
        sheet.Layout = SheetLayout.Visits;
        sheet.GeneralNote = "Thank you!";

        var text = SitterSheetText.Write(Build());

        Assert.StartsWith("Looking after the plants", text);
        Assert.Contains("20 October to 3 November", text);
        Assert.Contains("Thank you!", text);
        Assert.Contains("Before you water", text);
        Assert.Contains("Push a finger 2 to 3 cm", text);
        Assert.Contains("If it needs water", text);
        Assert.Contains("Friday 23 October: check 2", text);
        Assert.Contains("[ ] Fern", text);
        Assert.Contains("Hoya", text);
        Assert.Contains("Fern", text);
        Assert.Contains("Pothos", text);
        Assert.Contains("Note: Mind the leaves", text);
        Assert.True(text.IndexOf("Before you water", StringComparison.Ordinal) < text.IndexOf("If it needs water", StringComparison.Ordinal));
        Assert.True(text.IndexOf("If it needs water", StringComparison.Ordinal) < text.IndexOf("Friday 23 October", StringComparison.Ordinal));
    }

    [Fact]
    public void The_text_for_plant_labels_lists_each_subjects_dates()
    {
        Paced("Hoya", gap: 14, lastDaysAgo: 0);
        Plant("Fern");
        sheet.Layout = SheetLayout.Labels;

        var text = SitterSheetText.Write(Build());

        Assert.Contains("Check on: Monday 26 October", text);
        Assert.Contains("Check on: Friday 23 October, Monday 26 October, Thursday 29 October, Sunday 1 November", text);
        Assert.DoesNotContain(": check ", text);
    }

    [Fact]
    public void The_text_without_dates_says_to_check_each_visit()
    {
        stretches.Clear();
        Plant("Fern");

        var text = SitterSheetText.Write(Build());

        Assert.Contains("Before you water", text);
        Assert.Contains("Fern", text);
        Assert.Contains("Check it each time you visit", text);
    }
}
