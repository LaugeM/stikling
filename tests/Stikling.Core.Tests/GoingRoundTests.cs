using Stikling.Core.Care;
using Stikling.Core.Models;
using Stikling.Core.Rooms;
using Stikling.Core.Today;

namespace Stikling.Core.Tests;

public class GoingRoundTests
{
    private static readonly DateOnly Today = new(2026, 4, 20);
    private static readonly TimeProvider Time = new FixedTime(new DateTimeOffset(2026, 4, 20, 12, 0, 0, TimeSpan.Zero));
    private static readonly DateTimeOffset Created = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly Place living = new() { Name = "Living room", CreatedAt = Created };
    private readonly Place kitchen = new() { Name = "Kitchen", CreatedAt = Created.AddDays(1) };
    private readonly Place windowsill;
    private readonly Place shelf;
    private readonly List<Place> placeList = [];
    private readonly List<Plant> plants = [];
    private readonly List<Propagation> propagations = [];
    private readonly List<CareLog> logs = [];
    private readonly List<TimelineEntry> entries = [];

    public GoingRoundTests()
    {
        windowsill = new Place { Name = "Windowsill", RoomId = living.Id, CreatedAt = Created.AddDays(2) };
        shelf = new Place { Name = "Shelf", RoomId = living.Id, CreatedAt = Created.AddDays(3) };
        placeList.AddRange([living, kitchen, windowsill, shelf]);
    }

    private Places Places => new(placeList);

    private Plant Plant(string name, Place? place = null)
    {
        var plant = new Plant { Nickname = name, PlaceId = place?.Id };
        plants.Add(plant);
        return plant;
    }

    private Propagation Propagation(string name, Place? place = null)
    {
        var propagation = new Propagation { Nickname = name, PlaceId = place?.Id };
        propagations.Add(propagation);
        return propagation;
    }

    // Four weekly waterings ending on the given day, so it is due a week after
    private Plant Weekly(string name, DateOnly last, Place? place = null)
    {
        var plant = Plant(name, place);
        for (var i = 0; i < 4; i++)
            logs.Add(new CareLog { PlantId = plant.Id, Kind = CareKind.Watered, OccurredOn = last.AddDays(-7 * i) });
        return plant;
    }

    private IReadOnlyList<RoundCard> ForPlace(Place place, bool wateringOn = false) =>
        GoingRound.ForPlace(place.Id, plants, propagations, logs, [], null, [], Places, Hemisphere.Northern, Time, wateringOn);

    private IReadOnlyList<RoundCard> ForDue(PutOffs? putOffs = null) =>
        GoingRound.ForDue(plants, logs, [], null, [], Places, Hemisphere.Northern, Time, putOffs ?? PutOffs.Empty());

    [Fact]
    public void A_room_includes_its_spots_with_the_room_first_then_spots_by_name_then_names()
    {
        Plant("Zed", windowsill);
        Plant("Yam", living);
        Plant("Cee", shelf);
        Plant("Bee", windowsill);
        Plant("Elsewhere", kitchen);
        Plant("Nowhere");

        var cards = ForPlace(living);

        Assert.Equal(["Yam", "Cee", "Bee", "Zed"], cards.Select(c => c.Name));
        Assert.Equal([null, "Shelf", "Windowsill", "Windowsill"], cards.Select(c => c.Heading));
    }

    [Fact]
    public void Plants_come_before_propagations_and_each_group_has_the_same_order()
    {
        Propagation("Pa", living);
        Plant("Plant in spot", shelf);
        Propagation("Pb in spot", windowsill);
        Plant("Plant in room", living);
        Propagation("Pc", living);

        var cards = ForPlace(living);

        Assert.Equal(["Plant in room", "Plant in spot", "Pa", "Pc", "Pb in spot"], cards.Select(c => c.Name));
        Assert.All(cards.Take(2), c => Assert.NotNull(c.Form));
        Assert.All(cards.Skip(2), c => Assert.Null(c.Form));
        Assert.IsType<Propagation>(cards[^1].Subject);
    }

    [Fact]
    public void A_round_of_a_spot_has_only_that_spot_and_no_headings()
    {
        Plant("Here", windowsill);
        Plant("There", shelf);
        Plant("Room", living);

        var cards = ForPlace(windowsill);

        Assert.Equal(["Here"], cards.Select(c => c.Name));
        Assert.Null(cards[0].Heading);
    }

