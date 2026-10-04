using Stikling.Core.Sync;

namespace Stikling.Core.Tests;

public class PhotoSyncServiceTests
{
    private readonly Guid collectionId = Guid.NewGuid();
    private readonly FakePhotoSyncStore phone = new();
    private readonly FakePhotoServer server;

    public PhotoSyncServiceTests() => server = new FakePhotoServer(phone);

    private Task<PhotoSyncResult> Sync(bool canEdit = true) =>
        new PhotoSyncService(phone, server).SyncAsync(collectionId, canEdit);

    /// <summary>A photo taken on the phone whose record has already reached the server.</summary>
    private Guid Take()
    {
        var id = phone.Take();
        server.Records.Add(id);
        return id;
    }

    [Fact]
    public async Task A_new_photo_sends_its_thumbnail_then_its_full_size()
    {
        var id = Take();

        var result = await Sync();

        Assert.Equal([(id, PhotoSize.Thumbnail), (id, PhotoSize.Full)], server.Sent);
        Assert.Equal(1, result.Uploaded);
        Assert.Empty(phone.Uploads);
    }

    [Fact]
    public async Task Images_already_on_the_server_are_not_sent_again()
    {
        var id = Take();
        server.Images.Add((id, PhotoSize.Thumbnail));

        await Sync();

        Assert.Equal([(id, PhotoSize.Full)], server.Sent);
        Assert.Empty(phone.Uploads);
    }

    [Fact]
    public async Task A_photo_whose_record_the_server_doesnt_have_yet_is_tried_again_later()
    {
        var id = phone.Take();

        var result = await Sync();

        Assert.Equal(0, result.Uploaded);
        Assert.Contains(id, phone.Uploads);
    }

    [Fact]
    public async Task A_photo_deleted_elsewhere_comes_off_the_list()
    {
        var id = Take();
        server.Deleted.Add(id);

        await Sync();

        Assert.Empty(server.Sent);
        Assert.Empty(phone.Uploads);
    }

    [Fact]
    public async Task A_photo_whose_images_left_this_device_comes_off_the_list()
    {
        var id = Take();
        phone.Images.Clear();

        await Sync();

        Assert.Empty(server.Sent);
        Assert.Empty(phone.Uploads);
    }

    [Fact]
    public async Task Only_the_thumbnail_is_sent_when_the_full_size_isnt_here()
    {
        var id = Take();
        phone.Images.Remove((id, PhotoSize.Full));

        await Sync();

        Assert.Equal([(id, PhotoSize.Thumbnail)], server.Sent);
        Assert.Empty(phone.Uploads);
    }

    [Fact]
    public async Task A_full_collection_stops_sending_and_keeps_the_rest_for_later()
    {
        var ids = Enumerable.Range(0, 3).Select(_ => Take()).ToList();
        server.Limit = 3 * FakePhotoServer.ImageBytes;

        var result = await Sync();

        Assert.True(result.CollectionFull);
        Assert.Equal(1, result.Uploaded);
        Assert.Equal(2, phone.Uploads.Count);
    }

    [Fact]
    public async Task Someone_who_can_only_view_sends_nothing_but_still_fetches()
    {
        Take();
        var theirs = server.Add();
        phone.Downloads.Add(theirs);

        var result = await Sync(canEdit: false);

        Assert.Empty(server.Sent);
        Assert.Single(phone.Uploads);
        Assert.Equal(1, result.Downloaded);
    }

    [Fact]
    public async Task Thumbnails_from_other_devices_are_fetched_but_full_sizes_wait_until_opened()
    {
        var theirs = server.Add();
        phone.Downloads.Add(theirs);

        var result = await Sync();

        Assert.Equal([(theirs, PhotoSize.Thumbnail)], server.Fetched);
        Assert.Equal(1, result.Downloaded);
        Assert.Empty(phone.Downloads);
    }

    [Fact]
    public async Task A_thumbnail_not_on_the_server_yet_is_fetched_once_it_is()
    {
        var theirs = server.Add(full: false, thumbnail: false);
        phone.Downloads.Add(theirs);

        await Sync();
        Assert.Contains(theirs, phone.Downloads);

        server.Images.Add((theirs, PhotoSize.Thumbnail));
        await Sync();

        Assert.Empty(phone.Downloads);
        Assert.Contains((theirs, PhotoSize.Thumbnail), phone.Images);
    }

    [Fact]
    public async Task Every_thumbnail_is_fetched_on_a_device_that_just_signed_in()
    {
        // More than a batch, fetched a few at a time, with some not on the server yet
        var there = Enumerable.Range(0, PhotoRules.BatchSize + 7).Select(_ => server.Add()).ToList();
        var notYet = Enumerable.Range(0, 3).Select(_ => server.Add(full: false, thumbnail: false)).ToList();
        phone.Downloads.UnionWith(there.Concat(notYet));

        var result = await Sync();

        Assert.Equal(there.Count, result.Downloaded);
        Assert.Equal(there.Select(id => (id, PhotoSize.Thumbnail)).ToHashSet(), server.Fetched.ToHashSet());
        Assert.Equal(notYet.ToHashSet(), phone.Downloads.ToHashSet());
    }

    [Fact]
    public async Task Photos_the_server_cant_take_yet_dont_hold_up_the_rest()
    {
        // Two batches of photos the server can't take yet, mixed in with some it can
        var waiting = Enumerable.Range(0, 2 * PhotoRules.BatchSize).Select(_ => phone.Take()).ToList();
        var ready = Enumerable.Range(0, 50).Select(_ => Take()).ToList();

        var result = await Sync();

        Assert.Equal(ready.Count, result.Uploaded);
        Assert.Equal(waiting.Count, phone.Uploads.Count);
    }
}
