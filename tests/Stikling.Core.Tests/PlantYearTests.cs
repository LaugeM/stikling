using Stikling.Core.Models;
using Stikling.Core.Year;

namespace Stikling.Core.Tests;

public class PlantYearTests
{
    private static readonly DateOnly Today = new(2026, 10, 12);
    private static readonly TimeSpan Plus1 = TimeSpan.FromHours(1);

    private static DateOnly Utc(DateTimeOffset moment) => DateOnly.FromDateTime(moment.UtcDateTime);

    // A time zone one hour ahead of UTC, so late on 31 Dec UTC is already 1 Jan
    private static DateOnly Shifted(DateTimeOffset moment) => DateOnly.FromDateTime(moment.ToOffset(Plus1).DateTime);

    private static DateTimeOffset At(int year, int month, int day, int hour = 12) =>
        new(year, month, day, hour, 0, 0, TimeSpan.Zero);

    private static Plant APlant(string name = "Monstera", int createdYear = 2026) =>
        new() { Nickname = name, CreatedAt = At(createdYear, 3, 1), UpdatedAt = At(createdYear, 3, 1) };

    private static Photo Taken(Plant plant, DateTimeOffset at) =>
        new() { SubjectType = SubjectType.Plant, SubjectId = plant.Id, TakenAt = at };

    private static Photo Taken(Propagation prop, DateTimeOffset at) =>
        new() { SubjectType = SubjectType.Propagation, SubjectId = prop.Id, TakenAt = at };

    private static Propagation Prop(string name, int startedMonth, GrowingMedium medium = GrowingMedium.Water, int year = 2026) =>
        new() { Nickname = name, StartedOn = new DateOnly(year, startedMonth, 1), Medium = medium };

    // A batch of `succeeded` + `failed` units that all went one way or the other
    private static Propagation Decided(string name, GrowingMedium medium, int succeeded, int failed, int? rootedAfterDays = null)
    {
        var prop = Prop(name, 2, medium);
        prop.InitialCount = succeeded + failed;
        prop.PottedUpCount = succeeded;
        prop.FailedCount = failed;
        if (rootedAfterDays is { } days)
            prop.RootedOn = prop.StartedOn.AddDays(days);
        return prop;
    }

    private static PlantYearSummary Year(
        int year,
        IEnumerable<Plant>? plants = null,
        IEnumerable<Propagation>? props = null,
        IEnumerable<Photo>? photos = null,
        IEnumerable<TimelineEntry>? changes = null,
        Func<DateTimeOffset, DateOnly>? localDay = null) =>
        PlantYear.For(year, plants ?? [], props ?? [], photos ?? [], changes ?? [], Today, localDay ?? Utc, "Died");

    [Fact]
    public void A_photo_late_on_31_December_in_a_zone_ahead_of_UTC_lands_in_the_next_year()
    {
        var plant = APlant();
        var photo = Taken(plant, At(2025, 12, 31, 23, 30));

        Assert.Equal(1, Year(2025, [plant], photos: [photo]).PhotosTaken);
        Assert.Equal(0, Year(2025, [plant], photos: [photo], localDay: Shifted).PhotosTaken);
        Assert.Equal(1, Year(2026, [plant], photos: [photo], localDay: Shifted).PhotosTaken);
    }

    private static DateTimeOffset At(int year, int month, int day, int hour, int minute) =>
        new(year, month, day, hour, minute, 0, TimeSpan.Zero);

    [Fact]
    public void The_year_so_far_is_marked()
    {
        Assert.True(Year(2026).SoFar);
        Assert.False(Year(2025).SoFar);
    }

    [Fact]
    public void A_plant_counts_in_the_year_it_was_acquired_at_any_precision()
    {
        var yearOnly = APlant("Year", 2020);
        yearOnly.AcquiredOn = LooseDate.Of(2026);
        var monthOnly = APlant("Month", 2020);
        monthOnly.AcquiredOn = LooseDate.Of(2026, 12);
        var earlier = APlant("Earlier", 2026);
        earlier.AcquiredOn = LooseDate.Of(2024, 5);

        Assert.Equal(2, Year(2026, [yearOnly, monthOnly, earlier]).Added.Count);
        Assert.Equal(1, Year(2024, [yearOnly, monthOnly, earlier]).Added.Count);
    }

    [Fact]
    public void A_plant_without_a_date_got_counts_in_the_year_it_was_created()
    {
        var plant = APlant("New", 2025);

        Assert.Equal(1, Year(2025, [plant]).Added.Count);
        Assert.Equal(0, Year(2026, [plant]).Added.Count);
    }

