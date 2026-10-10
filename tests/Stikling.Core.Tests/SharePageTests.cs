using Stikling.Core.Models;
using Stikling.Core.Sharing;

namespace Stikling.Core.Tests;

public class SharePageTests
{
    private static readonly DateOnly Today = new(2026, 10, 6);
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;
    private static readonly TimeZoneInfo Copenhagen = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");

    private static DateTimeOffset At(int month, int day, int hour = 12, int minute = 0) =>
        new(2026, month, day, hour, minute, 0, TimeSpan.Zero);

    private static ShareLinkSettings Settings(bool notes = true, string? name = null, string? line = null, params Guid[] leftOut) =>
        new(notes, name, line, leftOut);

    private static Plant NewPlant() => new() { Nickname = "Mona", Genus = "monstera", Species = "deliciosa" };

    private static Photo PhotoOf(Entity subject, DateTimeOffset taken, SubjectType type = SubjectType.Plant) =>
        new() { SubjectType = type, SubjectId = subject.Id, TakenAt = taken, UpdatedAt = taken };

    private static TimelineEntry EntryOf(Entity subject, TimelineKind kind, DateTimeOffset at, string? text = null,
        SubjectType type = SubjectType.Plant, params Photo[] photos) =>
        new()
        {
            SubjectType = type, SubjectId = subject.Id, Kind = kind, OccurredAt = at, Text = text, UpdatedAt = at,
            PhotoIds = photos.Select(p => p.Id).ToList()
        };

    private static SharePageModel PageOf(Plant plant, IEnumerable<Photo>? photos = null, IEnumerable<TimelineEntry>? entries = null,
        ShareLinkSettings? settings = null, TimeZoneInfo? zone = null, IReadOnlyDictionary<Guid, Propagation>? propagations = null) =>
        SharePage.ForPlant(plant, null, null, [], photos ?? [], entries ?? [], propagations ?? new Dictionary<Guid, Propagation>(),
            settings ?? Settings(), zone ?? Utc, Today);

    private static SharePageModel PageOf(Propagation propagation, IEnumerable<Photo>? photos = null, IEnumerable<TimelineEntry>? entries = null,
        ShareLinkSettings? settings = null, TimeZoneInfo? zone = null) =>
        SharePage.ForPropagation(propagation, null, photos ?? [], entries ?? [], settings ?? Settings(), zone ?? Utc, Today);

    private static Propagation NewCutting() =>
        new() { Nickname = "Mona cutting", Type = PropagationType.Cutting, Medium = GrowingMedium.Water, StartedOn = new DateOnly(2026, 8, 1) };

    [Fact]
    public void A_change_entry_never_shows_even_with_a_photo_or_an_event()
    {
        var plant = NewPlant();
        var photo = PhotoOf(plant, At(9, 1));
        var secret = EntryOf(plant, TimelineKind.Change, At(9, 1), "Moved to Bedroom, paid 400 kr", photos: photo);
        var potted = EntryOf(plant, TimelineKind.Change, At(9, 2), "Potted up: 2");
        potted.Event = TimelineEvent.PottedUp;
        var failed = EntryOf(plant, TimelineKind.Change, At(9, 3), "Failed: 1");
        failed.Event = TimelineEvent.Failed;

        var page = PageOf(plant, [photo], [secret, potted, failed]);

        Assert.DoesNotContain(page.Items, i => i.Text is not null);
        // The photo is still the plant's own photo, so it shows on the day it was taken
        Assert.Single(page.Items);
        Assert.Equal(photo.Id, page.Items[0].Photos.Single().Id);
    }

    [Fact]
    public void The_plants_own_private_fields_are_not_in_the_page()
    {
        var plant = NewPlant();
        plant.Notes = "Bought for 300 kr from Anna";
        plant.Source = "Anna";
        plant.PricePaid = 300;
        plant.Tags = ["secret"];
        plant.Status = PlantStatus.Died;
        plant.CauseOfDeath = "Rot";

        var page = PageOf(plant);
        var all = string.Join(" ", page.Items.Select(i => i.Text).Append(page.Name).Append(page.Line).Append(page.Latin));

        Assert.DoesNotContain("Anna", all);
        Assert.DoesNotContain("300", all);
        Assert.DoesNotContain("Rot", all);
        Assert.DoesNotContain("secret", all);
    }

