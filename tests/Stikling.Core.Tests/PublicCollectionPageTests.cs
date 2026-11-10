using Stikling.Core.Examples;
using Stikling.Core.Models;
using Stikling.Core.Sharing;

namespace Stikling.Core.Tests;

public class PublicCollectionPageTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 15);

    private static PublicPageSettings Settings(
        PublicPageLayout layout = PublicPageLayout.Shelves, PublicPageOrder order = PublicPageOrder.Newest,
        bool cuttings = true, bool swap = false, bool wishlist = false, bool stories = false, string? title = null) =>
        new(title, null, layout, order, cuttings, swap, wishlist, stories);

    private static Plant APlant(string name, int addedDaysAgo = 0, Action<Plant>? change = null)
    {
        var plant = new Plant { Nickname = name, CreatedAt = Now.AddDays(-addedDaysAgo), UpdatedAt = Now.AddDays(-addedDaysAgo) };
        change?.Invoke(plant);
        return plant;
    }

    private static Propagation ACutting(string name, int addedDaysAgo = 0, Action<Propagation>? change = null)
    {
        var cutting = new Propagation
        {
            Nickname = name, StartedOn = Today.AddDays(-addedDaysAgo), CreatedAt = Now.AddDays(-addedDaysAgo), UpdatedAt = Now.AddDays(-addedDaysAgo)
        };
        change?.Invoke(cutting);
        return cutting;
    }

    private static Photo APhoto(Entity subject, SubjectType type, int takenDaysAgo = 0, Action<Photo>? change = null)
    {
        var photo = new Photo { SubjectType = type, SubjectId = subject.Id, TakenAt = Now.AddDays(-takenDaysAgo), UpdatedAt = Now.AddDays(-takenDaysAgo) };
        change?.Invoke(photo);
        return photo;
    }

    private static PublicSubjects Build(PublicPageSettings settings, IEnumerable<Plant>? plants = null,
        IEnumerable<Propagation>? cuttings = null, IEnumerable<Wish>? wishes = null) =>
        PublicCollectionPage.Subjects(settings, plants ?? [], cuttings ?? [], wishes ?? []);

    [Fact]
    public void Only_plants_that_are_here_and_not_hidden_show()
    {
        var shown = APlant("Monstera");
        var hidden = APlant("Hidden", change: p => p.HiddenFromPublicPage = true);
        var died = APlant("Died", change: p => p.Status = PlantStatus.Died);
        var given = APlant("Given", change: p => p.Status = PlantStatus.GivenAway);
        var sold = APlant("Sold", change: p => p.Status = PlantStatus.Sold);
        var deleted = APlant("Deleted", change: p => p.DeletedAt = Now);
        var demo = APlant("Coleus", change: p => p.Id = ExampleFamily.MotherId);

        var subjects = Build(Settings(), [shown, hidden, died, given, sold, deleted, demo]);

        Assert.Equal(["Monstera"], subjects.Plants.Select(p => p.Name));
    }

    [Fact]
    public void Only_cuttings_that_are_going_and_not_hidden_show_and_only_while_the_part_is_on()
    {
        var going = ACutting("Going");
        var hidden = ACutting("Hidden", change: p => p.HiddenFromPublicPage = true);
        var done = ACutting("Done", change: p => p.Stage = PropagationStage.Done);
        var failed = ACutting("Failed", change: p => p.Stage = PropagationStage.Failed);
        var deleted = ACutting("Deleted", change: p => p.DeletedAt = Now);
        var demo = ACutting("Demo", change: p => p.Id = ExampleFamily.CurrentCuttingId);
        IEnumerable<Propagation> all = [going, hidden, done, failed, deleted, demo];

        Assert.Equal(["Going"], Build(Settings(cuttings: true), cuttings: all).Cuttings.Select(p => p.Name));
        var off = Build(Settings(cuttings: false), cuttings: all);
        Assert.Empty(off.Cuttings);
        Assert.False(off.IsOn(PublicPartKind.Cuttings));
    }

    [Fact]
    public void Newest_comes_first_and_ties_go_by_id_so_pages_never_jump()
    {
        var old = APlant("Old", 10);
        var a = APlant("Same A", 1);
        var b = APlant("Same B", 1);
        var fresh = APlant("Fresh", 0);

        var first = Build(Settings(), [old, a, b, fresh]).Plants.Select(p => p.Id).ToList();
        var again = Build(Settings(), [b, fresh, old, a]).Plants.Select(p => p.Id).ToList();

        Assert.Equal(fresh.Id, first[0]);
        Assert.Equal(old.Id, first[3]);
        Assert.Equal(first, again);
    }

    [Fact]
    public void By_name_sorts_without_regard_to_case_and_ties_go_by_id()
    {
        var one = APlant("same");
        var two = APlant("SAME");

        var subjects = Build(Settings(order: PublicPageOrder.Name), [APlant("zebra plant"), APlant("Aloe"), one, two]);

        Assert.Equal("Aloe", subjects.Plants[0].Name);
        Assert.Equal("zebra plant", subjects.Plants[3].Name);
        Assert.Equal(new[] { one.Id, two.Id }.Order().ToList(), subjects.Plants.Skip(1).Take(2).Select(p => p.Id).ToList());
    }

    [Fact]
    public void A_tile_has_the_name_parts_and_nothing_else_about_the_plant()
    {
        var plant = APlant("Big Monstera", change: p =>
        {
            p.Genus = "Monstera";
            p.Species = "deliciosa";
            p.Cultivar = "Thai Constellation";
            p.Notes = "bought at the market for 40 kr";
        });
        var photo = APhoto(plant, SubjectType.Plant);

        var tile = Assert.Single(PublicCollectionPage.Front(Build(Settings(), [plant]), [photo], Now).Parts[0].Tiles);

        Assert.Equal("Big Monstera", tile.Name);
        Assert.Equal("Monstera deliciosa", tile.Latin);
        Assert.Equal("Thai Constellation", tile.Cultivar);
        Assert.Equal(photo.Id, tile.Cover?.Id);
        Assert.Null(tile.Caption);
    }

    [Fact]
    public void The_cover_is_the_chosen_one_when_it_shows_else_the_newest_own_photo_that_shows()
    {
        var plant = APlant("Monstera");
        var chosen = APhoto(plant, SubjectType.Plant, 9);
        var newest = APhoto(plant, SubjectType.Plant, 1);
        var hiddenNewer = APhoto(plant, SubjectType.Plant, 0, p => p.HiddenFromPublicPage = true);
        var reference = APhoto(plant, SubjectType.Plant, 0, p => p.IsReference = true);
        var deleted = APhoto(plant, SubjectType.Plant, 0, p => p.DeletedAt = Now);
        var other = APhoto(APlant("Other"), SubjectType.Plant, 0);
        IReadOnlyList<Photo> all = [chosen, newest, hiddenNewer, reference, deleted, other];

        Assert.Equal(chosen.Id, PublicCollectionPage.CoverOf(chosen.Id, SubjectType.Plant, plant.Id, all)?.Id);
        Assert.Equal(newest.Id, PublicCollectionPage.CoverOf(null, SubjectType.Plant, plant.Id, all)?.Id);
        Assert.Equal(newest.Id, PublicCollectionPage.CoverOf(hiddenNewer.Id, SubjectType.Plant, plant.Id, all)?.Id);
        Assert.Equal(newest.Id, PublicCollectionPage.CoverOf(other.Id, SubjectType.Plant, plant.Id, all)?.Id);
        Assert.Null(PublicCollectionPage.CoverOf(null, SubjectType.Plant, plant.Id, [hiddenNewer, reference, deleted]));
    }

    [Fact]
    public void A_tile_without_photos_has_no_cover()
    {
        var tile = Assert.Single(PublicCollectionPage.Front(Build(Settings(), [APlant("Monstera")]), [], Now).Parts[0].Tiles);

        Assert.Null(tile.Cover);
    }

    [Fact]
    public void Up_for_swap_lists_cuttings_then_cuttings_on_request_then_plants_with_captions()
    {
        var swapPlant = APlant("Pothos", change: p => p.Spare = SpareMark.UpForSwap);
        var spare = APlant("Hoya", change: p => p.Spare = SpareMark.CuttingsToSpare);
        var plain = APlant("Plain");
        var hiddenSwap = APlant("Secret", change: p => { p.Spare = SpareMark.UpForSwap; p.HiddenFromPublicPage = true; });
        var diedSwap = APlant("Gone", change: p => { p.Spare = SpareMark.UpForSwap; p.Status = PlantStatus.Died; });
        var cutting = ACutting("Monstera cutting", change: p => { p.SpareCount = 1; p.Stage = PropagationStage.Rooted; });
        var hiddenCutting = ACutting("Hidden cutting", change: p => { p.SpareCount = 1; p.HiddenFromPublicPage = true; });

        var subjects = Build(Settings(cuttings: false, swap: true), [swapPlant, spare, plain, hiddenSwap, diedSwap], [cutting, hiddenCutting]);

        Assert.Empty(subjects.Cuttings);
        Assert.Equal(["Monstera cutting", "Hoya", "Pothos"], subjects.Swap.Select(s => s.Name));
        Assert.Equal(["1 · rooted", "Cuttings to spare", "Up for swap"], subjects.Swap.Select(s => s.Caption));
        Assert.Equal([SubjectType.Propagation, SubjectType.Plant, SubjectType.Plant], subjects.Swap.Select(s => s.Type));
    }

    [Fact]
    public void Swap_is_empty_while_it_is_off()
    {
        var plant = APlant("Pothos", change: p => p.Spare = SpareMark.UpForSwap);

        var subjects = Build(Settings(swap: false), [plant]);

        Assert.Empty(subjects.Swap);
        Assert.False(subjects.IsOn(PublicPartKind.Swap));
    }

    [Fact]
    public void The_wishlist_is_names_only_for_wishes_not_yet_got()
    {
        var wish = new Wish
        {
            Genus = "Philodendron", Species = "spiritus-sancti", FoundAt = "Mr Green's stall", Notes = "the pink one", MostToPay = 400,
            CreatedAt = Now, UpdatedAt = Now
        };
        var got = new Wish { Name = "Got it", GotOn = Today, CreatedAt = Now, UpdatedAt = Now };
        var deleted = new Wish { Name = "Deleted", DeletedAt = Now, CreatedAt = Now, UpdatedAt = Now };

        var subjects = Build(Settings(wishlist: true), wishes: [wish, got, deleted]);

        Assert.Equal([wish.DisplayName], subjects.Wishlist.Select(l => l.Text));
        Assert.DoesNotContain("stall", string.Concat(subjects.Wishlist.Select(l => l.Text)));
        Assert.Empty(Build(Settings(wishlist: false), wishes: [wish]).Wishlist);
    }

    [Fact]
    public void The_wishlist_follows_the_page_order()
    {
        var older = new Wish { Name = "Zamioculcas", CreatedAt = Now.AddDays(-5), UpdatedAt = Now };
        var newer = new Wish { Name = "Alocasia", CreatedAt = Now, UpdatedAt = Now };
        var wishes = new[] { older, newer };

        Assert.Equal(["Alocasia", "Zamioculcas"], Build(Settings(wishlist: true, order: PublicPageOrder.Newest), wishes: wishes).Wishlist.Select(l => l.Text));
        Assert.Equal(["Alocasia", "Zamioculcas"], Build(Settings(wishlist: true, order: PublicPageOrder.Newest), wishes: [newer, older]).Wishlist.Select(l => l.Text));
        Assert.Equal(["Alocasia", "Zamioculcas"], Build(Settings(wishlist: true, order: PublicPageOrder.Name), wishes: wishes).Wishlist.Select(l => l.Text));
    }

    [Fact]
    public void The_front_page_has_the_title_counts_and_only_parts_that_are_on_and_not_empty()
    {
        var plants = Enumerable.Range(0, 3).Select(i => APlant($"Plant {i}", i)).ToList();
        var subjects = Build(Settings(cuttings: true, swap: true, wishlist: true, title: "  Lauge's plants "), plants, [ACutting("Cut")]);

        var page = PublicCollectionPage.Front(subjects, [], Now);

        Assert.Equal("Lauge's plants", page.Title);
        Assert.Equal([PublicPartKind.Plants, PublicPartKind.Cuttings], page.Parts.Select(p => p.Kind));
        Assert.Equal((3, 1), (page.PlantCount, page.PropagationCount));
        Assert.Equal("3 plants · 1 cutting", PublicCollectionPage.CountsLine(page));
    }

    [Fact]
    public void The_title_is_the_apps_words_when_none_is_set_and_cuttings_are_not_counted_while_off()
    {
        var subjects = Build(Settings(cuttings: false), [APlant("A")], [ACutting("Cut")]);

        var page = PublicCollectionPage.Front(subjects, [], Now);

        Assert.Equal("A plant collection", page.Title);
        Assert.Equal(0, page.PropagationCount);
        Assert.Equal("1 plant", PublicCollectionPage.CountsLine(page));
    }

    [Fact]
    public void An_empty_collection_still_has_a_plants_part()
    {
        var page = PublicCollectionPage.Front(Build(Settings(), []), [], Now);

        var part = Assert.Single(page.Parts);
        Assert.Equal((PublicPartKind.Plants, 0), (part.Kind, part.Total));
        Assert.Equal(Now, page.Updated);
        Assert.Null(page.Image);
    }

    [Theory]
    [InlineData(PublicPageLayout.Shelves, 12, 12)]
    [InlineData(PublicPageLayout.LongPage, 24, 12)]
    [InlineData(PublicPageLayout.PhotoWall, 48, 0)]
    public void The_front_page_carries_as_many_tiles_as_the_layout_says_and_the_total_is_all_of_them(
        PublicPageLayout layout, int plantTiles, int cuttingTiles)
    {
        var plants = Enumerable.Range(0, 60).Select(i => APlant($"P{i}", i)).ToList();
        var cuttings = Enumerable.Range(0, 30).Select(i => ACutting($"C{i}", i)).ToList();
        var subjects = Build(Settings(layout, cuttings: true), plants, cuttings);

        var page = PublicCollectionPage.Front(subjects, [], Now);

        Assert.Equal(plantTiles, page.Parts[0].Tiles.Count);
        Assert.Equal(60, page.Parts[0].Total);
        Assert.Equal(cuttingTiles, page.Parts[1].Tiles.Count);
        Assert.Equal(30, page.Parts[1].Total);
        Assert.Equal(plantTiles + cuttingTiles, PublicCollectionPage.FrontIds(subjects).Count);
    }

    [Fact]
    public void The_wishlist_on_the_front_page_carries_up_to_twelve_lines_except_on_the_photo_wall()
    {
        var wishes = Enumerable.Range(0, 20).Select(i => new Wish { Name = $"W{i}", CreatedAt = Now.AddDays(-i), UpdatedAt = Now }).ToList();

        var shelves = PublicCollectionPage.Front(Build(Settings(wishlist: true), wishes: wishes), [], Now);
        var wall = PublicCollectionPage.Front(Build(Settings(PublicPageLayout.PhotoWall, wishlist: true), wishes: wishes), [], Now);

        var shelf = shelves.Parts.Single(p => p.Kind == PublicPartKind.Wishlist);
        Assert.Equal((20, 12), (shelf.Total, shelf.Lines.Count));
        var link = wall.Parts.Single(p => p.Kind == PublicPartKind.Wishlist);
        Assert.Equal((20, 0), (link.Total, link.Lines.Count));
    }

    [Fact]
    public void Updated_is_the_newest_change_among_what_is_shown_and_the_image_is_the_first_plants_cover()
    {
        var first = APlant("First", 0);
        var second = APlant("Second", 1);
        var hiddenButNewer = APlant("Hidden", 0, p => { p.HiddenFromPublicPage = true; p.UpdatedAt = Now.AddDays(5); });
        var cover = APhoto(first, SubjectType.Plant, 2, p => p.UpdatedAt = Now.AddDays(2));

        var page = PublicCollectionPage.Front(Build(Settings(), [first, second, hiddenButNewer]), [cover], Now.AddDays(-30));

        Assert.Equal(Now.AddDays(2), page.Updated);
        Assert.Equal(cover.Id, page.Image?.Id);
    }

    [Fact]
    public void A_part_page_holds_48_tiles_and_a_page_out_of_range_is_missing()
    {
        var plants = Enumerable.Range(0, 100).Select(i => APlant($"P{i:000}", i)).ToList();
        var subjects = Build(Settings(order: PublicPageOrder.Name), plants);

        var one = PublicCollectionPage.Part(subjects, PublicPartKind.Plants, 1, [])!;
        var three = PublicCollectionPage.Part(subjects, PublicPartKind.Plants, 3, [])!;

        Assert.Equal((100, 1, 3, 48), (one.Total, one.Page, one.PageCount, one.Tiles.Count));
        Assert.Equal(4, three.Tiles.Count);
        Assert.Equal("P096", three.Tiles[0].Name);
        Assert.Equal(48, PublicCollectionPage.PartIds(subjects, PublicPartKind.Plants, 2).Count);
        Assert.Null(PublicCollectionPage.Part(subjects, PublicPartKind.Plants, 0, []));
        Assert.Null(PublicCollectionPage.Part(subjects, PublicPartKind.Plants, 4, []));
        Assert.Null(PublicCollectionPage.Part(subjects, PublicPartKind.Plants, -1, []));
    }

    [Fact]
    public void Page_one_of_an_empty_part_is_empty_and_a_part_that_is_off_is_missing()
    {
        var subjects = Build(Settings(cuttings: true, swap: false), [APlant("A")]);

        var empty = PublicCollectionPage.Part(subjects, PublicPartKind.Cuttings, 1, [])!;

        Assert.Equal((0, 1, 1), (empty.Total, empty.Page, empty.PageCount));
        Assert.Empty(empty.Tiles);
        Assert.Null(PublicCollectionPage.Part(subjects, PublicPartKind.Cuttings, 2, []));
        Assert.Null(PublicCollectionPage.Part(subjects, PublicPartKind.Swap, 1, []));
        Assert.Null(PublicCollectionPage.Part(subjects, PublicPartKind.Wishlist, 1, []));
    }

    [Fact]
    public void The_wishlist_part_holds_100_lines()
    {
        var wishes = Enumerable.Range(0, 130).Select(i => new Wish { Name = $"W{i:000}", CreatedAt = Now.AddDays(-i), UpdatedAt = Now }).ToList();
        var subjects = Build(Settings(wishlist: true), wishes: wishes);

        var two = PublicCollectionPage.Part(subjects, PublicPartKind.Wishlist, 2, [])!;

        Assert.Equal((2, 30, 130), (two.PageCount, two.Lines.Count, two.Total));
        Assert.Null(PublicCollectionPage.Part(subjects, PublicPartKind.Wishlist, 3, []));
    }

    // Stories

    [Fact]
    public void A_story_is_missing_while_stories_are_off_or_the_subject_does_not_show()
    {
        var plant = APlant("Monstera");
        var hidden = APlant("Hidden", change: p => p.HiddenFromPublicPage = true);
        var died = APlant("Died", change: p => p.Status = PlantStatus.Died);
        var cutting = ACutting("Cut");
        var done = ACutting("Done", change: p => p.Stage = PropagationStage.Done);
        var on = Settings(stories: true);
        var utc = TimeZoneInfo.Utc;

        Assert.NotNull(PublicCollectionPage.PlantStory(on, plant, null, null, [], [], [], [], utc, Today));
        Assert.Null(PublicCollectionPage.PlantStory(Settings(stories: false), plant, null, null, [], [], [], [], utc, Today));
        Assert.Null(PublicCollectionPage.PlantStory(on, hidden, null, null, [], [], [], [], utc, Today));
        Assert.Null(PublicCollectionPage.PlantStory(on, died, null, null, [], [], [], [], utc, Today));
        Assert.NotNull(PublicCollectionPage.PropagationStory(on, cutting, null, [], [], [], utc, Today));
        Assert.Null(PublicCollectionPage.PropagationStory(Settings(stories: false), cutting, null, [], [], [], utc, Today));
        Assert.Null(PublicCollectionPage.PropagationStory(Settings(cuttings: false, stories: true), cutting, null, [], [], [], utc, Today));
        Assert.Null(PublicCollectionPage.PropagationStory(on, done, null, [], [], [], utc, Today));
    }

    [Fact]
    public void A_story_shows_milestones_and_photos_but_never_the_text_of_a_note_and_cannot_be_kept()
    {
        var plant = APlant("Monstera");
        plant.AcquiredOn = LooseDate.Of(Today.AddDays(-20));
        var photo = APhoto(plant, SubjectType.Plant, 3);
        var entry = new TimelineEntry
        {
            SubjectType = SubjectType.Plant, SubjectId = plant.Id, Kind = TimelineKind.Note, Text = "paid 40 kr, from Jane",
            PhotoIds = [photo.Id], OccurredAt = photo.TakenAt, UpdatedAt = Now
        };

        var story = PublicCollectionPage.PlantStory(Settings(stories: true), plant, null, null, [], [photo], [entry], [], TimeZoneInfo.Utc, Today)!;

        Assert.Contains(story.Items, i => i.Kind == ShareItemKind.Milestone);
        Assert.Contains(story.Items, i => i.Photos.Any(p => p.Id == photo.Id));
        Assert.DoesNotContain(story.Items, i => i.Kind == ShareItemKind.Note);
        Assert.DoesNotContain(story.Items, i => i.Text is { } text && text.Contains("paid", StringComparison.Ordinal));
        Assert.False(story.CanKeep);
    }

    [Fact]
    public void A_hidden_cutting_leaves_no_milestone_or_count_in_its_parent_plants_story()
    {
        var plant = APlant("Monstera");
        var shown = ACutting("Shown");
        var hidden = ACutting("Hidden", change: p => p.HiddenFromPublicPage = true);
        TimelineEntry Propagated(Propagation p, int daysAgo) => new()
        {
            SubjectType = SubjectType.Plant, SubjectId = plant.Id, Kind = TimelineKind.Propagated, RelatedId = p.Id,
            OccurredAt = Now.AddDays(-daysAgo), UpdatedAt = Now
        };

        var story = PublicCollectionPage.PlantStory(Settings(stories: true), plant, null, null, [shown, hidden], [],
            [Propagated(shown, 5), Propagated(hidden, 3)], [], TimeZoneInfo.Utc, Today)!;

        Assert.Single(story.Items, i => i.Kind == ShareItemKind.Milestone && i.Day == Today.AddDays(-5));
        Assert.DoesNotContain(story.Items, i => i.Kind == ShareItemKind.Milestone && i.Day == Today.AddDays(-3));
    }

    [Fact]
    public void A_page_number_far_past_the_end_reads_nothing()
    {
        var subjects = PublicCollectionPage.Subjects(Settings(), [APlant("Monstera")], [], []);

        Assert.Empty(PublicCollectionPage.PartIds(subjects, PublicPartKind.Plants, int.MaxValue));
        Assert.Empty(PublicCollectionPage.PartIds(subjects, PublicPartKind.Plants, 2));
        Assert.Single(PublicCollectionPage.PartIds(subjects, PublicPartKind.Plants, 1));
    }

    [Fact]
    public void A_story_cannot_be_kept_even_when_units_were_given_away()
    {
        var cutting = ACutting("Cut", change: p => { p.GivenAwayCount = 1; p.InitialCount = 3; });

        var story = PublicCollectionPage.PropagationStory(Settings(stories: true), cutting, null, [], [], [], TimeZoneInfo.Utc, Today)!;

        Assert.False(story.CanKeep);
    }

    [Fact]
    public void Hidden_photos_are_left_out_of_a_story_including_the_rooting_it_came_from()
    {
        var rooting = ACutting("Rooting", 30, p => { p.Stage = PropagationStage.Done; p.FinishedOn = Today.AddDays(-10); });
        var plant = APlant("Pothos", change: p => p.FromPropagationId = rooting.Id);
        plant.AcquiredOn = LooseDate.Of(Today.AddDays(-10));
        var own = APhoto(plant, SubjectType.Plant, 2);
        var ownHidden = APhoto(plant, SubjectType.Plant, 3, p => p.HiddenFromPublicPage = true);
        var rootingPhoto = APhoto(rooting, SubjectType.Propagation, 20);
        var rootingHidden = APhoto(rooting, SubjectType.Propagation, 21, p => p.HiddenFromPublicPage = true);

        var story = PublicCollectionPage.PlantStory(Settings(stories: true), plant, null, rooting, [], [own, ownHidden, rootingPhoto, rootingHidden], [], [], TimeZoneInfo.Utc, Today)!;

        var shown = story.Items.SelectMany(i => i.Photos).Select(p => p.Id).ToHashSet();
        Assert.Equal(new HashSet<Guid> { own.Id, rootingPhoto.Id }, shown);
    }

    [Fact]
    public void A_story_does_not_use_a_hidden_rooting_or_a_hidden_parent()
    {
        var rooting = ACutting("Rooting", 30, p => { p.Stage = PropagationStage.Done; p.HiddenFromPublicPage = true; });
        var parent = APlant("Mother", change: p => p.HiddenFromPublicPage = true);
        var plant = APlant("Child", change: p => p.FromPropagationId = rooting.Id);
        var hiddenCutting = ACutting("Hidden", change: p => p.HiddenFromPublicPage = true);
        var rootingPhoto = APhoto(rooting, SubjectType.Propagation, 20);

        var story = PublicCollectionPage.PlantStory(Settings(stories: true), plant, parent, rooting, [hiddenCutting], [rootingPhoto], [], [], TimeZoneInfo.Utc, Today)!;

        Assert.Empty(story.Items.SelectMany(i => i.Photos));
        Assert.DoesNotContain("Mother", story.Line ?? "");
    }

    [Fact]
    public void A_rooting_that_is_finished_is_still_used_when_not_hidden()
    {
        var rooting = ACutting("Rooting", 30, p => { p.Stage = PropagationStage.Done; p.FinishedOn = Today.AddDays(-10); });
        var plant = APlant("Pothos", change: p => p.FromPropagationId = rooting.Id);
        plant.AcquiredOn = LooseDate.Of(Today.AddDays(-10));
        var rootingPhoto = APhoto(rooting, SubjectType.Propagation, 20);

        var story = PublicCollectionPage.PlantStory(Settings(stories: true), plant, null, rooting, [], [rootingPhoto], [], [], TimeZoneInfo.Utc, Today)!;

        Assert.Contains(story.Items.SelectMany(i => i.Photos), p => p.Id == rootingPhoto.Id);
    }

    // Which photos are served

    private static bool Serves(PublicPageSettings settings, Photo photo, Plant? plant, Propagation? cutting,
        IReadOnlyCollection<Photo> own, IReadOnlyCollection<Plant>? pottedUp = null) =>
        PublicCollectionPage.ServesPhoto(settings, photo, plant, cutting, own, pottedUp ?? [], TimeZoneInfo.Utc);

    [Fact]
    public void A_cover_is_served_and_another_photo_is_not_while_stories_are_off()
    {
        var plant = APlant("Monstera");
        var cover = APhoto(plant, SubjectType.Plant, 1);
        var other = APhoto(plant, SubjectType.Plant, 5);
        plant.CoverPhotoId = cover.Id;
        IReadOnlyCollection<Photo> own = [cover, other];

        Assert.True(Serves(Settings(), cover, plant, null, own));
        Assert.False(Serves(Settings(), other, plant, null, own));
        Assert.True(Serves(Settings(stories: true), other, plant, null, own));
    }

    [Fact]
    public void The_newest_photo_is_served_as_the_cover_when_none_is_chosen()
    {
        var plant = APlant("Monstera");
        var older = APhoto(plant, SubjectType.Plant, 5);
        var newer = APhoto(plant, SubjectType.Plant, 1);
        IReadOnlyCollection<Photo> own = [older, newer];

        Assert.True(Serves(Settings(), newer, plant, null, own));
        Assert.False(Serves(Settings(), older, plant, null, own));
    }

    [Fact]
    public void Hidden_reference_and_deleted_photos_and_photos_of_plants_that_do_not_show_are_never_served()
    {
        var plant = APlant("Monstera");
        var hiddenPlant = APlant("Hidden", change: p => p.HiddenFromPublicPage = true);
        var died = APlant("Died", change: p => p.Status = PlantStatus.Died);
        var on = Settings(stories: true);

        foreach (var make in new Action<Photo>[] { p => p.HiddenFromPublicPage = true, p => p.IsReference = true, p => p.DeletedAt = Now })
        {
            var photo = APhoto(plant, SubjectType.Plant, 0, make);
            Assert.False(Serves(on, photo, plant, null, [photo]));
        }

        var ofHidden = APhoto(hiddenPlant, SubjectType.Plant);
        var ofDied = APhoto(died, SubjectType.Plant);
        Assert.False(Serves(on, ofHidden, hiddenPlant, null, [ofHidden]));
        Assert.False(Serves(on, ofDied, died, null, [ofDied]));
        Assert.False(Serves(on, ofHidden, null, null, [ofHidden]));
    }

    [Fact]
    public void A_cutting_cover_is_served_under_cuttings_or_swap_and_its_other_photos_with_stories()
    {
        var cutting = ACutting("Cut", change: p => p.SpareCount = 1);
        var cover = APhoto(cutting, SubjectType.Propagation, 1);
        var other = APhoto(cutting, SubjectType.Propagation, 5);
        IReadOnlyCollection<Photo> own = [cover, other];

        Assert.True(Serves(Settings(cuttings: true), cover, null, cutting, own));
        Assert.False(Serves(Settings(cuttings: true), other, null, cutting, own));
        Assert.True(Serves(Settings(cuttings: true, stories: true), other, null, cutting, own));
        Assert.False(Serves(Settings(cuttings: false, stories: true), other, null, cutting, own));
        Assert.True(Serves(Settings(cuttings: false, swap: true), cover, null, cutting, own));
        Assert.False(Serves(Settings(cuttings: false, swap: false), cover, null, cutting, own));

        cutting.SpareCount = null;
        Assert.False(Serves(Settings(cuttings: false, swap: true), cover, null, cutting, own));
    }

    [Fact]
    public void The_photos_of_a_hidden_or_finished_cutting_are_not_served()
    {
        var hidden = ACutting("Hidden", change: p => p.HiddenFromPublicPage = true);
        var done = ACutting("Done", change: p => p.Stage = PropagationStage.Done);
        var on = Settings(stories: true);
        var a = APhoto(hidden, SubjectType.Propagation);
        var b = APhoto(done, SubjectType.Propagation);

        Assert.False(Serves(on, a, null, hidden, [a]));
        Assert.False(Serves(on, b, null, done, [b]));
    }

    [Fact]
    public void A_rooting_photo_is_served_for_a_shown_plant_with_stories_on_and_only_up_to_the_day_it_was_potted_up()
    {
        var rooting = ACutting("Rooting", 30, p => { p.Stage = PropagationStage.Done; p.FinishedOn = Today.AddDays(-10); });
        var plant = APlant("Pothos", change: p => p.FromPropagationId = rooting.Id);
        plant.AcquiredOn = LooseDate.Of(Today.AddDays(-10));
        var before = APhoto(rooting, SubjectType.Propagation, 20);
        var after = APhoto(rooting, SubjectType.Propagation, 2);
        IReadOnlyCollection<Photo> own = [before, after];
        var on = Settings(stories: true);

        Assert.True(Serves(on, before, null, rooting, own, [plant]));
        Assert.False(Serves(on, after, null, rooting, own, [plant]));
        Assert.False(Serves(Settings(stories: false), before, null, rooting, own, [plant]));
        Assert.False(Serves(on, before, null, rooting, own, []));

        plant.HiddenFromPublicPage = true;
        Assert.False(Serves(on, before, null, rooting, own, [plant]));
        plant.HiddenFromPublicPage = false;

        rooting.HiddenFromPublicPage = true;
        Assert.False(Serves(on, before, null, rooting, own, [plant]));
        rooting.HiddenFromPublicPage = false;

        before.HiddenFromPublicPage = true;
        Assert.False(Serves(on, before, null, rooting, own, [plant]));
    }
}