    [Fact]
    public void A_place_merged_into_another_counts_as_the_other()
    {
        var old = new Place { Name = "Old shelf", RoomId = living.Id, CreatedAt = Created, DeletedAt = Created.AddDays(10), MergedIntoId = shelf.Id };
        placeList.Add(old);
        Plant("From old", old);

        var cards = ForPlace(living);
        Assert.Equal("From old", Assert.Single(cards).Name);
        Assert.Equal("Shelf", cards[0].Heading);

        Assert.Single(ForPlace(shelf));
    }

    [Fact]
    public void Plants_and_propagations_that_are_gone_are_left_out()
    {
        Plant("Died", living).Status = PlantStatus.Died;
        Plant("Sold", living).Status = PlantStatus.Sold;
        Plant("Deleted", living).DeletedAt = Created;
        Propagation("Done", living).Stage = PropagationStage.Done;
        Propagation("Failed", living).Stage = PropagationStage.Failed;
        Propagation("Deleted", living).DeletedAt = Created;
        Plant("Here", living);

        Assert.Equal(["Here"], ForPlace(living).Select(c => c.Name));
    }

    [Fact]
    public void A_dormant_plant_or_propagation_is_included_and_flagged()
    {
        Plant("Asleep", living).DormantSince = Today.AddDays(-10);
        Plant("Awake", living);
        Propagation("Resting", living).DormantSince = Today.AddDays(-3);

        var cards = ForPlace(living);

        Assert.Equal([false, true, true], cards.Select(c => c.Dormant).Order());
        Assert.True(cards.Single(c => c.Name == "Asleep").Dormant);
        Assert.False(cards.Single(c => c.Name == "Awake").Dormant);
        Assert.True(cards.Single(c => c.Name == "Resting").Dormant);
    }

    [Fact]
    public void Only_a_plant_that_is_due_is_marked_due_and_only_with_the_setting_on()
    {
        Weekly("Late", Today.AddDays(-10), living);
        Weekly("Coming", Today.AddDays(-5), living);
        Weekly("Off", Today.AddDays(-10), living).WateringReminder = false;
        Plant("Learning", living);
        Propagation("Cutting", living);

        var cards = ForPlace(living, wateringOn: true).ToDictionary(c => c.Name);

        Assert.True(cards["Late"].DueNow);
        Assert.NotNull(cards["Late"].Guess);
        Assert.False(cards["Coming"].DueNow);
        Assert.NotNull(cards["Coming"].Guess);
        Assert.False(cards["Off"].DueNow);
        Assert.Null(cards["Off"].Guess);
        Assert.Null(cards["Learning"].Guess);
        Assert.Null(cards["Cutting"].Guess);

        Assert.All(ForPlace(living, wateringOn: false), c =>
        {
            Assert.False(c.DueNow);
            Assert.Null(c.Guess);
        });
    }

    [Fact]
    public void The_due_round_has_only_plants_due_now_by_room_then_spots_then_names_and_none_without_a_place_first()
    {
        Weekly("Nowhere", Today.AddDays(-10));
        Weekly("Kitchen plant", Today.AddDays(-10), kitchen);
        Weekly("Sill B", Today.AddDays(-10), windowsill);
        Weekly("Sill A", Today.AddDays(-12), windowsill);
        Weekly("Living plant", Today.AddDays(-10), living);
        Weekly("Shelf", Today.AddDays(-10), shelf);
        Weekly("Not yet", Today.AddDays(-2), living);
        Propagation("Cutting", living);

        var cards = ForDue();

        Assert.Equal(
            ["Kitchen plant", "Living plant", "Shelf", "Sill A", "Sill B", "Nowhere"],
            cards.Select(c => c.Name));
        Assert.Equal(
            ["Kitchen", "Living room", "Living room / Shelf", "Living room / Windowsill", "Living room / Windowsill", null],
            cards.Select(c => c.Heading));
        Assert.All(cards, c =>
        {
            Assert.True(c.DueNow);
            Assert.NotNull(c.Guess);
            Assert.NotNull(c.Form);
            Assert.IsType<Plant>(c.Subject);
        });
    }

    [Fact]
    public void A_plant_put_off_is_not_on_the_due_round()
    {
        var plant = Weekly("Late", Today.AddDays(-10), living);
        var putOffs = PutOffs.Empty();
        putOffs.PutOff(PutOffs.Water(plant.Id, Today.AddDays(-10)), Today.AddDays(2));

        Assert.Empty(ForDue(putOffs));
    }

