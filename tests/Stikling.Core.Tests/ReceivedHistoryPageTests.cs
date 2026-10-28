using Stikling.Core.Models;
using Stikling.Core.Sharing;

namespace Stikling.Core.Tests;

/// <summary>What the page of a plant that was received with a history shows, and when a page can be kept.</summary>
public class ReceivedHistoryPageTests
{
    private static readonly DateOnly Today = new(2026, 10, 12);
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    private static DateTimeOffset At(int month, int day) => new(2026, month, day, 12, 0, 0, TimeSpan.Zero);

    private static ShareLinkSettings Settings(bool notes = true, params Guid[] leftOut) => new(notes, null, null, leftOut);

    private static Plant FriendsPlant(DateOnly? got = null) =>
        new() { Nickname = "Mona", Origin = PlantOrigin.Gift, AcquiredOn = LooseDate.Of(got ?? new DateOnly(2026, 10, 1)) };

    private static Propagation Cutting() =>
        new() { Nickname = "Mona cutting", Type = PropagationType.Cutting, Medium = GrowingMedium.Water, StartedOn = new DateOnly(2026, 8, 1) };

    private static Photo Copied(Entity subject, ReceivedHistory history, DateTimeOffset taken, SubjectType type = SubjectType.Plant) =>
        new() { SubjectType = type, SubjectId = subject.Id, TakenAt = taken, UpdatedAt = taken, ReceivedHistoryId = history.Id };

    private static ReceivedHistory HistoryOf(Entity subject, DateOnly arrived, SubjectType type = SubjectType.Plant, params ReceivedItem[] items) =>
        new() { SubjectType = type, SubjectId = subject.Id, Name = "Mona", ArrivedOn = arrived, UpdatedAt = At(10, 1), Items = [.. items] };

    private static ReceivedItem Item(int month, int day, ShareItemKind kind, string? count, string? text = null, params Photo[] photos) =>
        new() { Day = new DateOnly(2026, month, day), DayCount = count, Kind = kind, Text = text, PhotoIds = photos.Select(p => p.Id).ToList() };

    private static SharePageModel PlantPage(Plant plant, Propagation? from, IEnumerable<Photo> photos, IEnumerable<ReceivedHistory> received,
        ShareLinkSettings? settings = null, IEnumerable<TimelineEntry>? entries = null) =>
        SharePage.ForPlant(plant, null, from, [], photos, entries ?? [], received, new Dictionary<Guid, Propagation>(), settings ?? Settings(), Utc, Today);

    // ---- CanKeep ----

    [Theory]
    [InlineData(PlantStatus.Active, false)]
    [InlineData(PlantStatus.Died, false)]
    [InlineData(PlantStatus.GivenAway, true)]
    [InlineData(PlantStatus.Sold, true)]
    public void A_plant_can_be_kept_once_it_has_left(PlantStatus status, bool expected)
    {
        var plant = FriendsPlant();
        plant.Status = status;

        Assert.Equal(expected, PlantPage(plant, null, [], []).CanKeep);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(2, true)]
    public void A_propagation_can_be_kept_once_units_are_given_away(int givenAway, bool expected)
    {
        var cutting = Cutting();
        cutting.InitialCount = 3;
        cutting.GivenAwayCount = givenAway;

        var page = SharePage.ForPropagation(cutting, null, [], [], [], Settings(), Utc, Today);

        Assert.Equal(expected, page.CanKeep);
    }

    // ---- A plant with a history ----

    private static (Plant Plant, ReceivedHistory History, Photo First, Photo Second) PlantWithHistory(DateOnly? got = null)
    {
        var plant = FriendsPlant(got);
        var history = HistoryOf(plant, new DateOnly(2026, 10, 1));
        var first = Copied(plant, history, At(9, 1));
        var second = Copied(plant, history, At(9, 5));
        history.Items =
        [
            Item(9, 1, ShareItemKind.Photos, "day 3", photos: first),
            Item(9, 5, ShareItemKind.Note, "day 7", "Roots are showing", second)
        ];
        return (plant, history, first, second);
    }

