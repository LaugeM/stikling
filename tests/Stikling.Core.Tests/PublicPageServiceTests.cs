using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Propagations;
using Stikling.Core.Sharing;
using Stikling.Core.Timeline;

namespace Stikling.Core.Tests;

public class PublicPageServiceTests
{
    private static readonly Guid Collection = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);

    private readonly FakePublicPageServer server = new();
    private readonly FakePlantRepository plants = new();
    private readonly FakePropagationRepository propagations = new();
    private readonly FakePhotoRepository photos = new();
    private readonly PublicPageService service;

    public PublicPageServiceTests()
    {
        var time = new FixedTime(Now);
        var timeline = new FakeTimelineRepository();
        service = new PublicPageService(
            server,
            new PlantService(plants, timeline, time),
            new PropagationService(propagations, plants, timeline, new FakePropagationCareLogRepository(), new FakeCareLogRepository(), photos, time),
            new TimelineService(timeline, photos, time));
    }

    [Fact]
    public async Task Nothing_is_known_before_the_first_refresh_and_a_page_cannot_be_turned_on()
    {
        Assert.False(service.Known);
        Assert.Null(service.Page);

        var made = await service.TurnOnAsync("Europe/Copenhagen", "da");

        Assert.False(made.Ok);
        Assert.True(made.Offline);
        Assert.Empty(server.TurnedOn);
    }

    [Fact]
    public async Task A_refresh_with_no_page_on_makes_it_known_to_be_off()
    {
        await service.RefreshAsync(Collection);

        Assert.True(service.Known);
        Assert.Null(service.Page);
    }

    [Fact]
    public async Task A_refresh_picks_up_a_page_made_on_another_device_and_raises_Changed_once()
    {
        var raised = 0;
        service.Changed += () => raised++;
        var page = server.Add();

        await service.RefreshAsync(Collection);
        await service.RefreshAsync(Collection);

        Assert.Equal(page, service.Page);
        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task A_refresh_that_fails_keeps_what_was_known_without_throwing()
    {
        server.Add();
        await service.RefreshAsync(Collection);
        var raised = 0;
        service.Changed += () => raised++;

        server.FailWith = 500;
        await service.RefreshAsync(Collection);

        Assert.NotNull(service.Page);
        Assert.True(service.Known);
        Assert.Equal(0, raised);
    }

    [Fact]
    public async Task A_refresh_for_another_collection_starts_unknown()
    {
        server.Add();
        await service.RefreshAsync(Collection);
        server.FailWith = 500;

        await service.RefreshAsync(Guid.NewGuid());

        Assert.Null(service.Page);
        Assert.False(service.Known);
    }

    [Fact]
    public async Task Clear_forgets_the_page()
    {
        server.Add();
        await service.RefreshAsync(Collection);

        service.Clear();

        Assert.Null(service.Page);
        Assert.False(service.Known);
    }

    [Fact]
    public async Task Load_says_why_it_failed_and_keeps_what_was_known()
    {
        server.Add();
        await service.RefreshAsync(Collection);
        server.FailWith = 503;

        var loaded = await service.LoadAsync();

        Assert.False(loaded.Ok);
        Assert.Equal(503, loaded.Status);
        Assert.NotNull(service.Page);
    }

    [Fact]
    public async Task Turning_it_on_sends_the_zone_and_the_normalized_language_and_keeps_the_page()
    {
        await service.RefreshAsync(Collection);

        var made = await service.TurnOnAsync("Europe/Copenhagen", "da-DK");

        Assert.True(made.Ok);
        Assert.Equal(made.Value, service.Page);
        var sent = Assert.Single(server.TurnedOn);
        Assert.Equal("Europe/Copenhagen", sent.TimeZone);
        Assert.Equal("da", sent.Language);
    }

    [Fact]
    public async Task Saving_the_settings_cleans_the_words_and_keeps_what_comes_back()
    {
        server.Add();
        await service.RefreshAsync(Collection);

        var result = await service.UpdateAsync(
            service.Page! with { Title = "  My plants  ", Line = "   ", Layout = PublicPageLayout.PhotoWall, ShowWishlist = true },
            "Europe/Copenhagen", "en-GB");

        Assert.True(result.Ok);
        var sent = Assert.Single(server.Updates);
        Assert.Equal("My plants", sent.Title);
        Assert.Null(sent.Line);
        Assert.Equal(PublicPageLayout.PhotoWall, sent.Layout);
        Assert.True(sent.ShowWishlist);
        Assert.Equal("en", sent.Language);
        Assert.Equal(PublicPageLayout.PhotoWall, service.Page!.Layout);
    }

    [Fact]
    public async Task A_failed_save_keeps_the_page_as_it_was_and_gives_the_status()
    {
        server.Add();
        await service.RefreshAsync(Collection);
        server.FailWith = 429;

        var result = await service.UpdateAsync(service.Page! with { ShowSwap = true }, null, "en");

        Assert.False(result.Ok);
        Assert.Equal(429, result.Status);
        Assert.False(service.Page!.ShowSwap);
    }

    [Theory]
    [InlineData("ab", NameProblem.TooShort)]
    [InlineData("Admin", NameProblem.Reserved)]
    [InlineData("my_plants", NameProblem.Characters)]
    [InlineData("-plants", NameProblem.Hyphens)]
    public async Task A_name_that_breaks_the_rules_is_not_sent(string name, NameProblem expected)
    {
        server.Add();
        await service.RefreshAsync(Collection);

        var result = await service.SetNameAsync(name);

        Assert.False(result.Ok);
        Assert.Equal(expected, result.NameProblem);
        Assert.Equal(400, result.Status);
        Assert.Empty(server.Names);
    }

    [Fact]
    public async Task A_name_is_sent_normalized_and_nothing_gives_the_name_up()
    {
        server.Add();
        await service.RefreshAsync(Collection);

        await service.SetNameAsync("  @Green-Thumb ");
        Assert.Equal("green-thumb", service.Page!.Name);

        await service.SetNameAsync("  ");
        Assert.Null(service.Page!.Name);
        Assert.Equal(["green-thumb", null], server.Names.Select(n => n.Name));
    }

    [Fact]
    public async Task Turning_it_off_forgets_the_page_but_a_failure_keeps_it()
    {
        server.Add();
        await service.RefreshAsync(Collection);

        server.FailWith = 403;
        var failed = await service.TurnOffAsync();
        Assert.False(failed.Ok);
        Assert.NotNull(service.Page);

        server.FailWith = null;
        var done = await service.TurnOffAsync();
        Assert.True(done.Ok);
        Assert.Null(service.Page);
        Assert.True(service.Known);
    }

    [Fact]
    public async Task Hiding_a_plant_a_cutting_and_a_photo_saves_the_record_and_does_nothing_when_unchanged()
    {
        var plant = new Plant { Nickname = "Monstera", UpdatedAt = Now };
        var cutting = new Propagation { Nickname = "Cutting", UpdatedAt = Now };
        var photo = new Photo { SubjectId = plant.Id, UpdatedAt = Now };
        plants.Plants[plant.Id] = plant;
        propagations.Propagations[cutting.Id] = cutting;
        photos.Photos[photo.Id] = photo;

        await service.SetPlantHiddenAsync(plant, true);
        await service.SetPlantHiddenAsync(plant, true);
        await service.SetPropagationHiddenAsync(cutting, true);
        await service.SetPhotoHiddenAsync(photo, true);

        Assert.True(plants.Plants[plant.Id].HiddenFromPublicPage);
        Assert.Equal(1, plants.Saves);
        Assert.True(propagations.Propagations[cutting.Id].HiddenFromPublicPage);
        Assert.True(photos.Photos[photo.Id].HiddenFromPublicPage);

        await service.SetPlantHiddenAsync(plant, false);
        Assert.False(plants.Plants[plant.Id].HiddenFromPublicPage);
    }

    [Fact]
    public void The_could_show_rules_ignore_only_the_hidden_flag()
    {
        var hiddenPlant = new Plant { Nickname = "A", HiddenFromPublicPage = true };
        var died = new Plant { Nickname = "B", Status = PlantStatus.Died };

        Assert.True(PublicCollectionPage.CouldShowPlant(hiddenPlant));
        Assert.False(PublicCollectionPage.ShowsPlant(hiddenPlant));
        Assert.False(PublicCollectionPage.CouldShowPlant(died));
        Assert.True(PublicCollectionPage.CouldShowPhoto(new Photo { HiddenFromPublicPage = true }));
        Assert.False(PublicCollectionPage.CouldShowPhoto(new Photo { IsReference = true }));
    }
}