    [Fact]
    public void A_note_hides_its_text_when_notes_are_off_but_keeps_its_photos()
    {
        var plant = NewPlant();
        var photo = PhotoOf(plant, At(9, 1));
        var note = EntryOf(plant, TimelineKind.Note, At(9, 1), "Private words", photos: photo);

        var page = PageOf(plant, [photo], [note], Settings(notes: false));

        var item = Assert.Single(page.Items);
        Assert.Null(item.Text);
        Assert.Equal(ShareItemKind.Photos, item.Kind);
        Assert.Single(item.Photos);
        Assert.Equal(0, page.NoteCount);
    }

    [Fact]
    public void A_note_shows_its_text_when_notes_are_on()
    {
        var plant = NewPlant();
        var photo = PhotoOf(plant, At(9, 1));
        var note = EntryOf(plant, TimelineKind.Note, At(9, 1), "A new leaf", photos: photo);

        var page = PageOf(plant, [photo], [note]);

        var item = Assert.Single(page.Items);
        Assert.Equal(ShareItemKind.Note, item.Kind);
        Assert.Equal("A new leaf", item.Text);
        Assert.Equal(1, page.NoteCount);
        Assert.Equal(1, page.PhotoCount);
    }

    [Fact]
    public void A_note_without_photos_is_dropped_when_notes_are_off()
    {
        var plant = NewPlant();
        var note = EntryOf(plant, TimelineKind.Note, At(9, 1), "Private words");

        Assert.Empty(PageOf(plant, entries: [note], settings: Settings(notes: false)).Items);
    }

    [Fact]
    public void A_note_edit_history_is_never_shown()
    {
        var plant = NewPlant();
        var note = EntryOf(plant, TimelineKind.Note, At(9, 1), "Now");
        note.Edits = [new TimelineEdit { Text = "Before the edit", OccurredAt = At(9, 1), ReplacedAt = At(9, 2) }];

        var page = PageOf(plant, entries: [note]);

        Assert.Equal("Now", Assert.Single(page.Items).Text);
    }

    [Fact]
    public void A_left_out_photo_is_gone_and_an_entry_emptied_by_it_is_dropped()
    {
        var plant = NewPlant();
        var gone = PhotoOf(plant, At(9, 1));
        var kept = PhotoOf(plant, At(9, 2));
        var onlyGone = EntryOf(plant, TimelineKind.Photo, At(9, 1), photos: gone);
        var other = EntryOf(plant, TimelineKind.Photo, At(9, 2), photos: kept);

        var page = PageOf(plant, [gone, kept], [onlyGone, other], Settings(leftOut: gone.Id));

        var item = Assert.Single(page.Items);
        Assert.Equal(kept.Id, item.Photos.Single().Id);
        Assert.Equal(1, page.PhotoCount);
    }

    [Fact]
    public void A_left_out_photo_does_not_show_as_a_loose_photo_either()
    {
        var plant = NewPlant();
        var gone = PhotoOf(plant, At(9, 1));

        Assert.Empty(PageOf(plant, [gone], settings: Settings(leftOut: gone.Id)).Items);
    }

    [Fact]
    public void A_note_that_loses_its_photos_keeps_its_text()
    {
        var plant = NewPlant();
        var gone = PhotoOf(plant, At(9, 1));
        var note = EntryOf(plant, TimelineKind.Note, At(9, 1), "Still here", photos: gone);

        var item = Assert.Single(PageOf(plant, [gone], [note], Settings(leftOut: gone.Id)).Items);

        Assert.Equal("Still here", item.Text);
        Assert.Empty(item.Photos);
    }

    [Fact]
    public void Deleted_photos_and_entries_are_gone()
    {
        var plant = NewPlant();
        var photo = PhotoOf(plant, At(9, 1));
        photo.DeletedAt = At(9, 5);
        var entry = EntryOf(plant, TimelineKind.Note, At(9, 2), "Deleted note");
        entry.DeletedAt = At(9, 5);
        var liveWithDeletedPhoto = EntryOf(plant, TimelineKind.Photo, At(9, 3), photos: photo);

        var page = PageOf(plant, [photo], [entry, liveWithDeletedPhoto]);

        Assert.Empty(page.Items);
        Assert.Equal(0, page.PhotoCount);
        Assert.Null(page.Cover);
    }