    [Fact]
    public void A_plant_answered_not_yet_on_Today_is_not_due_on_a_room_round()
    {
        var plant = Weekly("Late", Today.AddDays(-10), living);
        var putOffs = PutOffs.Empty();
        putOffs.PutOff(PutOffs.Water(plant.Id, Today.AddDays(-10)), Today.AddDays(2));

        var card = GoingRound.ForPlace(living.Id, plants, propagations, logs, [], null, [], Places, Hemisphere.Northern, Time, true, putOffs).Single();

        Assert.False(card.DueNow);
        Assert.NotNull(card.Guess);
    }

    private static RoundCard CardOf(Entity subject, WateringForm? form = WateringForm.TopWatered) =>
        new(subject, "x", null, form, null, false, false);

    [Fact]
    public void The_day_shows_what_was_logged_today_and_not_before()
    {
        var plant = new Plant { Nickname = "P" };
        var card = CardOf(plant);
        logs.Add(new CareLog { PlantId = plant.Id, Kind = CareKind.MoistureReading, Moisture = 7, OccurredOn = Today.AddDays(-1) });
        logs.Add(new CareLog { PlantId = plant.Id, Kind = CareKind.Watered, OccurredOn = Today.AddDays(-1) });
        entries.Add(new TimelineEntry { SubjectId = plant.Id, Kind = TimelineKind.Photo, OccurredAt = Time.GetUtcNow().AddDays(-1) });

        var day = GoingRound.DayOf(card, logs, entries, Time);

        Assert.False(day.Any);
        Assert.Null(day.Reading);
        Assert.Null(day.WateredId);
        Assert.Null(day.LookedOverId);
        Assert.False(day.Photo);
    }

    [Fact]
    public void The_day_finds_a_reading_a_watering_a_look_over_and_a_photo()
    {
        var plant = new Plant { Nickname = "P" };
        var reading = new CareLog { PlantId = plant.Id, Kind = CareKind.MoistureReading, Moisture = 3, OccurredOn = Today };
        var watered = new CareLog { PlantId = plant.Id, Kind = CareKind.Watered, OccurredOn = Today };
        var looked = new TimelineEntry
        {
            SubjectId = plant.Id, Kind = TimelineKind.Change, Event = TimelineEvent.LookedOver, OccurredAt = Time.GetUtcNow()
        };
        logs.AddRange([reading, watered]);
        entries.Add(looked);

        var day = GoingRound.DayOf(CardOf(plant), logs, entries, Time);

        Assert.True(day.Any);
        Assert.Equal(new RoundReading(reading.Id, 3), day.Reading);
        Assert.Equal(watered.Id, day.WateredId);
        Assert.Equal(looked.Id, day.LookedOverId);
        Assert.False(day.Photo);

        entries.Add(new TimelineEntry { SubjectId = plant.Id, Kind = TimelineKind.Note, Text = "Hi", PhotoIds = [Guid.NewGuid()], OccurredAt = Time.GetUtcNow() });
        Assert.True(GoingRound.DayOf(CardOf(plant), logs, entries, Time).Photo);
    }

    [Fact]
    public void A_topped_up_plant_is_watered_by_its_own_kind_and_deleted_logs_are_ignored()
    {
        var plant = new Plant { Nickname = "P" };
        logs.Add(new CareLog { PlantId = plant.Id, Kind = CareKind.Watered, OccurredOn = Today });
        logs.Add(new CareLog { PlantId = plant.Id, Kind = CareKind.MoistureReading, Moisture = 5, OccurredOn = Today, DeletedAt = Created });
        var topped = new CareLog { PlantId = plant.Id, Kind = CareKind.ToppedUp, OccurredOn = Today };
        logs.Add(topped);
        entries.Add(new TimelineEntry { SubjectId = plant.Id, Kind = TimelineKind.Note, Text = "Just text", OccurredAt = Time.GetUtcNow() });

        var day = GoingRound.DayOf(CardOf(plant, WateringForm.Reservoir), logs, entries, Time);

        Assert.Equal(topped.Id, day.WateredId);
        Assert.Null(day.Reading);
        Assert.False(day.Photo);
    }

