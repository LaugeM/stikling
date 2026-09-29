using System.Text.Json;
using Stikling.Core.Models;
using Stikling.Core.Sync;

namespace Stikling.Core.Tests;

public class SyncServiceTests
{
    private const string Plants = "plants";
    private static readonly DateTimeOffset Monday = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    private readonly Guid collectionId = Guid.NewGuid();
    private readonly FakeSyncServer server = new();
    private readonly FakeSyncStore phone = new();
    private readonly FakeSyncStore computer = new();

    private Task<SyncResult> Sync(FakeSyncStore device, bool canEdit = true) =>
        new SyncService(device, server).SyncAsync(collectionId, canEdit);

    private static Plant Plant(string nickname, DateTimeOffset updatedAt, Guid? id = null) =>
        new() { Id = id ?? Guid.NewGuid(), Nickname = nickname, CreatedAt = Monday, UpdatedAt = updatedAt };

    private static JsonElement Json(Entity entity) =>
        JsonSerializer.SerializeToElement(entity, entity.GetType(), FakeSyncStore.Json);

    [Fact]
    public async Task The_first_sync_sends_what_was_on_the_device_before_signing_in()
    {
        var plant = Plant("Monstera", Monday);
        phone.Keep(Plants, plant);

        var result = await Sync(phone);

        Assert.Equal(1, result.Sent);
        Assert.NotNull(server.Find(collectionId, Plants, plant.Id));
        Assert.Empty(phone.Pending);
    }

    [Fact]
    public async Task The_first_sync_merges_the_device_with_the_collection()
    {
        var fromComputer = Plant("Monstera", Monday);
        var fromPhone = Plant("Hoya", Monday);
        computer.Keep(Plants, fromComputer);
        phone.Keep(Plants, fromPhone);

        await Sync(computer);
        await Sync(phone);
        await Sync(computer);

        Assert.NotNull(phone.Get<Plant>(Plants, fromComputer.Id));
        Assert.NotNull(computer.Get<Plant>(Plants, fromPhone.Id));
    }

    [Fact]
    public async Task A_change_on_one_device_reaches_the_other()
    {
        await Sync(phone);
        await Sync(computer);
        var plant = Plant("Monstera", Monday);
        computer.Save(Plants, plant);

        await Sync(computer);
        var result = await Sync(phone);

        Assert.Equal("Monstera", phone.Get<Plant>(Plants, plant.Id)?.Nickname);
        Assert.Equal(1, result.Received);
        Assert.Empty(phone.Pending);
    }

    [Fact]
    public async Task After_the_first_sync_only_changes_are_sent()
    {
        phone.Keep(Plants, Plant("Monstera", Monday));
        await Sync(phone);
        server.Pushed.Clear();

        var nothingNew = await Sync(phone);
        phone.Save(Plants, Plant("Hoya", Monday));
        var oneNew = await Sync(phone);

        Assert.Equal(0, nothingNew.Sent);
        Assert.Equal(1, oneNew.Sent);
        Assert.Equal("Hoya", Assert.Single(server.Pushed).Data.GetProperty("nickname").GetString());
    }

    [Fact]
    public async Task A_newer_edit_from_another_device_replaces_the_one_here()
    {
        var plant = Plant("Old name", Monday);
        computer.Save(Plants, plant);
        await Sync(computer);
        await Sync(phone);

        computer.Save(Plants, Plant("New name", Monday.AddHours(1), plant.Id));
        await Sync(computer);
        await Sync(phone);

        Assert.Equal("New name", phone.Get<Plant>(Plants, plant.Id)?.Nickname);
    }