    [Fact]
    public void Records_of_other_subjects_are_gone()
    {
        var plant = NewPlant();
        var other = NewPlant();
        var propagation = NewCutting();
        propagation.Id = plant.Id; // same id, other kind of subject
        var theirPhoto = PhotoOf(other, At(9, 1));
        var samePhotoIdOtherType = PhotoOf(propagation, At(9, 1), SubjectType.Propagation);
        var theirNote = EntryOf(other, TimelineKind.Note, At(9, 1), "Not mine");
        var propagationNote = EntryOf(propagation, TimelineKind.Note, At(9, 1), "Not mine either", SubjectType.Propagation);
        var borrowed = EntryOf(plant, TimelineKind.Photo, At(9, 1), photos: theirPhoto);

        var page = PageOf(plant, [theirPhoto, samePhotoIdOtherType], [theirNote, propagationNote, borrowed]);

        Assert.Empty(page.Items);
    }

    [Fact]
    public void The_cover_is_the_chosen_photo_when_it_is_shown()
    {
        var plant = NewPlant();
        var old = PhotoOf(plant, At(9, 1));
        old.Frame = new PhotoFrame(0.2, 0.3, 2);
        var newest = PhotoOf(plant, At(9, 9));
        plant.CoverPhotoId = old.Id;

        var page = PageOf(plant, [old, newest]);

        Assert.Equal(old.Id, page.Cover!.Id);
        Assert.Equal(new PhotoFrame(0.2, 0.3, 2), page.Cover.Frame);
    }

    [Fact]
    public void The_cover_falls_back_to_the_newest_shown_photo_when_the_chosen_one_is_left_out_or_deleted()
    {
        var plant = NewPlant();
        var chosen = PhotoOf(plant, At(9, 1));
        var middle = PhotoOf(plant, At(9, 5));
        var newest = PhotoOf(plant, At(9, 9));
        var hidden = PhotoOf(plant, At(9, 20));
        plant.CoverPhotoId = chosen.Id;

        var page = PageOf(plant, [chosen, middle, newest, hidden], settings: Settings(leftOut: [chosen.Id, hidden.Id]));

        Assert.Equal(newest.Id, page.Cover!.Id);
    }

    [Fact]
    public void There_is_no_cover_without_photos()
    {
        Assert.Null(PageOf(NewPlant()).Cover);
    }

    [Fact]
    public void The_links_name_and_line_replace_the_cards_while_latin_and_cultivar_stay()
    {
        var plant = NewPlant();
        plant.Cultivar = "Thai Constellation";

        var page = PageOf(plant, settings: Settings(name: "  Our Mona ", line: " Grows fast "));

        Assert.Equal("Our Mona", page.Name);
        Assert.Equal("Grows fast", page.Line);
        Assert.Equal("Monstera deliciosa", page.Latin);
        Assert.Equal("Thai Constellation", page.Cultivar);
    }

    [Fact]
    public void Without_a_name_or_line_the_cards_words_are_used()
    {
        var propagation = NewCutting();
        var card = ShareCardText.ForPropagation(propagation, null, Today);

        var page = PageOf(propagation, settings: Settings(name: " ", line: ""));

        Assert.Equal(card.Name, page.Name);
        Assert.Equal(card.Line, page.Line);
    }

    [Fact]
    public void A_propagation_has_milestones_from_its_dates()
    {
        var p = NewCutting();
        p.Medium = GrowingMedium.Leca;
        p.FirstRootOn = new DateOnly(2026, 8, 19);
        p.FirstLeafOn = new DateOnly(2026, 9, 5);
        p.RootedOn = new DateOnly(2026, 9, 10);
        p.RecordPottedUp(1, new DateOnly(2026, 9, 20));

        var texts = PageOf(p).Items.Select(i => i.Text).ToList();

        Assert.Equal(["Started in LECA", "First root", "First leaf", "Rooted", "Potted up"], texts);
        Assert.All(PageOf(p).Items, i => Assert.Equal(ShareItemKind.Milestone, i.Kind));
    }

    [Fact]
    public void A_failed_propagation_says_it_did_not_make_it()
    {
        var p = NewCutting();
        p.RecordFailed(1, new DateOnly(2026, 8, 20));

        Assert.Equal("Didn't make it", PageOf(p).Items.Last().Text);
    }

