using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Stikling.Core.Backup;
using Stikling.Core.Models;
using Stikling.Core.Sync;

namespace Stikling.Core.Tests;

/// <summary>
/// RecordKinds is the one list of kinds of record. These catch a step in docs/dev/new-record.md
/// that was missed, which would otherwise leave records out of backups or sync without a sign.
/// </summary>
public class RecordKindTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private static Entity NewRecord(RecordKind kind)
    {
        var record = (Entity)Activator.CreateInstance(kind.Type)!;
        record.Id = Guid.NewGuid();
        record.UpdatedAt = Now;
        return record;
    }

    private static Task Put(IRecordStore store, RecordKind kind, Entity record) =>
        (Task)typeof(IRecordStore).GetMethod(nameof(IRecordStore.PutAsync))!
            .MakeGenericMethod(kind.Type)
            .Invoke(store, [kind, record])!;

    /// <summary>A store with one record of every kind.</summary>
    private static async Task<(InMemoryRecordStore Store, Dictionary<string, Guid> Ids)> StoreWithOneOfEach()
    {
        var store = new InMemoryRecordStore();
        var ids = new Dictionary<string, Guid>();
        foreach (var kind in RecordKinds.All)
        {
            var record = NewRecord(kind);
            ids[kind.Name] = record.Id;
            await Put(store, kind, record);
        }
        return (store, ids);
    }

    private static async Task<BackupData> ExportAll(IRecordStore store)
    {
        var data = new BackupData();
        foreach (var list in BackupData.Lists)
            await list.ExportAsync(data, store);
        return data;
    }

    private static IEnumerable<PropertyInfo> ListProperties() =>
        typeof(BackupData).GetProperties()
            .Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(List<>));

    private static string RepoFile(params string[] path)
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "Stikling.slnx")))
            folder = folder.Parent;
        return Path.Combine([folder?.FullName ?? throw new InvalidOperationException("Stikling.slnx not found."), .. path]);
    }

    [Fact]
    public void Every_entity_has_exactly_one_record_kind()
    {
        var entities = typeof(Entity).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(Entity)))
            .ToList();

        foreach (var entity in entities)
            Assert.True(RecordKinds.All.Count(k => k.Type == entity) == 1, $"{entity.Name} needs exactly one kind in RecordKinds.");
        foreach (var kind in RecordKinds.All)
            Assert.Contains(kind.Type, entities);
    }

    [Fact]
    public void Kind_names_are_unique()
    {
        Assert.Equal(RecordKinds.All.Count, RecordKinds.All.Select(k => k.Name).Distinct().Count());
    }

    [Fact]
    public void The_backup_has_every_kind_once_in_the_order_of_its_lists()
    {
        Assert.Equal(RecordKinds.All.Select(k => k.Name).Order(), BackupData.Lists.Select(l => l.Kind.Name).Order());
        Assert.Equal(BackupData.Lists.Count, BackupData.Lists.Select(l => l.Kind).Distinct().Count());
        Assert.Contains(BackupData.PhotoList, BackupData.Lists);

        Assert.Equal(
            ListProperties().Select(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name)),
            BackupData.Lists.Select(l => l.Kind.Name));
    }

    [Fact]
    public async Task Each_list_reads_and_writes_its_own_property()
    {
        var (store, ids) = await StoreWithOneOfEach();
        var data = await ExportAll(store);

        foreach (var property in ListProperties())
        {
            var kind = BackupData.Lists.Single(l => l.Kind.Name == JsonNamingPolicy.CamelCase.ConvertName(property.Name)).Kind;
            var records = ((System.Collections.IEnumerable)property.GetValue(data)!).Cast<Entity>().ToList();

            var record = Assert.Single(records);
            Assert.IsType(kind.Type, record);
            Assert.Equal(ids[kind.Name], record.Id);
        }
    }

    [Fact]
    public async Task A_backup_round_trips_every_kind_and_a_second_restore_changes_nothing()
    {
        var (store, ids) = await StoreWithOneOfEach();
        var exported = await ExportAll(store);

        var data = JsonSerializer.Deserialize<BackupData>(JsonSerializer.Serialize(exported, Json), Json)!;

        var empty = new InMemoryRecordStore();
        var first = new MergeCounts();
        foreach (var list in BackupData.Lists)
            first += await list.RestoreAsync(data, empty);

        Assert.Equal(new MergeCounts(40, 0, 0, 0), first);
        foreach (var kind in RecordKinds.All)
            Assert.Equal([ids[kind.Name]], empty.IdsOf(kind));

        var second = new MergeCounts();
        foreach (var list in BackupData.Lists)
            second += await list.RestoreAsync(data, empty);

        Assert.Equal(new MergeCounts(0, 0, 0, 40), second);
    }

    [Fact]
    public async Task Counts_covers_every_list()
    {
        var (store, _) = await StoreWithOneOfEach();
        var data = await ExportAll(store);

        // A new Place has no RoomId, so it counts as a room
        var numbers = typeof(BackupCounts).GetProperties().Where(p => p.PropertyType == typeof(int)).ToList();
        Assert.Equal(BackupData.Lists.Count, numbers.Count);
        foreach (var number in numbers)
            Assert.True((int)number.GetValue(data.Counts)! == 1, $"{number.Name} should count its list.");
    }

    [Fact]
    public void The_sync_order_is_pinned()
    {
        // A device fetches everything again when this list changes
        Assert.Equal(
            "plants,propagations,propagationCareLogs,timeline,photos,careLogs,pestCases,pestTreatments,pots,soilMixes,"
            + "products,feeds,treatmentRecipes,places,putOffs,growLights,receivedHistories,awayStretches,sitterSheets,"
            + "floweringMarks,problems,problemUpdates,wishes,sellers,ownCareKinds,ownFields,careIntervals,treatmentPlans,"
            + "orders,orderItems,leaves,photoMarks,supplies,supplyEntries,shoppingItems,measurements,ownMeasureKinds,photoMarkups,placeReadings,settings",
            string.Join(",", SyncKinds.All));
    }

    private static HashSet<string> Names(string quoted) =>
        Regex.Matches(quoted, "\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToHashSet();

    [Fact]
    public void Every_kind_has_a_store_in_db_js_and_no_other_store_is_a_record()
    {
        var js = File.ReadAllText(RepoFile("src", "Stikling.Web", "wwwroot", "js", "db.js"));

        var notRecords = Names(Regex.Match(js, @"NOT_RECORDS\s*=\s*new Set\(\[(.*?)\]\)", RegexOptions.Singleline).Groups[1].Value);
        var created = Names(Regex.Match(js, @"STORES_V1\s*=\s*\[(.*?)\]", RegexOptions.Singleline).Groups[1].Value);
        created.UnionWith(Regex.Matches(js, "createObjectStore\\(\"([^\"]+)\"").Select(m => m.Groups[1].Value));

        var kinds = RecordKinds.All.Select(k => k.Name).ToHashSet();

        Assert.NotEmpty(notRecords);
        Assert.Empty(kinds.Except(created));
        Assert.Empty(created.Except(kinds).Except(notRecords));
        Assert.Empty(kinds.Intersect(notRecords));
    }

    [Fact]
    public void Only_settings_belong_to_the_person_and_three_kinds_are_not_content()
    {
        Assert.Equal(["settings"], RecordKinds.All.Where(k => k.Owner == RecordOwner.Person).Select(k => k.Name));
        Assert.Equal(
            ["putOffs", "settings", "sitterSheets"],
            RecordKinds.All.Where(k => !k.IsContent).Select(k => k.Name).Order());
    }
}
