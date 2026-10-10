using Stikling.Core.Models;
using Stikling.Core.Sharing;

namespace Stikling.Core.Tests;

public class ShareLinkServiceTests
{
    private static readonly Guid Collection = Guid.NewGuid();
    private static readonly Guid Plant = Guid.NewGuid();

    private readonly FakeShareLinkServer server = new();
    private readonly ShareLinkService service;

    public ShareLinkServiceTests() => service = new ShareLinkService(server);

    [Fact]
    public async Task Nothing_is_known_before_the_first_refresh()
    {
        Assert.Null(service.For(Plant));

        var made = await service.CreateAsync(SubjectType.Plant, Plant, "Europe/Copenhagen");

        Assert.False(made.Ok);
        Assert.Empty(server.Creates);
    }

    [Fact]
    public async Task A_refresh_picks_up_the_links_on_the_server_by_subject()
    {
        var link = server.Add(SubjectType.Plant, Plant);

        await service.RefreshAsync(Collection);

        Assert.Equal(link.Id, service.For(Plant)?.Id);
        Assert.Null(service.For(Guid.NewGuid()));
    }

    [Fact]
    public async Task A_refresh_that_finds_something_new_raises_Changed_and_one_that_finds_the_same_does_not()
    {
        var raised = 0;
        service.Changed += () => raised++;
        server.Add(SubjectType.Plant, Plant);

        await service.RefreshAsync(Collection);
        await service.RefreshAsync(Collection);

        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task A_refresh_that_fails_keeps_the_last_known_links_without_throwing()
    {
        server.Add(SubjectType.Plant, Plant);
        await service.RefreshAsync(Collection);
        var raised = 0;
        service.Changed += () => raised++;

        server.Problem = "Nope";
        await service.RefreshAsync(Collection);

        Assert.NotNull(service.For(Plant));
        Assert.Equal(0, raised);
    }

    [Fact]
    public async Task A_refresh_for_another_collection_starts_empty()
    {
        server.Add(SubjectType.Plant, Plant);
        await service.RefreshAsync(Collection);

        server.Problem = "Nope";
        await service.RefreshAsync(Guid.NewGuid());

        Assert.Null(service.For(Plant));
    }

    [Fact]
    public async Task Making_a_link_sends_the_choice_about_notes()
    {
        await service.RefreshAsync(Collection);

        await service.CreateAsync(SubjectType.Plant, Plant, null, showNotes: false);

        Assert.False(Assert.Single(server.Creates).ShowNotes);
    }

    [Fact]
    public async Task Clearing_empties_the_list_and_forgets_the_collection()
    {
        server.Add(SubjectType.Plant, Plant);
        await service.RefreshAsync(Collection);

        service.Clear();

        Assert.Null(service.For(Plant));
        Assert.False((await service.CreateAsync(SubjectType.Plant, Plant, null)).Ok);
    }

    [Fact]
    public async Task Making_a_link_sends_the_time_zone_and_keeps_the_link()
    {
        await service.RefreshAsync(Collection);
        var raised = 0;
        service.Changed += () => raised++;

        var made = await service.CreateAsync(SubjectType.Plant, Plant, "Europe/Copenhagen");

        Assert.True(made.Ok);
        Assert.Equal("Europe/Copenhagen", Assert.Single(server.Creates).TimeZone);
        Assert.Equal(made.Value!.Id, service.For(Plant)?.Id);
        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task A_link_that_cannot_be_made_comes_back_with_the_servers_words_and_is_not_kept()
    {
        await service.RefreshAsync(Collection);
        server.Problem = "It isn't on the server yet. Sync and try again.";

        var made = await service.CreateAsync(SubjectType.Plant, Plant, null);

        Assert.Equal("It isn't on the server yet. Sync and try again.", made.Problem);
        Assert.False(made.Offline);
        Assert.Null(service.For(Plant));
    }

    [Fact]
    public async Task A_server_that_cannot_be_reached_is_reported_as_offline()
    {
        await service.RefreshAsync(Collection);
        server.Problem = "The Stikling server can't be reached right now.";
        server.Offline = true;

        var made = await service.CreateAsync(SubjectType.Plant, Plant, null);

        Assert.True(made.Offline);
    }

    [Fact]
    public async Task A_failed_refresh_still_lets_a_link_be_made_once_the_server_answers()
    {
        server.Problem = "Down";
        await service.RefreshAsync(Collection);
        server.Problem = null;

        var made = await service.CreateAsync(SubjectType.Plant, Plant, null);

        Assert.True(made.Ok);
    }

    [Fact]
    public async Task Updating_cleans_the_words_and_keeps_the_saved_link()
    {
        server.Add(SubjectType.Plant, Plant);
        await service.RefreshAsync(Collection);
        var photo = Guid.NewGuid();

        var saved = await service.UpdateAsync(service.For(Plant)!, false, "  Mona  ", "   ", [photo], "Europe/Copenhagen");

        Assert.True(saved.Ok);
        var sent = Assert.Single(server.Updates);
        Assert.False(sent.ShowNotes);
        Assert.Equal("Mona", sent.Name);
        Assert.Null(sent.Line);
        Assert.Equal("Europe/Copenhagen", sent.TimeZone);
        var kept = service.For(Plant)!;
        Assert.False(kept.ShowNotes);
        Assert.Equal("Mona", kept.Name);
        Assert.Equal([photo], kept.LeftOutPhotoIds);
    }

    [Fact]
    public async Task A_failed_save_keeps_the_link_as_it_was()
    {
        server.Add(SubjectType.Plant, Plant);
        await service.RefreshAsync(Collection);
        server.Problem = "The line can have at most 200 characters.";

        var saved = await service.UpdateAsync(service.For(Plant)!, false, "Mona", null, [], null);

        Assert.Equal("The line can have at most 200 characters.", saved.Problem);
        Assert.True(service.For(Plant)!.ShowNotes);
    }

    [Fact]
    public async Task Turning_a_link_off_takes_it_off_the_list()
    {
        server.Add(SubjectType.Plant, Plant);
        await service.RefreshAsync(Collection);
        var raised = 0;
        service.Changed += () => raised++;

        var off = await service.TurnOffAsync(service.For(Plant)!);

        Assert.True(off.Ok);
        Assert.Null(service.For(Plant));
        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task A_link_that_could_not_be_turned_off_stays_on_the_list()
    {
        server.Add(SubjectType.Plant, Plant);
        await service.RefreshAsync(Collection);
        server.Problem = "Down";

        var off = await service.TurnOffAsync(service.For(Plant)!);

        Assert.False(off.Ok);
        Assert.NotNull(service.For(Plant));
    }
}
