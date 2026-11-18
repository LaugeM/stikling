using Stikling.Core.People;
using Stikling.Core.Sync;

namespace Stikling.Core.Tests;

public class FileSyncServiceTests
{
    private readonly Guid collectionId = Guid.NewGuid();
    private readonly FakeFileSyncStore phone = new();
    private readonly FakeFileServer server;

    public FileSyncServiceTests() => server = new FakeFileServer(phone);

    private static readonly MemberAccess Editor = MemberAccess.Full(Guid.NewGuid());
    private static readonly MemberAccess Helper = new(Guid.NewGuid(), CollectionRole.Helper, false, false);
    private static readonly MemberAccess Viewer = new(Guid.NewGuid(), CollectionRole.Viewer, false, false);

    private Task<FileSyncResult> Sync(MemberAccess? me = null) =>
        new FileSyncService(phone, server).SyncAsync(collectionId, me ?? Editor);

    /// <summary>A file added on the phone whose record has already reached the server.</summary>
    private Guid Add()
    {
        var id = phone.Add();
        server.Records.Add(id);
        return id;
    }

    [Fact]
    public async Task A_new_file_is_sent_and_comes_off_the_list()
    {
        var id = Add();

        var result = await Sync();

        Assert.Equal([id], server.Sent);
        Assert.Equal(1, result.Uploaded);
        Assert.Empty(phone.Uploads);
    }

    [Fact]
    public async Task A_file_already_on_the_server_is_not_sent_again()
    {
        var id = Add();
        server.Files.Add(id);

        var result = await Sync();

        Assert.Empty(server.Sent);
        Assert.Equal(0, result.Uploaded);
        Assert.Empty(phone.Uploads);
    }

    [Fact]
    public async Task A_file_whose_record_the_server_doesnt_have_yet_is_tried_again_later()
    {
        var id = phone.Add();

        await Sync();
        Assert.Contains(id, phone.Uploads);

        server.Records.Add(id);
        await Sync();

        Assert.Equal([id], server.Sent);
        Assert.Empty(phone.Uploads);
    }

    [Fact]
    public async Task A_file_deleted_elsewhere_comes_off_the_list()
    {
        var id = Add();
        server.Deleted.Add(id);

        await Sync();

        Assert.Empty(server.Sent);
        Assert.Empty(phone.Uploads);
    }

    [Fact]
    public async Task A_file_whose_bytes_left_this_device_comes_off_the_list()
    {
        var id = Add();
        phone.Bytes.Remove(id);

        await Sync();

        Assert.Empty(server.Sent);
        Assert.Empty(phone.Uploads);
    }

    [Fact]
    public async Task A_file_the_server_will_never_take_comes_off_the_list()
    {
        var id = Add();
        server.Refuses.Add(id);

        await Sync();

        Assert.Empty(server.Sent);
        Assert.Empty(phone.Uploads);
    }

    [Fact]
    public async Task A_full_collection_stops_sending_and_keeps_the_rest_for_later()
    {
        for (var i = 0; i < 3; i++)
            Add();
        server.Limit = 2 * FakeFileServer.FileBytes;

        var result = await Sync();

        Assert.True(result.CollectionFull);
        Assert.Equal(2, result.Uploaded);
        Assert.Single(phone.Uploads);
    }

    [Fact]
    public async Task Someone_who_cant_edit_sends_nothing()
    {
        Add();

        Assert.Equal(new FileSyncResult(0, false), await Sync(Helper));
        Assert.Equal(new FileSyncResult(0, false), await Sync(Viewer));

        Assert.Empty(server.Sent);
        Assert.Single(phone.Uploads);
    }

    [Fact]
    public async Task Files_the_server_cant_take_yet_dont_hold_up_the_rest()
    {
        // Two batches of files the server can't take yet, mixed in with some it can
        var waiting = Enumerable.Range(0, 2 * FileRules.BatchSize).Select(_ => phone.Add()).ToList();
        var ready = Enumerable.Range(0, 50).Select(_ => Add()).ToList();

        var result = await Sync();

        Assert.Equal(ready.Count, result.Uploaded);
        Assert.Equal(waiting.Count, phone.Uploads.Count);
    }
}
