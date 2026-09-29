using System.Text.Json;

namespace Stikling.Core.Sync;

/// <summary>How far this device has got with a collection.</summary>
/// <param name="Cursor">The last change number fetched from the server.</param>
/// <param name="Kinds">
/// The kinds of record the app knew when it last fetched. Records of a kind the app doesn't know
/// are skipped, so when a new kind is added, everything is fetched again to pick them up.
/// </param>
public sealed record SyncState(Guid CollectionId, long Cursor, string Kinds);

/// <summary>A record changed on this device that the server hasn't had yet.</summary>
/// <param name="Data">The record as it is now, or null if it is no longer on the device.</param>
/// <param name="Mark">What the change list noted about it, so a record changed again during a sync stays on the list.</param>
public sealed record PendingRecord(string Kind, Guid Id, JsonElement? Data, string Mark);

/// <summary>A record from the server to save on the device.</summary>
/// <param name="Replaces">
/// The updatedAt of the copy on the device it was compared with, exactly as stored, or null when
/// there was none. It is only saved if that copy is still there unchanged.
/// </param>
public sealed record ServerCopy(JsonElement Data, string? Replaces);

/// <summary>The records on this device, and what sync needs to keep track of there.</summary>
public interface ISyncStore
{
    Task<SyncState?> GetStateAsync();

    Task SaveStateAsync(SyncState state);

    /// <summary>Changes made here that haven't reached the server, of the given kinds, at most <paramref name="max"/>.</summary>
    Task<IReadOnlyList<PendingRecord>> GetPendingAsync(IReadOnlyCollection<string> kinds, int max);

    /// <summary>Takes the records off the change list, unless they changed again after they were read.</summary>
    Task MarkSentAsync(IReadOnlyList<PendingRecord> sent);

    /// <summary>Puts every record on the device on the change list.</summary>
    Task QueueAllAsync();

    /// <summary>The records of a kind with these ids that are on the device, deleted ones too.</summary>
    Task<IReadOnlyList<JsonElement>> GetAsync(string kind, IReadOnlyCollection<Guid> ids);

    /// <summary>
    /// Saves records from the server without putting them on the change list. Each one is skipped
    /// if the copy here changed after it was read, so an edit made during a sync isn't lost.
    /// </summary>
    Task SaveFromServerAsync(string kind, IReadOnlyList<ServerCopy> records);
}

/// <summary>The sync API, as the signed-in person.</summary>
public interface ISyncServer
{
    Task<PullResponse> PullAsync(Guid collectionId, long after);

    Task<PushResponse> PushAsync(Guid collectionId, IReadOnlyList<SyncRecord> records);

    /// <summary>The person's settings on the server, or null when none have been sent yet.</summary>
    Task<JsonElement?> GetSettingsAsync();

    /// <summary>
    /// Sends the settings, and gets back whichever version the server kept, or null when the
    /// server refused them.
    /// </summary>
    Task<JsonElement?> PutSettingsAsync(JsonElement settings);
}

/// <param name="Received">Records saved here from the server, so the screen can be refreshed when it's more than 0.</param>
/// <param name="Sent">Changes from here that reached the server.</param>
/// <param name="Refused">
/// Changes the server wouldn't keep, e.g. one dated in the future by a clock that is wrong. They
/// stay on the change list and are tried again next time.
/// </param>
public sealed record SyncResult(int Received, int Sent, int Refused);

/// <summary>
/// Keeps the records on this device and on the server the same. Each record is compared on its
/// own and the newest version wins, like restoring a backup, except that a delete is a change like
/// any other and spreads to the other devices. What changed on the server is found by its change
/// numbers rather than by dates, so a device clock that is off can't make a change get skipped.
/// </summary>
public sealed class SyncService(ISyncStore store, ISyncServer server)
{
    /// <summary>Stops a sync that keeps finding new changes from running forever. The next sync carries on.</summary>
    internal const int MaxRounds = 100;

    private static readonly string KnownKinds = string.Join(",", SyncKinds.All);

    /// <summary>
    /// Fetches what changed on the server, then sends what changed here. The first time this
    /// device syncs with the collection, everything already on it is sent, so it is merged with
    /// what the collection has.
    /// </summary>
    /// <param name="canEdit">False for someone who can only view the collection. Their own settings are still sent.</param>
    public async Task<SyncResult> SyncAsync(Guid collectionId, bool canEdit)
    {
        var state = await store.GetStateAsync();
        if (state is null || state.CollectionId != collectionId)
            state = await StartOverAsync(collectionId);
        else if (state.Kinds != KnownKinds)
        {
            state = state with { Cursor = 0, Kinds = KnownKinds };
            await store.SaveStateAsync(state);
        }

        var received = 0;
        for (var round = 0; round < MaxRounds; round++)
        {
            var page = await server.PullAsync(collectionId, state.Cursor);
            if (page.StartOver)
            {
                state = await StartOverAsync(collectionId);
                continue;
            }

            received += await SaveAsync(page.Records);
            state = state with { Cursor = page.Next };
            await store.SaveStateAsync(state);
            if (!page.More)
                break;
        }

        if (await server.GetSettingsAsync() is { } settings)
            received += await SaveAsync([new SyncRecord(SyncKinds.Settings, settings)]);

        var (sent, newer, refused) = await SendAsync(collectionId, canEdit);
        return new SyncResult(received + newer, sent, refused);
    }