    [Fact]
    public void Added_is_split_by_origin_biggest_first_with_ties_in_enum_order()
    {
        var plants = new[]
        {
            new Plant { Nickname = "a", Origin = PlantOrigin.Gift, CreatedAt = At(2026, 1, 1) },
            new Plant { Nickname = "b", Origin = PlantOrigin.Gift, CreatedAt = At(2026, 1, 1) },
            new Plant { Nickname = "c", Origin = PlantOrigin.Swap, CreatedAt = At(2026, 1, 1) },
            new Plant { Nickname = "d", Origin = PlantOrigin.Purchased, CreatedAt = At(2026, 1, 1) }
        };

        var added = Year(2026, plants).Added;

        Assert.Equal(4, added.Count);
        Assert.Equal(
            [(PlantOrigin.Gift, 2), (PlantOrigin.Purchased, 1), (PlantOrigin.Swap, 1)],
            added.ByOrigin.Select(o => (o.Origin, o.Count)));
    }

    [Fact]
    public void Deleted_records_and_received_photos_never_count()
    {
        var plant = APlant();
        var gone = APlant("Gone");
        gone.DeletedAt = At(2026, 5, 1);
        var prop = Prop("Gone prop", 3);
        prop.DeletedAt = At(2026, 5, 1);
        var ofGone = Taken(gone, At(2026, 4, 1));
        var deletedPhoto = Taken(plant, At(2026, 4, 1));
        deletedPhoto.DeletedAt = At(2026, 5, 1);
        var received = Taken(plant, At(2026, 4, 1));
        received.ReceivedHistoryId = Guid.NewGuid();
        var ofPot = new Photo { SubjectType = SubjectType.Pot, SubjectId = plant.Id, TakenAt = At(2026, 4, 1) };
        var ofNobody = new Photo { SubjectType = SubjectType.Plant, SubjectId = Guid.NewGuid(), TakenAt = At(2026, 4, 1) };
        var good = Taken(plant, At(2026, 4, 2));

        var year = Year(2026, [plant, gone], [prop], [ofGone, deletedPhoto, received, ofPot, ofNobody, good]);

        Assert.Equal(1, year.Added.Count);
        Assert.Equal(0, year.Propagations.Started);
        Assert.Equal(1, year.PhotosTaken);
        Assert.Equal([good.Id], year.Collage.Select(c => c.Photo.Id));
    }

    [Fact]
    public void A_year_with_nothing_is_empty_and_has_no_cards()
    {
        var year = Year(2026);

        Assert.True(year.IsEmpty);
        Assert.Equal(0, year.CardCount);
    }

    [Fact]
    public void A_died_plant_counts_in_added_and_photos_but_is_off_every_photo_card()
    {
        var plant = APlant("Dead");
        plant.Status = PlantStatus.Died;
        var photos = new[]
        {
            Taken(plant, At(2026, 1, 1)), Taken(plant, At(2026, 3, 1)), Taken(plant, At(2026, 6, 1))
        };

        var year = Year(2026, [plant], photos: photos);

        Assert.Equal(1, year.Added.Count);
        Assert.Equal(3, year.PhotosTaken);
        Assert.Null(year.MostPhotographed);
        Assert.Null(year.Grown);
        Assert.Empty(year.Collage);
    }

    [Fact]
    public void Most_photographed_needs_three_photos_and_picks_the_cover_when_taken_this_year()
    {
        var few = APlant("Few");
        var many = APlant("Many");
        var photos = new List<Photo> { Taken(few, At(2026, 1, 1)), Taken(few, At(2026, 1, 2)) };
        var cover = Taken(many, At(2026, 2, 1));
        photos.AddRange([cover, Taken(many, At(2026, 3, 1)), Taken(many, At(2026, 4, 1))]);
        many.CoverPhotoId = cover.Id;

        var most = Year(2026, [few, many], photos: photos).MostPhotographed;

        Assert.NotNull(most);
        Assert.Equal(many.Id, most.Plant.Id);
        Assert.Equal(3, most.PhotoCount);
        Assert.Equal(cover.Id, most.PhotoId);

        Assert.Null(Year(2026, [few], photos: photos.Take(2)).MostPhotographed);
    }

