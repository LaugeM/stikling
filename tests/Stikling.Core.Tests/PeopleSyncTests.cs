using System.Text.Json;
using Stikling.Core.Models;
using Stikling.Core.People;
using Stikling.Core.Sync;

namespace Stikling.Core.Tests;

/// <summary>What gets sent and what is dropped from the change list, depending on the person's role.</summary>
public class PeopleSyncTests
{
    private const string Plants = "plants";
    private static readonly DateTimeOffset Monday = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    private readonly Guid collectionId = Guid.NewGuid();
    private readonly Guid me = Guid.NewGuid();
    private readonly FakeSyncServer server = new();
    private readonly FakeSyncStore phone = new();

    private MemberAccess As(CollectionRole role) => new(me, role, false, false);

    private Task<SyncResult> Sync(CollectionRole role) =>
        new SyncService(phone, server).SyncAsync(collectionId, As(role));

    private static Plant Plant(string nickname) =>
        new() { Id = Guid.NewGuid(), Nickname = nickname, CreatedAt = Monday, UpdatedAt = Monday };

    private CareLog Log(Guid? createdBy) =>
        new() { Id = Guid.NewGuid(), PlantId = Guid.NewGuid(), OccurredOn = new DateOnly(2026, 9, 28), Kind = CareKind.Watered, CreatedBy = createdBy, CreatedAt = Monday, UpdatedAt = Monday };

    private static TimelineEntry Entry(TimelineKind kind, Guid? createdBy) =>
        new() { Id = Guid.NewGuid(), SubjectId = Guid.NewGuid(), Kind = kind, CreatedBy = createdBy, CreatedAt = Monday, UpdatedAt = Monday, OccurredAt = Monday };

    private static JsonElement Json(Entity entity) =>
        JsonSerializer.SerializeToElement(entity, entity.GetType(), FakeSyncStore.Json);

    [Fact]
    public async Task An_editor_sends_everything()
    {
        var plant = Plant("Monstera");
        var log = Log(null);
        phone.Save(Plants, plant);
        phone.Save("careLogs", log);

        var result = await Sync(CollectionRole.Editor);

        Assert.Equal(2, result.Sent);
        Assert.NotNull(server.Find(collectionId, Plants, plant.Id));
        Assert.NotNull(server.Find(collectionId, "careLogs", log.Id));
        Assert.Empty(phone.Pending);
    }

    [Fact]
    public async Task A_helper_sends_what_they_added_and_the_rest_comes_off_the_list_and_stays_here()
    {
        var plant = Plant("Monstera");
        var mine = Log(me);
        var theirs = Log(Guid.NewGuid());
        var unknown = Log(null);
        var change = Entry(TimelineKind.Change, me);
        var note = Entry(TimelineKind.Note, me);
        phone.Save(Plants, plant);
        phone.Save("careLogs", mine);
        phone.Save("careLogs", theirs);
        phone.Save("careLogs", unknown);
        phone.Save("timeline", change);
        phone.Save("timeline", note);

        var result = await Sync(CollectionRole.Helper);

        Assert.Equal(2, result.Sent);
        Assert.Equal(0, result.Refused);
        Assert.NotNull(server.Find(collectionId, "careLogs", mine.Id));
        Assert.NotNull(server.Find(collectionId, "timeline", note.Id));
        Assert.Null(server.Find(collectionId, Plants, plant.Id));
        Assert.Null(server.Find(collectionId, "careLogs", theirs.Id));
        Assert.Null(server.Find(collectionId, "careLogs", unknown.Id));
        Assert.Null(server.Find(collectionId, "timeline", change.Id));

        Assert.Empty(phone.Pending);
        Assert.NotNull(phone.Get<Plant>(Plants, plant.Id));
        Assert.NotNull(phone.Get<CareLog>("careLogs", theirs.Id));
    }

    [Fact]
    public async Task A_helper_still_sends_their_own_settings()
    {
        phone.Save(SyncKinds.Settings, new UserSettings { Theme = ThemeMode.Dark, UpdatedAt = Monday });

        var result = await Sync(CollectionRole.Helper);

        Assert.Equal(1, result.Sent);
        Assert.NotNull(await server.GetSettingsAsync());
        Assert.Empty(phone.Pending);
    }

    [Fact]
    public async Task A_viewer_sends_only_settings_and_the_rest_comes_off_the_list()
    {
        phone.Save(Plants, Plant("Edited"));
        phone.Save("careLogs", Log(me));
        phone.Save(SyncKinds.Settings, new UserSettings { Theme = ThemeMode.Dark, UpdatedAt = Monday });

        var result = await Sync(CollectionRole.Viewer);

        Assert.Equal(1, result.Sent);
        Assert.Empty(server.Pushed);
        Assert.Empty(phone.Pending);
        Assert.NotNull(await server.GetSettingsAsync());
    }

    [Fact]
    public async Task A_change_made_again_while_it_was_being_dropped_stays_on_the_list()
    {
        var plant = Plant("Monstera");
        phone.Save(Plants, plant);

        // The list is read, then the same record is changed again before the entry is taken off
        var store = new ChangesAfterReading(phone, plant);
        await new SyncService(store, server).SyncAsync(collectionId, As(CollectionRole.Viewer));

        Assert.Single(phone.Pending);
    }