    [Fact]
    public async Task A_newer_edit_made_here_while_offline_wins()
    {
        var plant = Plant("First", Monday);
        computer.Save(Plants, plant);
        await Sync(computer);
        await Sync(phone);

        computer.Save(Plants, Plant("From the computer", Monday.AddHours(1), plant.Id));
        phone.Save(Plants, Plant("From the phone", Monday.AddHours(2), plant.Id));
        await Sync(computer);
        await Sync(phone);
        await Sync(computer);

        Assert.Equal("From the phone", phone.Get<Plant>(Plants, plant.Id)?.Nickname);
        Assert.Equal("From the phone", computer.Get<Plant>(Plants, plant.Id)?.Nickname);
        Assert.Empty(phone.Pending);
    }

    [Fact]
    public async Task An_older_edit_made_here_while_offline_gives_way()
    {
        var plant = Plant("First", Monday);
        computer.Save(Plants, plant);
        await Sync(computer);
        await Sync(phone);

        phone.Save(Plants, Plant("From the phone", Monday.AddHours(1), plant.Id));
        computer.Save(Plants, Plant("From the computer", Monday.AddHours(2), plant.Id));
        await Sync(computer);
        await Sync(phone);

        Assert.Equal("From the computer", phone.Get<Plant>(Plants, plant.Id)?.Nickname);
        Assert.Equal("From the computer", server.Find(collectionId, Plants, plant.Id)?.GetProperty("nickname").GetString());
        Assert.Empty(phone.Pending);
    }

    [Fact]
    public async Task An_older_edit_the_server_turns_down_is_replaced_by_the_one_it_sends_back()
    {
        var plant = Plant("First", Monday);
        phone.Save(Plants, plant);
        await Sync(phone);
        phone.Save(Plants, Plant("Older here", Monday.AddHours(1), plant.Id));

        // Another device's newer edit lands after this one has fetched, so only the send finds it
        server.BeforePush = () => server.Add(collectionId, Plants, Json(Plant("Newer elsewhere", Monday.AddHours(2), plant.Id)));
        var result = await Sync(phone);

        Assert.Equal("Newer elsewhere", phone.Get<Plant>(Plants, plant.Id)?.Nickname);
        Assert.Equal("Newer elsewhere", server.Find(collectionId, Plants, plant.Id)?.GetProperty("nickname").GetString());
        Assert.Equal(1, result.Received);
        Assert.Empty(phone.Pending);
    }

    [Fact]
    public async Task A_delete_spreads_to_the_other_devices()
    {
        var plant = Plant("Monstera", Monday);
        computer.Save(Plants, plant);
        await Sync(computer);
        await Sync(phone);

        var deleted = Plant("Monstera", Monday.AddDays(1), plant.Id);
        deleted.DeletedAt = deleted.UpdatedAt;
        computer.Save(Plants, deleted);
        await Sync(computer);
        await Sync(phone);

        Assert.True(phone.Get<Plant>(Plants, plant.Id)?.IsDeleted);
    }

    [Fact]
    public async Task An_edit_made_during_a_sync_is_not_overwritten()
    {
        var plant = Plant("First", Monday);
        computer.Save(Plants, plant);
        await Sync(computer);
        phone.Keep(Plants, plant);
        await Sync(phone);

        computer.Save(Plants, Plant("From the computer", Monday.AddHours(1), plant.Id));
        await Sync(computer);
        phone.BeforeSavingFromServer = () =>
        {
            phone.BeforeSavingFromServer = null;
            phone.Save(Plants, Plant("Typed during the sync", Monday.AddHours(2), plant.Id));
        };
        await Sync(phone);

        Assert.Equal("Typed during the sync", phone.Get<Plant>(Plants, plant.Id)?.Nickname);
        Assert.Equal("Typed during the sync", server.Find(collectionId, Plants, plant.Id)?.GetProperty("nickname").GetString());
    }

    [Fact]
    public async Task Settings_follow_the_person_to_every_device()
    {
        await Sync(phone);
        computer.Save(SyncKinds.Settings, new UserSettings { Theme = ThemeMode.Dark, UpdatedAt = Monday });

        await Sync(computer);
        await Sync(phone);

        Assert.Equal(ThemeMode.Dark, phone.Get<UserSettings>(SyncKinds.Settings, UserSettings.SettingsId)?.Theme);
        Assert.Empty(server.Pushed);
    }