    [Fact]
    public void Most_photographed_falls_back_to_the_newest_photo_and_breaks_ties_on_the_newer_photo_then_name()
    {
        var a = APlant("A");
        var b = APlant("B");
        var photosA = new[] { Taken(a, At(2026, 1, 1)), Taken(a, At(2026, 1, 2)), Taken(a, At(2026, 1, 3)) };
        var newestB = Taken(b, At(2026, 5, 3));
        var photosB = new[] { Taken(b, At(2026, 5, 1)), Taken(b, At(2026, 5, 2)), newestB };
        b.CoverPhotoId = Guid.NewGuid();

        var most = Year(2026, [a, b], photos: [.. photosA, .. photosB]).MostPhotographed;

        Assert.Equal(b.Id, most!.Plant.Id);
        Assert.Equal(newestB.Id, most.PhotoId);

        // Same newest photo: the name decides
        var c = APlant("C");
        var d = APlant("D");
        var same = new[] { Taken(c, At(2026, 5, 1)), Taken(c, At(2026, 5, 2)), Taken(c, At(2026, 5, 3)),
            Taken(d, At(2026, 5, 1)), Taken(d, At(2026, 5, 2)), Taken(d, At(2026, 5, 3)) };
        Assert.Equal(c.Id, Year(2026, [d, c], photos: same).MostPhotographed!.Plant.Id);
    }

    [Theory]
    [InlineData(89, false)]
    [InlineData(90, true)]
    public void Grown_needs_the_first_and_last_photo_to_be_90_days_apart(int days, bool qualifies)
    {
        var plant = APlant();
        var first = At(2026, 1, 1);
        var photos = new[] { Taken(plant, first), Taken(plant, first.AddDays(days)) };

        var grown = Year(2026, [plant], photos: photos).Grown;

        Assert.Equal(qualifies, grown is not null);
        if (grown is not null)
        {
            Assert.Equal(photos[0].Id, grown.First.Id);
            Assert.Equal(photos[1].Id, grown.Last.Id);
            Assert.Equal(90, grown.DaysApart);
        }
    }

    [Fact]
    public void Grown_picks_the_longest_gap_then_more_photos_then_name()
    {
        var shortGap = APlant("Short");
        var longGap = APlant("Long");
        var photos = new[]
        {
            Taken(shortGap, At(2026, 1, 1)), Taken(shortGap, At(2026, 6, 1)),
            Taken(longGap, At(2026, 1, 1)), Taken(longGap, At(2026, 9, 1))
        };
        Assert.Equal(longGap.Id, Year(2026, [shortGap, longGap], photos: photos).Grown!.Plant.Id);

        var b = APlant("B");
        var a = APlant("A");
        var tie = new[]
        {
            Taken(b, At(2026, 1, 1)), Taken(b, At(2026, 5, 1)),
            Taken(a, At(2026, 1, 1)), Taken(a, At(2026, 5, 1))
        };
        Assert.Equal(a.Id, Year(2026, [b, a], photos: tie).Grown!.Plant.Id);

        tie = [.. tie, Taken(b, At(2026, 3, 1))];
        Assert.Equal(b.Id, Year(2026, [b, a], photos: tie).Grown!.Plant.Id);
    }

    [Fact]
    public void The_collage_has_one_item_per_subject_most_photographed_first()
    {
        var one = APlant("One");
        var three = APlant("Three");
        var prop = Prop("Cutting", 3);
        var newestThree = Taken(three, At(2026, 5, 3));
        var photos = new[]
        {
            Taken(one, At(2026, 6, 1)),
            Taken(three, At(2026, 5, 1)), Taken(three, At(2026, 5, 2)), newestThree,
            Taken(prop, At(2026, 4, 1)), Taken(prop, At(2026, 4, 2))
        };

        var collage = Year(2026, [one, three], [prop], photos).Collage;

        Assert.Equal([three.Id, prop.Id, one.Id], collage.Select(c => c.SubjectId));
        Assert.Equal(SubjectType.Propagation, collage[1].SubjectType);
        Assert.Equal(newestThree.Id, collage[0].Photo.Id);
    }

    [Fact]
    public void Cards_are_counted_from_what_is_there()
    {
        var plants = Enumerable.Range(0, 4).Select(i => APlant($"P{i}")).ToList();
        var photos = plants.Select(p => Taken(p, At(2026, 5, 1))).ToList();

        var year = Year(2026, plants, photos: photos);

        // title, added, collage
        Assert.Equal(3, year.CardCount);
        Assert.Equal(2, Year(2026, plants.Take(3), photos: photos.Take(3)).CardCount);
    }

