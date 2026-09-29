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

    /// <summary>
    /// The newest version of a record wins, the same as when a backup is restored. A delete is a
    /// change like any other, so it wins over an older edit.
    /// </summary>
    public bool IsNewerThan(RecordStamp other) => UpdatedAt > other.UpdatedAt;
}

/// <summary>The size limits the server holds uploads to.</summary>
public static class SyncLimits
{
    /// <summary>The most records sent or fetched in one request.</summary>
    public const int BatchSize = 500;

    /// <summary>The longest one record's JSON may be, in characters. Photo images don't count, since they aren't in the record.</summary>
    public const int MaxRecordLength = 100_000;
}

/// <summary><c>POST /collections/{id}/records</c>: changes made on a device.</summary>
public sealed record PushRequest(List<SyncRecord> Records);

/// <param name="Newer">
/// Records the server already had a newer version of. They are sent back so the device can take
/// them in place of its own.
/// </param>
public sealed record PushResponse(List<SyncRecord> Newer);

/// <summary><c>GET /collections/{id}/records?after=N</c>: what changed on the server after change N.</summary>
/// <param name="Next">The change number to ask after next time.</param>
/// <param name="More">True when there is more to fetch straight away.</param>
public sealed record PullResponse(List<SyncRecord> Records, long Next, bool More);
