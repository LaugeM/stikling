using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Stikling.Api.Collections;
using Stikling.Api.Data;
using Stikling.Core.Sync;

namespace Stikling.Api.Sync;

public static class RecordEndpoints
{
    public static IEndpointRouteBuilder MapRecords(this IEndpointRouteBuilder app)
    {
        var records = app.MapGroup("/collections/{collectionId:guid}/records");

        // GET: what changed after change number "after", oldest first, a page at a time
        records.MapGet("", async (Guid collectionId, long? after, StiklingDbContext db) =>
        {
            var from = Math.Max(after ?? 0, 0);
            var page = await db.Records
                .Where(r => r.CollectionId == collectionId && r.Version > from)
                .OrderBy(r => r.Version)
                .Take(SyncLimits.BatchSize + 1)
                .Select(r => new { r.Kind, r.Version, r.Data })
                .ToListAsync();

            var more = page.Count > SyncLimits.BatchSize;
            if (more)
                page.RemoveAt(page.Count - 1);

            return new PullResponse(
                page.Select(r => new SyncRecord(r.Kind, RecordJson.Parse(r.Data))).ToList(),
                page.Count > 0 ? page[^1].Version : from,
                more);
        })
        .RequireAuthorization(CollectionPolicies.View);

        // POST: changes from a device. Each record is kept if it's newer than the version here.
        records.MapPost("", async (Guid collectionId, PushRequest request, StiklingDbContext db) =>
        {
            var received = request.Records ?? [];
            var errors = new Dictionary<string, string[]>();
            if (received.Count > SyncLimits.BatchSize)
                errors["records"] = [$"Send at most {SyncLimits.BatchSize} records at a time."];

            var incoming = new List<(string Kind, RecordStamp Stamp, string Data)>();
            for (var i = 0; i < received.Count && errors.Count == 0; i++)
            {
                var record = received[i];
                if (record.Kind is null || !SyncKinds.IsCollection(record.Kind))
                    errors[$"records[{i}].kind"] = [$"\"{record.Kind}\" isn't a kind of record a collection holds."];
                else if (RecordJson.Check(record.Data) is { } problem)
                    errors[$"records[{i}].data"] = [problem];
                else
                {
                    RecordStamp.TryRead(record.Data, out var stamp);
                    incoming.Add((record.Kind, stamp, record.Data.GetRawText()));
                }
            }

            if (errors.Count > 0)
                return Results.ValidationProblem(errors);

            // The same record twice in one upload: only its newest version counts
            var newest = incoming
                .GroupBy(r => (r.Kind, r.Stamp.Id))
                .Select(g => g.MaxBy(r => r.Stamp.UpdatedAt))
                .ToList();

            await using var transaction = await db.Database.BeginTransactionAsync();

            // Locks the collection's row until the end of the transaction, so two uploads to one
            // collection take turns. Change numbers are then committed in order, and a device
            // fetching at the same moment can't pass a number that is yet to be committed.
            await db.Collections
                .Where(c => c.Id == collectionId)
                .ExecuteUpdateAsync(set => set.SetProperty(c => c.LastVersion, c => c.LastVersion));
            var collection = await db.Collections.SingleAsync(c => c.Id == collectionId);

            var ids = newest.Select(r => r.Stamp.Id).Distinct().ToList();
            var existing = (await db.Records
                    .Where(r => r.CollectionId == collectionId && ids.Contains(r.Id))
                    .ToListAsync())
                .ToDictionary(r => (r.Kind, r.Id));

            var newer = new List<SyncRecord>();
            foreach (var (kind, stamp, data) in newest)
            {
                if (!existing.TryGetValue((kind, stamp.Id), out var current))
                {
                    db.Records.Add(new SyncedRecord
                    {
                        CollectionId = collectionId,
                        Kind = kind,
                        Id = stamp.Id,
                        Version = ++collection.LastVersion,
                        UpdatedAt = stamp.UpdatedAt,
                        DeletedAt = stamp.DeletedAt,
                        Data = data,
                    });
                }
                else if (stamp.IsNewerThan(new RecordStamp(current.Id, current.UpdatedAt, current.DeletedAt)))
                {
                    current.Version = ++collection.LastVersion;
                    current.UpdatedAt = stamp.UpdatedAt;
                    current.DeletedAt = stamp.DeletedAt;
                    current.Data = data;
                }
                else if (stamp.UpdatedAt < current.UpdatedAt)
                {
                    newer.Add(new SyncRecord(current.Kind, RecordJson.Parse(current.Data)));
                }

                // The same version again, e.g. sent twice because an answer got lost: nothing to do
            }

            await db.SaveChangesAsync();
            await transaction.CommitAsync();

            return Results.Ok(new PushResponse(newer));
        })
        .RequireAuthorization(CollectionPolicies.Edit);

        return app;
    }
}

/// <summary>Checking and reading the record JSON the app sends.</summary>
public static class RecordJson
{
    /// <summary>What is wrong with a record, or null when it can be kept.</summary>
    public static string? Check(JsonElement data)
    {
        if (!RecordStamp.TryRead(data, out _))
            return "A record needs to be an object with an id and an updatedAt.";
        if (data.GetRawText().Length > SyncLimits.MaxRecordLength)
            return $"A record can be at most {SyncLimits.MaxRecordLength} characters of JSON.";
        return null;
    }

    public static JsonElement Parse(string data) => JsonSerializer.Deserialize<JsonElement>(data);
}