    [Fact]
    public void Propagations_count_started_succeeded_decided_and_rooted()
    {
        var cutting = Decided("A", GrowingMedium.Water, 2, 1, 14);
        var seeds = Decided("B", GrowingMedium.Soil, 1, 0);
        seeds.Type = PropagationType.Seed;
        var rootedThisYear = Prop("C", 11, year: 2025);
        rootedThisYear.RootedOn = new DateOnly(2026, 1, 5);

        var year = Year(2026, props: [cutting, seeds, rootedThisYear]).Propagations;

        Assert.Equal(2, year.Started);
        Assert.Equal(3, year.Succeeded);
        Assert.Equal(4, year.Decided);
        Assert.Equal(2, year.Rooted);
        Assert.False(year.AllCuttings);
        Assert.True(Year(2026, props: [cutting]).Propagations.AllCuttings);
        Assert.Equal(0, Year(2026).Propagations.Decided);
    }

    [Fact]
    public void Quickest_is_the_fewest_days_then_the_earlier_rooting_then_the_name()
    {
        var slow = Decided("Slow", GrowingMedium.Water, 1, 0, 30);
        var b = Decided("B", GrowingMedium.Water, 1, 0, 10);
        var a = Decided("A", GrowingMedium.Water, 1, 0, 10);
        var earlier = Prop("Earlier", 1);
        earlier.RootedOn = earlier.StartedOn.AddDays(10);

        Assert.Equal(10, Year(2026, props: [slow, b, a]).Quickest!.DaysToRoot);
        Assert.Equal("A", Year(2026, props: [slow, b, a]).Quickest!.Propagation.Nickname);
        Assert.Equal("Earlier", Year(2026, props: [slow, b, a, earlier]).Quickest!.Propagation.Nickname);
        Assert.Null(Year(2026, props: [Prop("None", 1)]).Quickest);
    }

    [Fact]
    public void Quickest_shows_the_cover_when_it_exists_else_its_newest_photo_else_nothing()
    {
        var prop = Decided("Fast", GrowingMedium.Water, 1, 0, 7);
        var older = Taken(prop, At(2026, 2, 1));
        var newer = Taken(prop, At(2026, 3, 1));

        Assert.Equal(newer.Id, Year(2026, props: [prop], photos: [older, newer]).Quickest!.PhotoId);

        prop.CoverPhotoId = older.Id;
        Assert.Equal(older.Id, Year(2026, props: [prop], photos: [older, newer]).Quickest!.PhotoId);

        older.DeletedAt = At(2026, 4, 1);
        Assert.Equal(newer.Id, Year(2026, props: [prop], photos: [older, newer]).Quickest!.PhotoId);

        Assert.Null(Year(2026, props: [prop]).Quickest!.PhotoId);
    }

    [Fact]
    public void Best_medium_compares_two_that_qualify()
    {
        var water = Decided("W", GrowingMedium.Water, 3, 1);
        var soil = Decided("S", GrowingMedium.Soil, 3, 0);

        var medium = Year(2026, props: [water, soil]).Medium;

        Assert.NotNull(medium);
        Assert.Equal(GrowingMedium.Soil, medium.Medium);
        Assert.True(medium.Compared);
        Assert.Equal(2, medium.MediumsCompared);
        Assert.Equal(3, medium.Succeeded);
        Assert.Equal(3, medium.Decided);
    }

    [Fact]
    public void Best_medium_with_only_one_that_qualifies_is_not_compared()
    {
        var water = Decided("W", GrowingMedium.Water, 3, 1);
        var soil = Decided("S", GrowingMedium.Soil, 1, 0);

        var medium = Year(2026, props: [water, soil]).Medium;

        Assert.Equal(GrowingMedium.Water, medium!.Medium);
        Assert.False(medium.Compared);
        Assert.Equal(1, medium.MediumsCompared);
    }

    [Fact]
    public void Best_medium_is_null_with_too_few_decided()
    {
        Assert.Null(Year(2026, props: [Decided("W", GrowingMedium.Water, 2, 0)]).Medium);
        Assert.Null(Year(2026).Medium);
    }

    [Fact]
    public void Best_medium_ties_go_to_more_decided_then_faster_rooting_then_enum_order()
    {
        // Same rate: more decided wins
        var fewer = Decided("A", GrowingMedium.Water, 3, 0);
        var more = Decided("B", GrowingMedium.Soil, 4, 0);
        Assert.Equal(GrowingMedium.Soil, Year(2026, props: [fewer, more]).Medium!.Medium);

        // Same rate and decided: the quicker average wins, and no days is last
        var slow = Decided("A", GrowingMedium.Water, 3, 0, 20);
        var fast = Decided("B", GrowingMedium.Soil, 3, 0, 10);
        Assert.Equal(GrowingMedium.Soil, Year(2026, props: [slow, fast]).Medium!.Medium);
        var noDays = Decided("C", GrowingMedium.Water, 3, 0);
        Assert.Equal(GrowingMedium.Soil, Year(2026, props: [noDays, fast]).Medium!.Medium);

        // Everything the same: enum order
        var first = Decided("A", GrowingMedium.Soil, 3, 0);
        var second = Decided("B", GrowingMedium.Water, 3, 0);
        Assert.Equal(GrowingMedium.Soil, Year(2026, props: [second, first]).Medium!.Medium);
    }