    [Fact]
    public void Seeds_have_a_germination_milestone()
    {
        var p = NewCutting();
        p.Type = PropagationType.Seed;
        p.Medium = GrowingMedium.Soil;
        p.FirstGerminatedOn = new DateOnly(2026, 8, 10);

        var texts = PageOf(p).Items.Select(i => i.Text).ToList();

        Assert.Equal(["Sown in soil", "First seed germinated"], texts);
    }

    [Fact]
    public void A_milestone_counts_the_day_from_the_start()
    {
        var p = NewCutting();
        p.FirstRootOn = new DateOnly(2026, 8, 19);

        var items = PageOf(p).Items;

        Assert.Equal(" · day 0", items[0].DayCount);
        Assert.Equal(" · day 18", items[1].DayCount);
    }

    [Fact]
    public void A_plant_acquired_on_a_whole_day_has_a_got_it_milestone_and_counts_from_it()
    {
        var plant = NewPlant();
        plant.AcquiredOn = LooseDate.Of(new DateOnly(2026, 9, 1));
        var photo = PhotoOf(plant, At(9, 11));

        var items = PageOf(plant, [photo]).Items;

        Assert.Equal("Got it", items[0].Text);
        Assert.Equal(" · day 10", items[1].DayCount);
    }

    [Fact]
    public void A_plant_acquired_in_a_month_or_year_has_no_milestone()
    {
        var plant = NewPlant();
        plant.AcquiredOn = LooseDate.Of(2025, 3);
        Assert.Empty(PageOf(plant).Items);

        plant.AcquiredOn = LooseDate.Of(2025);
        Assert.Empty(PageOf(plant).Items);
    }

    [Fact]
    public void A_propagated_entry_says_what_kind_without_a_link_or_name()
    {
        var plant = NewPlant();
        var cutting = NewCutting();
        var corm = NewCutting();
        corm.Type = PropagationType.Corm;
        var gone = NewCutting();
        gone.DeletedAt = At(9, 9);
        var entry = EntryOf(plant, TimelineKind.Propagated, At(9, 1), "Took a cutting: Mona cutting");
        entry.RelatedType = SubjectType.Propagation;
        entry.RelatedId = cutting.Id;
        var second = EntryOf(plant, TimelineKind.Propagated, At(9, 2), "x");
        second.RelatedId = corm.Id;
        var third = EntryOf(plant, TimelineKind.Propagated, At(9, 3), "x");
        third.RelatedId = gone.Id;
        var map = new[] { cutting, corm, gone }.ToDictionary(p => p.Id);

        var items = PageOf(plant, entries: [entry, second, third], propagations: map).Items;

        Assert.Equal(["A cutting was taken", "A corm was taken", "A propagation was started"], items.Select(i => i.Text).ToList());
        Assert.All(items, i => Assert.Equal(ShareItemKind.Milestone, i.Kind));
    }

    [Fact]
    public void A_created_entry_is_dropped_but_its_photos_still_show()
    {
        var plant = NewPlant();
        var photo = PhotoOf(plant, At(9, 1));
        var created = EntryOf(plant, TimelineKind.Created, At(9, 1), "Added Mona", photos: photo);

        var item = Assert.Single(PageOf(plant, [photo], [created]).Items);

        Assert.Null(item.Text);
        Assert.Equal(photo.Id, item.Photos.Single().Id);
    }

    [Fact]
    public void A_photo_with_no_entry_shows_as_its_own_item_with_others_that_day_together()
    {
        var plant = NewPlant();
        var a = PhotoOf(plant, At(9, 1, 8));
        var b = PhotoOf(plant, At(9, 1, 9));
        var c = PhotoOf(plant, At(9, 3));

        var items = PageOf(plant, [c, b, a]).Items;

        Assert.Equal(2, items.Count);
        Assert.Equal([a.Id, b.Id], items[0].Photos.Select(p => p.Id).ToList());
        Assert.Equal([c.Id], items[1].Photos.Select(p => p.Id).ToList());
    }

    [Fact]
    public void A_photo_in_two_entries_shows_once()
    {
        var plant = NewPlant();
        var photo = PhotoOf(plant, At(9, 1));
        var first = EntryOf(plant, TimelineKind.Photo, At(9, 1), photos: photo);
        var second = EntryOf(plant, TimelineKind.Note, At(9, 2), "Again", photos: photo);

        var page = PageOf(plant, [photo], [first, second]);

        Assert.Equal(1, page.PhotoCount);
        Assert.Equal(1, page.Items.Sum(i => i.Photos.Count));
    }