    [Fact]
    public async Task Newer_settings_here_win_over_the_ones_on_the_server()
    {
        computer.Save(SyncKinds.Settings, new UserSettings { Theme = ThemeMode.Dark, UpdatedAt = Monday });
        await Sync(computer);
        phone.Save(SyncKinds.Settings, new UserSettings { Theme = ThemeMode.Light, UpdatedAt = Monday.AddHours(1) });

        await Sync(phone);
        await Sync(computer);

        Assert.Equal(ThemeMode.Light, computer.Get<UserSettings>(SyncKinds.Settings, UserSettings.SettingsId)?.Theme);
    }

    [Fact]
    public async Task A_viewer_gets_the_changes_but_only_sends_their_own_settings()
    {
        var plant = Plant("Monstera", Monday);
        computer.Save(Plants, plant);
        await Sync(computer);

        phone.Save(Plants, Plant("Edited by the sitter", Monday.AddHours(1), plant.Id));
        phone.Save(SyncKinds.Settings, new UserSettings { Theme = ThemeMode.Dark, UpdatedAt = Monday });
        server.Pushed.Clear();
        var result = await Sync(phone, canEdit: false);

        Assert.Empty(server.Pushed);
        Assert.Equal(1, result.Sent);
        Assert.NotNull(await server.GetSettingsAsync());
        Assert.Equal("Monstera", server.Find(collectionId, Plants, plant.Id)?.GetProperty("nickname").GetString());
        Assert.Single(phone.Pending);
    }

    [Fact]
    public async Task Fetching_carries_on_until_there_is_no_more()
    {
        server.PageSize = 2;
        for (var i = 0; i < 5; i++)
            server.Add(collectionId, Plants, Json(Plant($"Plant {i}", Monday)));

        var result = await Sync(phone);

        Assert.Equal(5, result.Received);
        Assert.Equal(5, phone.Records.Count);
        Assert.Equal(5, phone.State?.Cursor);
    }

    [Fact]
    public async Task Sending_carries_on_past_one_batch()
    {
        for (var i = 0; i < SyncRules.BatchSize + 5; i++)
            phone.Keep(Plants, Plant($"Plant {i}", Monday));

        var result = await Sync(phone);

        Assert.Equal(SyncRules.BatchSize + 5, result.Sent);
        Assert.Equal(SyncRules.BatchSize + 5, server.Pushed.Count);
        Assert.Empty(phone.Pending);
    }

    [Fact]
    public async Task A_kind_this_version_of_the_app_doesnt_know_is_skipped()
    {
        server.Add(collectionId, "compostBins", Json(Plant("Not a plant", Monday)));
        server.Add(collectionId, Plants, Json(Plant("Monstera", Monday)));

        var result = await Sync(phone);

        Assert.Equal(1, result.Received);
        Assert.DoesNotContain(phone.Records.Keys, k => k.Kind == "compostBins");
    }

    [Fact]
    public async Task Everything_is_fetched_again_once_the_app_knows_more_kinds()
    {
        var plant = Plant("Monstera", Monday);
        server.Add(collectionId, Plants, Json(plant));
        phone.State = new SyncState(collectionId, 1, "plants,settings");

        await Sync(phone);

        Assert.NotNull(phone.Get<Plant>(Plants, plant.Id));
        Assert.Equal(string.Join(",", SyncKinds.All), phone.State?.Kinds);
    }

    [Fact]
    public async Task Syncing_with_another_collection_sends_everything_here_to_it()
    {
        var plant = Plant("Monstera", Monday);
        phone.Keep(Plants, plant);
        await Sync(phone);

        var other = Guid.NewGuid();
        await new SyncService(phone, server).SyncAsync(other, canEdit: true);

        Assert.NotNull(server.Find(other, Plants, plant.Id));
        Assert.Equal(other, phone.State?.CollectionId);
    }

