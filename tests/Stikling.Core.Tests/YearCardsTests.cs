using Stikling.Core.Models;
using Stikling.Core.Year;

namespace Stikling.Core.Tests;

public class YearCardsTests
{
    private static string Label(Enum value) => value switch
    {
        GrowingMedium.Leca => "LECA",
        GrowingMedium.OrchidBark => "Orchid bark",
        _ => value.ToString()
    };

    private static DateOnly Utc(DateTimeOffset moment) => DateOnly.FromDateTime(moment.UtcDateTime);

    private static Photo APhoto(DateTimeOffset at, PhotoFrame? frame = null) =>
        new() { SubjectType = SubjectType.Plant, SubjectId = Guid.NewGuid(), TakenAt = at, Frame = frame };

    private static CollageItem Item(Photo photo) => new(SubjectType.Plant, photo.SubjectId, photo);

    private static PlantYearSummary Summary(
        int year = 2025,
        bool soFar = false,
        int added = 0,
        IReadOnlyList<OriginCount>? origins = null,
        PropagationsYear? propagations = null,
        QuickestRoot? quickest = null,
        BestMedium? medium = null,
        MostPhotographed? most = null,
        GrownThisYear? grown = null,
        IReadOnlyList<CollageItem>? collage = null,
        int photos = 0) =>
        new(year, soFar, new PlantsAdded(added, origins ?? []),
            propagations ?? new PropagationsYear(0, 0, 0, 0, true, true),
            quickest, medium, most, grown, collage ?? [], photos, []);

    private static IReadOnlyList<YearCard> Cards(PlantYearSummary summary, IEnumerable<Photo>? photos = null) =>
        YearCards.For(summary, Label, photos, Utc);

    private static YearCard Card(IReadOnlyList<YearCard> cards, string id) => cards.Single(c => c.Id == id);

    private static Propagation APropagation(string nickname = "Thai Con", GrowingMedium medium = GrowingMedium.Water) =>
        new() { Nickname = nickname, Medium = medium, StartedOn = new DateOnly(2025, 3, 1) };

    private static PlantYearSummary Everything()
    {
        var plant = new Plant { Nickname = "Monstera", Genus = "Monstera", Species = "deliciosa", Cultivar = "Albo" };
        var prop = APropagation();
        var first = APhoto(new DateTimeOffset(2025, 1, 12, 12, 0, 0, TimeSpan.Zero));
        var last = APhoto(new DateTimeOffset(2025, 11, 3, 12, 0, 0, TimeSpan.Zero));
        var collage = Enumerable.Range(0, 5)
            .Select(i => Item(APhoto(new DateTimeOffset(2025, 5, 1 + i, 12, 0, 0, TimeSpan.Zero))))
            .ToList();
        return Summary(
            added: 24,
            origins: [new(PlantOrigin.Purchased, 9), new(PlantOrigin.Propagated, 6), new(PlantOrigin.Gift, 5)],
            propagations: new PropagationsYear(10, 8, 10, 7, true, true),
            quickest: new QuickestRoot(prop, 9, Guid.NewGuid()),
            medium: new BestMedium(GrowingMedium.Leca, 5, 5, 17.4, true, 3),
            most: new MostPhotographed(plant, 31, first.Id),
            grown: new GrownThisYear(plant, first, last, 295),
            collage: collage,
            photos: 412);
    }

    [Fact]
    public void An_empty_year_has_no_cards()
    {
        Assert.Empty(Cards(Summary()));
    }

    [Fact]
    public void A_full_year_has_the_cards_in_order()
    {
        var cards = Cards(Everything());

        Assert.Equal(
            ["title", "plants-added", "propagations", "quickest-to-root", "medium", "most-photographed", "grown", "collage"],
            cards.Select(c => c.Id));
        Assert.Equal(
            [YearCardKind.Title, YearCardKind.Number, YearCardKind.Number, YearCardKind.Photo, YearCardKind.Number, YearCardKind.Photo, YearCardKind.Pair, YearCardKind.Collage],
            cards.Select(c => c.Kind));
    }

    [Fact]
    public void A_card_with_nothing_to_say_is_left_out()
    {
        var cards = Cards(Summary(added: 3, origins: [new(PlantOrigin.Purchased, 3)], photos: 2));

        Assert.Equal(["title", "plants-added"], cards.Select(c => c.Id));
    }