    [Fact]
    public void A_received_history_comes_first_with_its_frozen_day_counts_and_got_it_after()
    {
        var (plant, history, first, second) = PlantWithHistory();

        var page = PlantPage(plant, null, [first, second], [history]);

        Assert.Equal(3, page.Items.Count);
        Assert.Equal(first.Id, page.Items[0].Photos.Single().Id);
        Assert.Equal(" · day 3", page.Items[0].DayCount);
        Assert.Equal("day 3", page.Items[0].Label);
        Assert.Equal(ShareItemKind.Note, page.Items[1].Kind);
        Assert.Equal("Roots are showing", page.Items[1].Text);
        Assert.Equal(" · day 7", page.Items[1].DayCount);
        Assert.Equal("Got it", page.Items[2].Text);
        Assert.Equal(2, page.PhotoCount);
        Assert.Equal(1, page.NoteCount);
    }

    [Fact]
    public void A_received_history_is_before_the_plants_own_items_on_the_same_day()
    {
        var (plant, history, first, second) = PlantWithHistory(got: new DateOnly(2026, 9, 5));
        var own = new Photo { SubjectType = SubjectType.Plant, SubjectId = plant.Id, TakenAt = At(9, 5).AddHours(-3), UpdatedAt = At(9, 5) };

        var page = PlantPage(plant, null, [first, second, own], [history]);

        // The note of the history on 5 Sep, then Got it, then the plant's own photo that day
        Assert.Equal(["Roots are showing", "Got it"], page.Items.Skip(1).Select(i => i.Text).OfType<string>().ToArray());
        Assert.Equal(own.Id, page.Items[3].Photos.Single().Id);
    }

    [Fact]
    public void A_note_of_the_history_needs_notes_to_be_shown()
    {
        var (plant, history, first, second) = PlantWithHistory();

        var hidden = PlantPage(plant, null, [first, second], [history], Settings(notes: false));
        var shown = PlantPage(plant, null, [first, second], [history], Settings(notes: true));

        Assert.Equal(ShareItemKind.Photos, hidden.Items[1].Kind);
        Assert.Null(hidden.Items[1].Text);
        Assert.Equal(second.Id, hidden.Items[1].Photos.Single().Id);
        Assert.Equal(0, hidden.NoteCount);
        Assert.Equal("Roots are showing", shown.Items[1].Text);
    }

    [Fact]
    public void A_note_of_the_history_without_photos_is_dropped_when_notes_are_hidden()
    {
        var plant = FriendsPlant();
        var history = HistoryOf(plant, new DateOnly(2026, 10, 1), SubjectType.Plant, Item(9, 1, ShareItemKind.Note, "day 1", "Only words"));

        var page = PlantPage(plant, null, [], [history], Settings(notes: false));

        Assert.Equal(["Got it"], page.Items.Select(i => i.Text).OfType<string>().ToArray());
    }

    [Fact]
    public void A_left_out_copied_photo_is_gone_and_an_item_without_photos_is_dropped()
    {
        var (plant, history, first, second) = PlantWithHistory();

        var page = PlantPage(plant, null, [first, second], [history], Settings(true, first.Id));

        Assert.DoesNotContain(page.Items, i => i.Photos.Any(p => p.Id == first.Id));
        Assert.Equal(2, page.Items.Count);
        Assert.Equal(1, page.PhotoCount);
    }

    [Fact]
    public void Copied_photos_are_not_shown_again_as_loose_photos()
    {
        var plant = FriendsPlant();
        var history = HistoryOf(plant, new DateOnly(2026, 10, 1));
        var copied = Copied(plant, history, At(9, 1));
        var stray = Copied(plant, history, At(9, 2));
        history.Items = [Item(9, 1, ShareItemKind.Photos, null, photos: copied)];

        var page = PlantPage(plant, null, [copied, stray], [history]);

        Assert.Single(page.Items.SelectMany(i => i.Photos), p => p.Id == copied.Id);
        Assert.DoesNotContain(page.Items.SelectMany(i => i.Photos), p => p.Id == stray.Id);
        Assert.Equal(1, page.PhotoCount);
    }