    [Fact]
    public void Lost_reads_the_day_from_StatusSince_first()
    {
        var plant = APlant("Fern");
        plant.Status = PlantStatus.Died;
        plant.StatusSince = new DateOnly(2026, 2, 3);
        plant.CauseOfDeath = "Overwatered";
        plant.UpdatedAt = At(2026, 8, 1);
        var line = Change(plant, "Status: Died (was Active)", At(2026, 5, 5));

        var lost = Assert.Single(Year(2026, [plant], changes: [line]).Lost);

        Assert.Equal(new DateOnly(2026, 2, 3), lost.Died);
        Assert.Equal("Overwatered", lost.Cause);
        Assert.Equal(plant.Id, lost.Plant.Id);
    }

    [Fact]
    public void Lost_falls_back_to_the_newest_died_line_in_the_history_then_to_UpdatedAt()
    {
        var plant = APlant("Fern");
        plant.Status = PlantStatus.Died;
        plant.UpdatedAt = At(2026, 9, 1);
        var older = Change(plant, "Status: Died (was Active)", At(2026, 3, 1));
        var newer = Change(plant, "Moved to Bedroom\nStatus: Died (was Active)", At(2026, 4, 1));
        var other = Change(plant, "Status: Given away (was Active)", At(2026, 6, 1));
        var deleted = Change(plant, "Status: Died (was Active)", At(2026, 7, 1));
        deleted.DeletedAt = At(2026, 7, 2);

        Assert.Equal(new DateOnly(2026, 4, 1), Year(2026, [plant], changes: [older, newer, other, deleted]).Lost[0].Died);
        Assert.Equal(new DateOnly(2026, 9, 1), Year(2026, [plant], changes: [other]).Lost[0].Died);
    }

    [Fact]
    public void Lost_only_has_plants_that_died_in_the_year_in_order()
    {
        var late = APlant("Late");
        late.Status = PlantStatus.Died;
        late.StatusSince = new DateOnly(2026, 8, 1);
        var b = APlant("B");
        b.Status = PlantStatus.Died;
        b.StatusSince = new DateOnly(2026, 2, 1);
        var a = APlant("A");
        a.Status = PlantStatus.Died;
        a.StatusSince = new DateOnly(2026, 2, 1);
        var lastYear = APlant("Last year");
        lastYear.Status = PlantStatus.Died;
        lastYear.StatusSince = new DateOnly(2025, 12, 31);
        var gone = APlant("Given");
        gone.Status = PlantStatus.GivenAway;
        gone.StatusSince = new DateOnly(2026, 3, 1);

        var lost = Year(2026, [late, b, a, lastYear, gone]).Lost;

        Assert.Equal(["A", "B", "Late"], lost.Select(l => l.Plant.Nickname));
    }

    private static TimelineEntry Change(Plant plant, string text, DateTimeOffset at) => new()
    {
        SubjectType = SubjectType.Plant,
        SubjectId = plant.Id,
        OccurredAt = at,
        Kind = TimelineKind.Change,
        Text = text
    };

    [Fact]
    public void Years_lists_the_years_with_something_newest_first_and_always_this_year()
    {
        var plant = APlant("Old", 2023);
        var prop = Prop("P", 5, year: 2024);
        var photo = Taken(plant, At(2025, 6, 1));

        Assert.Equal([2026, 2025, 2024, 2023], PlantYear.Years([plant], [prop], [photo], Today, Utc));
        Assert.Equal([2026], PlantYear.Years([], [], [], Today, Utc));
    }

    [Fact]
    public void Years_leaves_out_what_does_not_count()
    {
        var plant = APlant("Old", 2023);
        var deleted = APlant("Deleted", 2019);
        deleted.DeletedAt = At(2020, 1, 1);
        var received = Taken(plant, At(2021, 1, 1));
        received.ReceivedHistoryId = Guid.NewGuid();

        Assert.Equal([2026, 2023], PlantYear.Years([plant, deleted], [], [received], Today, Utc));
    }
}