    [Fact]
    public void The_collage_needs_enough_photos()
    {
        var few = Enumerable.Range(0, PlantYear.MinimumForCollage - 1).Select(_ => Item(APhoto(DateTimeOffset.UnixEpoch))).ToList();
        var enough = few.Append(Item(APhoto(DateTimeOffset.UnixEpoch))).ToList();

        Assert.DoesNotContain(Cards(Summary(collage: few, photos: 3)), c => c.Id == "collage");
        Assert.Equal(PlantYear.MinimumForCollage, Card(Cards(Summary(collage: enough, photos: 4)), "collage").Collage.Count);
    }

    [Fact]
    public void The_title_leaves_out_figures_that_are_zero()
    {
        var title = Card(Cards(Summary(added: 0, propagations: new PropagationsYear(2, 0, 0, 0, true, true), photos: 12)), "title");

        Assert.Equal("My 2025 with my plants", title.Title);
        var figure = Assert.Single(title.Figures);
        Assert.Equal(new YearFigure("12", "photos"), figure);
    }

    [Fact]
    public void The_title_takes_the_most_photographed_photo_and_its_frame()
    {
        var summary = Everything();
        var photo = APhoto(DateTimeOffset.UnixEpoch, new PhotoFrame(0.3, 0.4, 2));
        summary = summary with { MostPhotographed = summary.MostPhotographed! with { PhotoId = photo.Id } };

        var title = Card(Cards(summary, [photo]), "title");

        Assert.Equal(photo.Id, title.Photo!.PhotoId);
        Assert.Equal(new PhotoFrame(0.3, 0.4, 2), title.Photo.Frame);
    }

    [Fact]
    public void The_title_falls_back_on_the_first_collage_photo_and_then_on_none()
    {
        var collage = Enumerable.Range(0, 4).Select(i => Item(APhoto(DateTimeOffset.UnixEpoch.AddDays(i)))).ToList();

        Assert.Equal(collage[0].Photo.Id, Card(Cards(Summary(collage: collage, photos: 4)), "title").Photo!.PhotoId);
        Assert.Null(Card(Cards(Summary(added: 1, origins: [new(PlantOrigin.Unknown, 1)])), "title").Photo);
    }

    [Fact]
    public void The_title_takes_at_most_three_figures_with_rooted_cuttings_or_propagations()
    {
        var cuttings = Card(Cards(Everything()), "title");
        Assert.Equal(["24 plants added", "7 cuttings rooted", "412 photos"], cuttings.Figures.Select(f => $"{f.Value} {f.Caption}"));

        var mixed = Card(Cards(Everything() with { Propagations = new PropagationsYear(10, 8, 10, 7, false, false) }), "title");
        Assert.Equal("propagations rooted", mixed.Figures[1].Caption);
    }

    [Fact]
    public void One_takes_the_singular()
    {
        var cards = Cards(Summary(
            added: 1,
            origins: [new(PlantOrigin.Gift, 1)],
            propagations: new PropagationsYear(1, 1, 1, 1, true, true),
            photos: 1));

        Assert.Equal(["1 plant added", "1 cutting rooted", "1 photo"], Card(cards, "title").Figures.Select(f => $"{f.Value} {f.Caption}"));
        Assert.Equal("plant added", Card(cards, "plants-added").Figures[0].Caption);
        Assert.Equal("1 gift", Card(cards, "plants-added").Line);
    }

    [Fact]
    public void The_plants_added_card_words_where_they_came_from()
    {
        var added = Card(Cards(Everything()), "plants-added");

        Assert.Equal("Plants added in 2025", added.Kicker);
        Assert.Equal("plant", added.Tone);
        Assert.Equal(new YearFigure("24", "plants added"), added.Figures[0]);
        Assert.Equal("9 bought · 6 propagated · 5 gifts", added.Line);
        Assert.Equal("Plants added in 2025: 24. 9 bought, 6 propagated, 5 gifts.", added.AltText);
    }

    [Theory]
    [InlineData(PlantOrigin.GrownFromSeed, 2, "2 from seed")]
    [InlineData(PlantOrigin.Swap, 4, "4 swaps")]
    [InlineData(PlantOrigin.Swap, 1, "1 swap")]
    [InlineData(PlantOrigin.Unknown, 1, "1 other")]
    public void Every_origin_has_its_words(PlantOrigin origin, int count, string expected)
    {
        var added = Card(Cards(Summary(added: count, origins: [new(origin, count)])), "plants-added");

        Assert.Equal(expected, added.Line);
    }