    /// <summary>Everything here goes on the change list, and the collection is fetched from the start.</summary>
    private async Task<SyncState> StartOverAsync(Guid collectionId)
    {
        await store.QueueAllAsync();
        var state = new SyncState(collectionId, 0, KnownKinds);
        await store.SaveStateAsync(state);
        return state;
    }

    private async Task<(int Sent, int Newer, int Refused)> SendAsync(Guid collectionId, bool canEdit)
    {
        // A viewer's changes to the collection stay on the list, since the server won't take them
        IReadOnlyCollection<string> kinds = canEdit ? SyncKinds.All : [SyncKinds.Settings];
        int sent = 0, newer = 0;

        // Refused ones stay on the list for next time, but aren't sent again in this sync
        var refused = new HashSet<(string Kind, Guid Id)>();

        for (var round = 0; round < MaxRounds; round++)
        {
            var batch = await store.GetPendingAsync(kinds, SyncRules.BatchSize);
            var pending = batch.Where(p => !refused.Contains((p.Kind, p.Id))).ToList();
            if (pending.Count == 0)
                break;

            var done = new List<PendingRecord>();
            var forCollection = new List<PendingRecord>();
            foreach (var record in pending)
            {
                // It left the device after it was changed. There's nothing to send, only the entry to clear
                if (record.Data is not { } data)
                    done.Add(record);
                else if (record.Kind == SyncKinds.Settings)
                {
                    if (await server.PutSettingsAsync(data) is { } kept)
                    {
                        newer += await SaveAsync([new SyncRecord(SyncKinds.Settings, kept)]);
                        done.Add(record);
                        sent++;
                    }
                    else
                        refused.Add((record.Kind, record.Id));
                }
                else
                    forCollection.Add(record);
            }

            if (forCollection.Count > 0)
            {
                var response = await server.PushAsync(collectionId, forCollection.Select(p => new SyncRecord(p.Kind, p.Data!.Value)).ToList());
                var turnedAway = response.Refused.Select(r => r.Index).ToHashSet();
                for (var i = 0; i < forCollection.Count; i++)
                {
                    if (turnedAway.Contains(i))
                        refused.Add((forCollection[i].Kind, forCollection[i].Id));
                    else
                    {
                        done.Add(forCollection[i]);
                        sent++;
                    }
                }

                newer += await SaveAsync(response.Newer);
            }

            await store.MarkSentAsync(done);

            if (batch.Count < SyncRules.BatchSize)
                break;
        }

        return (sent, newer, refused.Count);
    }

    /// <summary>Saves the records that win over the copies here, and says how many there were.</summary>
    private async Task<int> SaveAsync(IReadOnlyList<SyncRecord> records)
    {
        var saved = 0;

        // A record of a kind this version of the app doesn't know is skipped. SyncState.Kinds makes
        // sure it is fetched again once the app knows it.
        foreach (var kind in records.Where(r => SyncKinds.IsKnown(r.Kind)).GroupBy(r => r.Kind))
        {
            var incoming = kind
                .Select(r => (Json: r.Data.GetRawText(), r.Data, Valid: RecordStamp.TryRead(r.Data, out var stamp), Stamp: stamp))
                .Where(r => r.Valid)
                .GroupBy(r => r.Stamp.Id)
                .Select(g => g.Aggregate((a, b) => SyncRules.Replaces(b.Stamp, b.Json, a.Stamp, a.Json) ? b : a))
                .ToList();

            var here = new Dictionary<Guid, (RecordStamp Stamp, string Json, string UpdatedAt)>();
            foreach (var record in await store.GetAsync(kind.Key, incoming.Select(r => r.Stamp.Id).ToList()))
            {
                if (RecordStamp.TryRead(record, out var stamp))
                    here[stamp.Id] = (stamp, record.GetRawText(), record.GetProperty("updatedAt").GetString()!);
            }

            var copies = new List<ServerCopy>();
            foreach (var (json, data, _, stamp) in incoming)
            {
                if (!here.TryGetValue(stamp.Id, out var current))
                    copies.Add(new ServerCopy(data, null));
                else if (SyncRules.Replaces(stamp, json, current.Stamp, current.Json))
                    copies.Add(new ServerCopy(data, current.UpdatedAt));
            }

            if (copies.Count > 0)
                await store.SaveFromServerAsync(kind.Key, copies);
            saved += copies.Count;
        }

        return saved;
    }
}