    [Fact]
    public async Task A_broken_record_from_the_server_is_skipped()
    {
        server.Add(collectionId, Plants, JsonSerializer.SerializeToElement(new { id = Guid.NewGuid(), nickname = "No date" }));
        server.Add(collectionId, Plants, Json(Plant("Monstera", Monday)));

        var result = await Sync(phone);

        Assert.Equal(1, result.Received);
    }

    [Fact]
    public async Task A_refused_change_stays_on_the_list_without_holding_up_the_rest()
    {
        var future = Plant("From a clock that is ahead", server.Now.AddDays(2));
        var fine = Plant("Monstera", Monday);
        phone.Save(Plants, future);
        phone.Save(Plants, fine);

        var result = await Sync(phone);

        Assert.Equal(1, result.Refused);
        Assert.Equal(1, result.Sent);
        Assert.NotNull(server.Find(collectionId, Plants, fine.Id));
        Assert.Null(server.Find(collectionId, Plants, future.Id));
        Assert.Equal((Plants, future.Id), Assert.Single(phone.Pending).Key);
    }

    [Fact]
    public async Task A_refused_change_goes_through_once_the_server_takes_it()
    {
        var plant = Plant("From a clock that is ahead", server.Now.AddDays(2));
        phone.Save(Plants, plant);
        await Sync(phone);

        server.Now = server.Now.AddDays(2);
        var result = await Sync(phone);

        Assert.Equal(0, result.Refused);
        Assert.NotNull(server.Find(collectionId, Plants, plant.Id));
        Assert.Empty(phone.Pending);
    }

    [Fact]
    public async Task Refused_settings_stay_on_the_list()
    {
        phone.Save(SyncKinds.Settings, new UserSettings { Theme = ThemeMode.Dark, UpdatedAt = server.Now.AddDays(2) });

        var result = await Sync(phone);

        Assert.Equal(1, result.Refused);
        Assert.Null(await server.GetSettingsAsync());
        Assert.Single(phone.Pending);
    }

    [Fact]
    public async Task A_device_ahead_of_the_server_sends_everything_again()
    {
        var plant = Plant("Monstera", Monday);
        phone.Save(Plants, plant);
        await Sync(phone);
        await Sync(phone);

        server.Forget();
        await Sync(phone);

        Assert.NotNull(server.Find(collectionId, Plants, plant.Id));
        Assert.Empty(phone.Pending);
    }

    [Fact]
    public async Task Two_different_versions_from_the_same_moment_settle_on_the_same_one()
    {
        var id = Guid.NewGuid();
        await Sync(phone);
        await Sync(computer);
        phone.Save(Plants, Plant("Aloe", Monday, id));
        computer.Save(Plants, Plant("Zamioculcas", Monday, id));

        await Sync(phone);
        await Sync(computer);
        await Sync(phone);

        Assert.Equal("Zamioculcas", phone.Get<Plant>(Plants, id)?.Nickname);
        Assert.Equal("Zamioculcas", computer.Get<Plant>(Plants, id)?.Nickname);
        Assert.Empty(phone.Pending);
        Assert.Empty(computer.Pending);
    }

    [Fact]
    public async Task The_same_version_written_differently_is_left_alone()
    {
        var plant = Plant("Kalanchoë", Monday);
        server.Add(collectionId, Plants, JsonSerializer.Deserialize<JsonElement>(Json(plant).GetRawText().Replace("ë", "\\u00EB")));
        phone.Keep(Plants, plant);
        phone.State = new SyncState(collectionId, 0, string.Join(",", SyncKinds.All));

        var result = await Sync(phone);

        Assert.Equal(0, result.Received);
    }

    [Fact]
    public void Every_kind_of_record_in_a_backup_syncs()
    {
        // A new store goes in BackupData, so this catches one left out of SyncKinds
        var inBackups = typeof(Backup.BackupData).GetProperties()
            .Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(List<>))
            .Select(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name))
            .Order();

        Assert.Equal(inBackups, SyncKinds.All.Order());
    }
}
