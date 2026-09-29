using System.Text.Json;

namespace Stikling.Core.Sync;

/// <summary>
/// One record on its way to or from the server: its kind, and the record itself as the device
/// stores it. The server keeps the record as it is and only reads its <see cref="RecordStamp"/>.
/// </summary>
public sealed record SyncRecord(string Kind, JsonElement Data);

/// <summary>The parts of a record that sync looks at.</summary>
public readonly record struct RecordStamp(Guid Id, DateTimeOffset UpdatedAt, DateTimeOffset? DeletedAt)
{
    /// <summary>
    /// Reads the id and dates, under the names the device stores them with. False when the
    /// record isn't an object with an id and an updatedAt.
    /// </summary>
    public static bool TryRead(JsonElement data, out RecordStamp stamp)
    {
        stamp = default;
        if (data.ValueKind != JsonValueKind.Object
            || !data.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String || !id.TryGetGuid(out var guid) || guid == Guid.Empty
            || !data.TryGetProperty("updatedAt", out var updated) || updated.ValueKind != JsonValueKind.String || !updated.TryGetDateTimeOffset(out var updatedAt))
            return false;

        DateTimeOffset? deletedAt = null;
        if (data.TryGetProperty("deletedAt", out var deleted) && deleted.ValueKind != JsonValueKind.Null)
        {
            if (deleted.ValueKind != JsonValueKind.String || !deleted.TryGetDateTimeOffset(out var at))
                return false;
            deletedAt = at;
        }

        stamp = new RecordStamp(guid, updatedAt, deletedAt);
        return true;
    }
}

/// <summary>The rules the server and the devices both follow, so they always agree.</summary>
public static class SyncRules
{
    /// <summary>The most records sent or fetched in one request.</summary>
    public const int BatchSize = 500;

    /// <summary>The longest one record's JSON may be, in characters. Photo images don't count, since they aren't in the record.</summary>
    public const int MaxRecordLength = 100_000;

    /// <summary>
    /// How far ahead of the server's clock a record's updatedAt may be. A device whose clock is
    /// far ahead would otherwise write versions that no later edit could beat.
    /// </summary>
    public static readonly TimeSpan MaxAhead = TimeSpan.FromDays(1);

    /// <summary>
    /// Whether a version of a record takes the place of the one already there. The newest wins,
    /// the same as when a backup is restored, and a delete is a change like any other. Two
    /// versions from the same moment are rare, but when they differ, the one whose JSON sorts
    /// last wins, so every device ends up with the same one.
    /// </summary>
    public static bool Replaces(RecordStamp incoming, string incomingJson, RecordStamp current, string currentJson) =>
        incoming.UpdatedAt != current.UpdatedAt
            ? incoming.UpdatedAt > current.UpdatedAt
            : string.CompareOrdinal(Normalised(incomingJson), Normalised(currentJson)) > 0;

    // The browser and the server can write the same record with different spacing or escaping
    private static string Normalised(string json) =>
        JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonElement>(json));

    /// <summary>Why the server won't keep a record, or null when it will.</summary>
    public static string? Problem(JsonElement data, DateTimeOffset now)
    {
        if (!RecordStamp.TryRead(data, out var stamp))
            return "A record needs to be an object with an id and an updatedAt.";
        if (data.GetRawText().Length > MaxRecordLength)
            return $"A record can be at most {MaxRecordLength} characters of JSON.";
        if (stamp.UpdatedAt > now + MaxAhead)
            return "The record was changed in the future. Check the date and time on the device.";
        return null;
    }
}

/// <summary><c>POST /collections/{id}/records</c>: changes made on a device.</summary>
public sealed record PushRequest(List<SyncRecord> Records);

/// <param name="Newer">
/// Records the server already had a newer version of. They are sent back so the device can take
/// them in place of its own.
/// </param>
/// <param name="Refused">Records the server didn't keep. The others were kept.</param>
public sealed record PushResponse(List<SyncRecord> Newer, List<RefusedRecord> Refused);

/// <param name="Index">Where the record was in the list that was sent.</param>
public sealed record RefusedRecord(int Index, string Reason);

/// <summary><c>GET /collections/{id}/records?after=N</c>: what changed on the server after change N.</summary>
/// <param name="Next">The change number to ask after next time.</param>
/// <param name="More">True when there is more to fetch straight away.</param>
/// <param name="StartOver">
/// The device asked after a change number the server hasn't reached, which happens when the
/// server's database was put back to an older copy. The device should fetch from the start and
/// send everything it has again.
/// </param>
public sealed record PullResponse(List<SyncRecord> Records, long Next, bool More, bool StartOver = false);