    [Fact]
    public async Task A_helper_reason_from_the_server_takes_the_change_off_the_list_without_counting_as_refused()
    {
        var log = Log(me);
        phone.Save("careLogs", log);
        server.Refuse = _ => HelperSync.HelperOwnershipProblem;

        var result = await Sync(CollectionRole.Helper);

        Assert.Equal(0, result.Sent);
        Assert.Equal(0, result.Refused);
        Assert.Empty(phone.Pending);
        Assert.NotNull(phone.Get<CareLog>("careLogs", log.Id));
    }

    [Fact]
    public async Task The_other_helper_reason_works_the_same()
    {
        phone.Save("careLogs", Log(me));
        server.Refuse = _ => HelperSync.HelperKindProblem;

        var result = await Sync(CollectionRole.Helper);

        Assert.Equal(0, result.Refused);
        Assert.Empty(phone.Pending);
    }

    [Fact]
    public async Task Another_refusal_keeps_the_change_and_counts_as_refused()
    {
        phone.Save("careLogs", Log(me));
        server.Refuse = _ => "The record was changed in the future. Check the date and time on the device.";

        var result = await Sync(CollectionRole.Helper);

        Assert.Equal(1, result.Refused);
        Assert.Single(phone.Pending);
    }

    [Fact]
    public async Task A_record_from_the_server_gets_createdBy_in_the_record_for_the_kinds_helpers_add()
    {
        var author = Guid.NewGuid();
        var log = Log(null);
        server.Add(collectionId, "careLogs", Json(log), createdBy: author);

        await Sync(CollectionRole.Helper);

        Assert.Equal(author, phone.Get<CareLog>("careLogs", log.Id)?.CreatedBy);
        Assert.Empty(phone.Pending);
    }

    [Fact]
    public async Task The_server_not_knowing_who_added_it_writes_null()
    {
        var log = Log(Guid.NewGuid());
        server.Add(collectionId, "careLogs", Json(log), createdBy: null);

        await Sync(CollectionRole.Editor);

        var saved = phone.Records[("careLogs", log.Id)];
        Assert.Equal(JsonValueKind.Null, saved.GetProperty("createdBy").ValueKind);
    }

    [Theory]
    [InlineData("propagationCareLogs")]
    [InlineData("timeline")]
    [InlineData("photos")]
    public async Task The_other_kinds_helpers_add_get_createdBy_too(string kind)
    {
        var author = Guid.NewGuid();
        var id = Guid.NewGuid();
        var data = JsonSerializer.SerializeToElement(
            new { id, createdAt = Monday, updatedAt = Monday, deletedAt = (DateTimeOffset?)null }, FakeSyncStore.Json);
        server.Add(collectionId, kind, data, createdBy: author);

        await Sync(CollectionRole.Editor);

        Assert.Equal(author, phone.Records[(kind, id)].GetProperty("createdBy").GetGuid());
    }

    [Fact]
    public async Task A_kind_helpers_dont_add_gets_no_createdBy()
    {
        var plant = Plant("Monstera");
        server.Add(collectionId, Plants, Json(plant), createdBy: Guid.NewGuid());

        await Sync(CollectionRole.Editor);

        Assert.False(phone.Records[(Plants, plant.Id)].TryGetProperty("createdBy", out _));
    }

    [Fact]
    public async Task A_helpers_own_record_comes_back_with_them_as_the_author_and_can_be_changed_again()
    {
        server.Caller = me;
        var log = Log(me);
        phone.Save("careLogs", log);
        await Sync(CollectionRole.Helper);

        log.Notes = "Still moist";
        log.UpdatedAt = Monday.AddHours(1);
        phone.Save("careLogs", log);
        server.Pushed.Clear();
        var result = await Sync(CollectionRole.Helper);

        Assert.Equal(1, result.Sent);
        Assert.Equal(me, phone.Get<CareLog>("careLogs", log.Id)?.CreatedBy);
    }

    /// <summary>A device whose record is changed again between reading the change list and dropping it.</summary>
    private sealed class ChangesAfterReading(FakeSyncStore inner, Plant plant) : ISyncStore
    {
        public Task<SyncState?> GetStateAsync() => inner.GetStateAsync();
        public Task SaveStateAsync(SyncState state) => inner.SaveStateAsync(state);
        public Task<IReadOnlyList<PendingRecord>> GetPendingAsync(IReadOnlyCollection<string> kinds, int max) => inner.GetPendingAsync(kinds, max);
        public Task MarkSentAsync(IReadOnlyList<PendingRecord> sent) => inner.MarkSentAsync(sent);
        public Task QueueAllAsync() => inner.QueueAllAsync();
        public Task<IReadOnlyList<JsonElement>> GetAsync(string kind, IReadOnlyCollection<Guid> ids) => inner.GetAsync(kind, ids);
        public Task SaveFromServerAsync(string kind, IReadOnlyList<ServerCopy> records) => inner.SaveFromServerAsync(kind, records);

        public Task DropPendingAsync(IReadOnlyList<PendingRecord> dropped)
        {
            plant.UpdatedAt = plant.UpdatedAt.AddMinutes(1);
            inner.Save("plants", plant);
            return inner.DropPendingAsync(dropped);
        }
    }
}