    [Fact]
    public void A_deleted_history_shows_nothing_and_the_newest_one_wins()
    {
        var (plant, history, first, second) = PlantWithHistory();
        var older = HistoryOf(plant, new DateOnly(2026, 10, 1), SubjectType.Plant, Item(9, 3, ShareItemKind.Milestone, null, "Old one"));
        older.UpdatedAt = At(9, 1);
        var deleted = HistoryOf(plant, new DateOnly(2026, 10, 1), SubjectType.Plant, Item(9, 4, ShareItemKind.Milestone, null, "Deleted one"));
        deleted.UpdatedAt = At(10, 5);
        deleted.DeletedAt = At(10, 5);

        var page = PlantPage(plant, null, [first, second], [older, history, deleted]);

        Assert.DoesNotContain(page.Items, i => i.Text is "Old one" or "Deleted one");
        Assert.Equal(first.Id, page.Items[0].Photos.Single().Id);
    }

    [Fact]
    public void A_copied_photo_works_as_the_cover()
    {
        var (plant, history, first, second) = PlantWithHistory();
        plant.CoverPhotoId = first.Id;

        var page = PlantPage(plant, null, [first, second], [history]);

        Assert.Equal(first.Id, page.Cover!.Id);
    }

    [Fact]
    public void The_history_of_another_plant_is_ignored()
    {
        var (plant, history, first, second) = PlantWithHistory();
        history.SubjectId = Guid.NewGuid();

        var page = PlantPage(plant, null, [first, second], [history]);

        Assert.Equal(["Got it"], page.Items.Select(i => i.Text).OfType<string>().ToArray());
    }

    // ---- A propagation with a history ----

    [Fact]
    public void A_propagation_with_a_history_has_no_repeated_milestones_and_gets_got_it_on_the_day_it_arrived()
    {
        var cutting = Cutting();
        cutting.FirstRootOn = new DateOnly(2026, 8, 19);
        cutting.RootedOn = new DateOnly(2026, 9, 20);
        var history = HistoryOf(cutting, new DateOnly(2026, 9, 10), SubjectType.Propagation,
            Item(8, 1, ShareItemKind.Milestone, "day 0", "Started in water"),
            Item(8, 19, ShareItemKind.Milestone, "day 18", "First root"));

        var page = SharePage.ForPropagation(cutting, null, [], [], [history], Settings(), Utc, Today);

        Assert.Equal(["Started in water", "First root", "Got it", "Rooted"], page.Items.Select(i => i.Text).OfType<string>().ToArray());
        Assert.Equal(" · day 18", page.Items[1].DayCount);
        Assert.Equal(new DateOnly(2026, 9, 10), page.Items[2].Day);
        Assert.Equal(" · day 50", page.Items[3].DayCount);
    }

    [Fact]
    public void A_plant_potted_up_from_a_received_propagation_shows_the_propagations_history_first()
    {
        var cutting = Cutting();
        cutting.FirstRootOn = new DateOnly(2026, 8, 19);
        cutting.FinishedOn = new DateOnly(2026, 10, 1);
        var plant = FriendsPlant();
        plant.FromPropagationId = cutting.Id;
        var history = HistoryOf(cutting, new DateOnly(2026, 8, 25), SubjectType.Propagation,
            Item(8, 1, ShareItemKind.Milestone, "day 0", "Started in water"),
            Item(8, 19, ShareItemKind.Milestone, "day 18", "First root"));

        var page = PlantPage(plant, cutting, [], [history]);

        Assert.Equal(["Started in water", "First root", "Got it", "Potted up"], page.Items.Select(i => i.Text).OfType<string>().ToArray());
    }
}