    [Fact]
    public void The_newest_reading_of_the_day_is_the_one_found()
    {
        var plant = new Plant { Nickname = "P" };
        logs.Add(new CareLog { PlantId = plant.Id, Kind = CareKind.MoistureReading, Moisture = 2, OccurredOn = Today, CreatedAt = Created });
        var newer = new CareLog { PlantId = plant.Id, Kind = CareKind.MoistureReading, Moisture = 6, OccurredOn = Today, CreatedAt = Created.AddHours(1) };
        logs.Add(newer);

        Assert.Equal(new RoundReading(newer.Id, 6), GoingRound.DayOf(CardOf(plant), logs, entries, Time).Reading);
    }

    [Fact]
    public void A_propagation_has_a_look_over_and_photos_but_no_watering()
    {
        var propagation = new Propagation { Nickname = "Cutting" };
        entries.Add(new TimelineEntry { SubjectId = propagation.Id, Kind = TimelineKind.Photo, OccurredAt = Time.GetUtcNow() });

        var day = GoingRound.DayOf(CardOf(propagation, form: null), logs, entries, Time);

        Assert.True(day.Photo);
        Assert.Null(day.WateredId);
    }

    [Fact]
    public void A_round_opened_again_starts_at_the_first_card_with_nothing_today()
    {
        var a = new Plant { Nickname = "A" };
        var b = new Plant { Nickname = "B" };
        var c = new Plant { Nickname = "C" };
        RoundCard[] cards = [CardOf(a), CardOf(b), CardOf(c)];
        entries.Add(new TimelineEntry { SubjectId = a.Id, Kind = TimelineKind.Change, Event = TimelineEvent.LookedOver, OccurredAt = Time.GetUtcNow() });
        logs.Add(new CareLog { PlantId = c.Id, Kind = CareKind.Watered, OccurredOn = Today });

        var days = GoingRound.Days(cards, logs, entries, Time);

        Assert.Equal(1, GoingRound.StartAt(cards, days));
    }

    [Fact]
    public void A_round_with_something_on_every_card_starts_past_the_end()
    {
        var a = new Plant { Nickname = "A" };
        var b = new Plant { Nickname = "B" };
        RoundCard[] cards = [CardOf(a), CardOf(b)];
        logs.Add(new CareLog { PlantId = a.Id, Kind = CareKind.MoistureReading, Moisture = 4, OccurredOn = Today });
        logs.Add(new CareLog { PlantId = b.Id, Kind = CareKind.Watered, OccurredOn = Today });

        Assert.Equal(2, GoingRound.StartAt(cards, GoingRound.Days(cards, logs, entries, Time)));
        Assert.Equal(0, GoingRound.StartAt([], []));
    }

    [Fact]
    public void The_summary_counts_what_is_logged_and_what_was_skipped()
    {
        var wateredOnly = new Plant { Nickname = "1" };
        var wateredAndLooked = new Plant { Nickname = "2" };
        var looked = new Plant { Nickname = "3" };
        var readingAndPhoto = new Plant { Nickname = "4" };
        var skipped = new Plant { Nickname = "5" };
        var cutting = new Propagation { Nickname = "6" };
        RoundCard[] cards =
        [
            CardOf(wateredOnly), CardOf(wateredAndLooked), CardOf(looked), CardOf(readingAndPhoto), CardOf(skipped),
            CardOf(cutting, null)
        ];
        logs.Add(new CareLog { PlantId = wateredOnly.Id, Kind = CareKind.Watered, OccurredOn = Today });
        logs.Add(new CareLog { PlantId = wateredAndLooked.Id, Kind = CareKind.Watered, OccurredOn = Today });
        logs.Add(new CareLog { PlantId = wateredAndLooked.Id, Kind = CareKind.MoistureReading, Moisture = 2, OccurredOn = Today });
        logs.Add(new CareLog { PlantId = readingAndPhoto.Id, Kind = CareKind.MoistureReading, Moisture = 8, OccurredOn = Today });
        foreach (var id in new[] { wateredAndLooked.Id, looked.Id, cutting.Id })
            entries.Add(new TimelineEntry { SubjectId = id, Kind = TimelineKind.Change, Event = TimelineEvent.LookedOver, OccurredAt = Time.GetUtcNow() });
        entries.Add(new TimelineEntry { SubjectId = readingAndPhoto.Id, Kind = TimelineKind.Photo, OccurredAt = Time.GetUtcNow() });

        var summary = GoingRound.Summarise(GoingRound.Days(cards, logs, entries, Time));

        Assert.Equal(new RoundSummary(Watered: 2, LookedOver: 2, Readings: 2, Photos: 1, Skipped: 1), summary);
    }
}