    [Fact]
    public void A_photo_late_in_the_evening_UTC_is_the_next_day_in_Copenhagen()
    {
        var plant = NewPlant();
        var photo = PhotoOf(plant, At(9, 10, 23, 30));
        var entry = EntryOf(plant, TimelineKind.Photo, At(9, 10, 23, 30), photos: photo);

        Assert.Equal(new DateOnly(2026, 9, 11), PageOf(plant, [photo], [entry], zone: Copenhagen).Items.Single().Day);
        Assert.Equal(new DateOnly(2026, 9, 10), PageOf(plant, [photo], [entry], zone: Utc).Items.Single().Day);
        // A loose photo is placed the same way
        Assert.Equal(new DateOnly(2026, 9, 11), PageOf(plant, [photo], zone: Copenhagen).Items.Single().Day);
    }

    [Fact]
    public void Items_are_oldest_first_with_milestones_before_photos_and_notes_of_the_same_day()
    {
        var plant = NewPlant();
        plant.AcquiredOn = LooseDate.Of(new DateOnly(2026, 9, 1));
        var early = PhotoOf(plant, At(9, 1, 7));
        var photoEntry = EntryOf(plant, TimelineKind.Photo, At(9, 1, 7), photos: early);
        var note = EntryOf(plant, TimelineKind.Note, At(9, 1, 9), "Morning");
        var later = EntryOf(plant, TimelineKind.Note, At(8, 20), "Before I got it");

        var items = PageOf(plant, [early], [note, photoEntry, later]).Items;

        Assert.Equal(["Before I got it", "Got it", null, "Morning"], items.Select(i => i.Text).ToList());
        Assert.Equal(items.OrderBy(i => i.Day).Select(i => i.Day), items.Select(i => i.Day));
    }

    [Fact]
    public void Updated_is_the_newest_change_among_what_is_shown()
    {
        var plant = NewPlant();
        var shown = PhotoOf(plant, At(9, 1));
        shown.UpdatedAt = At(9, 2);
        var hidden = PhotoOf(plant, At(9, 3));
        hidden.UpdatedAt = At(9, 30);
        var note = EntryOf(plant, TimelineKind.Note, At(9, 4), "Hi");
        note.UpdatedAt = At(9, 5);
        var change = EntryOf(plant, TimelineKind.Change, At(9, 6), "Moved");
        change.UpdatedAt = At(9, 29);

        var page = PageOf(plant, [shown, hidden], [note, change], Settings(leftOut: hidden.Id));

        Assert.Equal(At(9, 5), page.Updated);
    }

    [Fact]
    public void A_deleted_parent_is_not_named_on_the_card()
    {
        var parent = new Plant { Nickname = "Gone plant", DeletedAt = At(9, 1) };
        var p = NewCutting();

        var page = SharePage.ForPropagation(p, parent, [], [], Settings(), Utc, Today);

        Assert.DoesNotContain("Gone plant", page.Line);
    }

    [Fact]
    public void Status_is_not_part_of_the_page()
    {
        var plant = NewPlant();
        plant.Status = PlantStatus.GivenAway;
        plant.LeftTo = "Anna";

        var page = PageOf(plant);

        Assert.DoesNotContain("Anna", page.Line ?? "");
    }

    [Fact]
    public void Rules_trim_a_name_and_turn_blank_into_null()
    {
        Assert.Equal("Mona", ShareLinkRules.Clean("  Mona \n"));
        Assert.Null(ShareLinkRules.Clean("   "));
        Assert.Null(ShareLinkRules.Clean(""));
        Assert.Null(ShareLinkRules.Clean(null));
    }

    [Fact]
    public void The_rules_match_the_share_sheet()
    {
        Assert.Equal(80, ShareLinkRules.MaxNameLength);
        Assert.Equal(200, ShareLinkRules.MaxLineLength);
        Assert.Equal(50, ShareLinkRules.MaxActivePerCollection);
        Assert.Equal(20, ShareLinkRules.MaxCreatedPerPersonPerDay);
        Assert.Equal(2000, ShareLinkRules.MaxLeftOutPhotos);
    }
}