    [Fact]
    public void The_propagations_card_says_cuttings_when_everything_was_a_cutting()
    {
        var all = Card(Cards(Everything()), "propagations");
        Assert.Equal("Cuttings in 2025", all.Kicker);
        Assert.Equal(new YearFigure("8", "of 10 made it"), all.Figures[0]);
        Assert.Equal("10 started · 7 rooted", all.Line);

        var mixed = Card(Cards(Everything() with { Propagations = new PropagationsYear(10, 8, 10, 7, false, false) }), "propagations");
        Assert.Equal("Propagations in 2025", mixed.Kicker);
    }

    [Fact]
    public void The_propagations_card_shows_how_many_started_when_nothing_is_decided()
    {
        var card = Card(Cards(Summary(propagations: new PropagationsYear(4, 0, 0, 0, true, true))), "propagations");

        Assert.Equal(new YearFigure("4", "started"), card.Figures[0]);
    }

    [Fact]
    public void Quickest_to_root_is_a_photo_card_when_there_is_a_photo()
    {
        var card = Card(Cards(Everything()), "quickest-to-root");

        Assert.Equal(YearCardKind.Photo, card.Kind);
        Assert.Equal("Quickest to root", card.Kicker);
        Assert.Equal("Thai Con", card.Title);
        Assert.Equal("Rooted in 9 days in water", card.Line);
        Assert.NotNull(card.Photo);
        Assert.Contains("Photo of Thai Con.", card.AltText);
    }

    [Fact]
    public void Quickest_to_root_without_a_photo_is_a_number_card_with_the_name_in_the_line()
    {
        var summary = Everything();
        summary = summary with { Quickest = summary.Quickest! with { PhotoId = null, DaysToRoot = 1 } };

        var card = Card(Cards(summary), "quickest-to-root");

        Assert.Equal(YearCardKind.Number, card.Kind);
        Assert.Equal(new YearFigure("1", "day to root"), card.Figures[0]);
        Assert.Equal("Thai Con rooted in water.", card.Line);
        Assert.Null(card.Photo);
    }

    [Fact]
    public void A_plant_with_a_nickname_gets_its_botanical_name_split()
    {
        var most = Card(Cards(Everything()), "most-photographed");

        Assert.Equal("Monstera", most.Title);
        Assert.Equal("Monstera deliciosa", most.Latin);
        Assert.Equal("Albo", most.Cultivar);
        Assert.Equal("31 photos in 2025", most.Line);
    }

    [Fact]
    public void The_medium_card_compares_when_there_is_more_than_one_medium()
    {
        var card = Card(Cards(Everything()), "medium");

        Assert.Equal("Best medium in 2025", card.Kicker);
        Assert.Equal(new YearFigure("5", "of 5 made it in LECA"), card.Figures[0]);
        Assert.Equal("The best of the 3 I tried. Rooted in 17 days on average.", card.Line);
    }

    [Fact]
    public void The_medium_card_without_a_comparison_is_just_the_medium_used()
    {
        var summary = Everything() with { Medium = new BestMedium(GrowingMedium.Water, 4, 5, 12.4, false, 1) };

        var card = Card(Cards(summary), "medium");

        Assert.Equal("My medium in 2025", card.Kicker);
        Assert.Equal("of 5 made it in water", card.Figures[0].Caption);
        Assert.Equal("Rooted in 12 days on average.", card.Line);
    }

    [Fact]
    public void The_medium_card_has_no_line_with_nothing_to_say()
    {
        var summary = Everything() with { Medium = new BestMedium(GrowingMedium.OrchidBark, 3, 3, null, false, 1) };

        var card = Card(Cards(summary), "medium");

        Assert.Equal("of 3 made it in orchid bark", card.Figures[0].Caption);
        Assert.Null(card.Line);
    }

    [Fact]
    public void The_grown_card_gives_both_dates_and_says_so_far_for_the_current_year()
    {
        var past = Card(Cards(Everything()), "grown");
        Assert.Equal("Grown in 2025", past.Kicker);
        Assert.Equal("12 Jan", past.BeforeLabel);
        Assert.Equal("3 Nov", past.AfterLabel);
        Assert.Equal("295 days between these two photos", past.Line);
        Assert.NotNull(past.Before);
        Assert.NotNull(past.After);

        var current = Card(Cards(Everything() with { SoFar = true }), "grown");
        Assert.Equal("Grown this year", current.Kicker);
    }

    [Fact]
    public void The_collage_lists_every_photo_and_counts_the_photos_taken()
    {
        var card = Card(Cards(Everything()), "collage");

        Assert.Equal(5, card.Collage.Count);
        Assert.Equal("2025 in photos", card.Kicker);
        Assert.Equal("My 2025 in photos", card.Title);
        Assert.Equal("412 photos taken", card.Line);
    }
}
