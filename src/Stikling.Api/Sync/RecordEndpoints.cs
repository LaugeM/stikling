using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Stikling.Api.Collections;
using Stikling.Api.Data;
using Stikling.Api.Photos;
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

            var last = await db.Collections.Where(c => c.Id == collectionId).Select(c => c.LastVersion).SingleAsync();
            if (from > last)
                return new PullResponse([], 0, More: false, StartOver: true);

            var page = await db.Records
                .Where(r => r.CollectionId == collectionId && r.Version > from)
                .OrderBy(r => r.Version)
                .Take(SyncRules.BatchSize + 1)
                .Select(r => new { r.Kind, r.Version, r.Data, r.CreatedBy })
                .ToListAsync();

            var more = page.Count > SyncRules.BatchSize;
            if (more)
                page.RemoveAt(page.Count - 1);

            return new PullResponse(
                page.Select(r => new SyncRecord(r.Kind, Parse(r.Data), r.CreatedBy)).ToList(),
                page.Count > 0 ? page[^1].Version : from,
                more);
        })
        .RequireAuthorization(CollectionPolicies.View);

        // POST: changes from a device. Each record is kept if it wins over the version here, and
        // one that can't be kept is refused on its own, so it doesn't hold up the rest.
        // A helper may send some kinds of record, and change only what they added themselves. An
        // editor may send everything.
        records.MapPost("", async (Guid collectionId, PushRequest request, HttpContext http, StiklingDbContext db, PhotoStorage photos, TimeProvider clock) =>
        {
            var caller = http.Rights();
            var isHelper = !caller.Allows(CollectionRight.Edit);

            var received = request.Records ?? [];
            if (received.Count > SyncRules.BatchSize)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["records"] = [$"Send at most {SyncRules.BatchSize} records at a time."],
                });
            }

            var now = clock.GetUtcNow();
            var refused = new List<RefusedRecord>();
            var incoming = new List<(string Kind, RecordStamp Stamp, string Data, int Index, JsonElement Element)>();
            for (var i = 0; i < received.Count; i++)
            {
                var record = received[i];
                if (record?.Kind is null || !SyncKinds.IsCollection(record.Kind))
                    refused.Add(new RefusedRecord(i, $"\"{record?.Kind}\" isn't a kind of record a collection holds."));
                else if (SyncRules.Problem(record.Data, now) is { } problem)
                    refused.Add(new RefusedRecord(i, problem));
                else
                {
                    // A deleted record is stripped before it's compared, so the same deletion sent
                    // again, in full, is seen as the version already here
                    RecordStamp.TryRead(record.Data, out var stamp);
                    incoming.Add((record.Kind, stamp, SyncRules.AsStored(record.Data), i, record.Data));
                }
            }

            // The same record twice in one upload: only the version that wins counts
            var winners = incoming
                .GroupBy(r => (r.Kind, r.Stamp.Id))
                .Select(g => g.Aggregate((a, b) => SyncRules.Replaces(b.Stamp, b.Data, a.Stamp, a.Data) ? b : a))
                .ToList();

            await using var transaction = await db.Database.BeginTransactionAsync();

            // Two uploads to one collection take turns. Change numbers are then committed in order,
            // and a device fetching at the same moment can't pass a number that is yet to be committed.
            await db.LockCollectionAsync(collectionId);
            var collection = await db.Collections.SingleAsync(c => c.Id == collectionId);

            var ids = winners.Select(r => r.Stamp.Id).Distinct().ToList();
            var existing = (await db.Records
                    .Where(r => r.CollectionId == collectionId && ids.Contains(r.Id))
                    .ToListAsync())
                .ToDictionary(r => (r.Kind, r.Id));

            var newer = new List<SyncRecord>();
            var deletedPhotos = new List<Guid>();
            var deletedSubjects = new List<Guid>();
            foreach (var (kind, stamp, data, index, element) in winners)
            {
                existing.TryGetValue((kind, stamp.Id), out var current);

                // Checked one by one, so a refused record doesn't hold up the rest. It is also
                // kept out of the lists of deleted photos and subjects below.
                if (isHelper
                    && PeopleRules.HelperProblem(kind, element, current is not null, current?.CreatedBy, caller.PersonId) is { } problem)
                {
                    refused.Add(new RefusedRecord(index, problem));
                    continue;
                }

                if (kind == SyncKinds.Photos && stamp.DeletedAt is not null)
                    deletedPhotos.Add(stamp.Id);
                if (kind is "plants" or "propagations" && stamp.DeletedAt is not null)
                    deletedSubjects.Add(stamp.Id);

                if (current is null)
                {
                    db.Records.Add(new SyncedRecord
                    {
                        CollectionId = collectionId,
                        Kind = kind,
                        Id = stamp.Id,
                        Version = ++collection.LastVersion,
                        UpdatedAt = stamp.UpdatedAt,
                        DeletedAt = stamp.DeletedAt,
                        CreatedBy = caller.PersonId,
                        Data = data,
                    });
                    continue;
                }

                var here = new RecordStamp(current.Id, current.UpdatedAt, current.DeletedAt);
                if (SyncRules.Replaces(stamp, data, here, current.Data))
                {
                    current.Version = ++collection.LastVersion;
                    current.UpdatedAt = stamp.UpdatedAt;
                    current.DeletedAt = stamp.DeletedAt;
                    current.Data = data;
                }
                else if (SyncRules.Replaces(here, current.Data, stamp, data))
                {
                    newer.Add(new SyncRecord(current.Kind, Parse(current.Data), current.CreatedBy));
                    deletedPhotos.Remove(stamp.Id);
                    deletedSubjects.Remove(stamp.Id);
                }

                // Neither wins when it's the same version again, e.g. sent twice because an answer got lost
            }

            await db.SaveChangesAsync();

            // A page for a deleted plant or propagation is turned off for good, so it stays off if the
            // record is brought back. Sharing can be turned on again then, with a new address.
            if (deletedSubjects.Count > 0)
            {
                var turnedOff = now;
                await db.ShareLinks
                    .Where(l => l.CollectionId == collectionId && l.TurnedOffAt == null && deletedSubjects.Contains(l.SubjectId))
                    .ExecuteUpdateAsync(set => set.SetProperty(l => l.TurnedOffAt, turnedOff).SetProperty(l => l.UpdatedAt, turnedOff));
            }

            // A deleted photo's images go too. Its record stays, so other devices hear it was deleted
            var goneImages = await db.PhotoImages
                .Where(i => i.CollectionId == collectionId && deletedPhotos.Contains(i.PhotoId))
                .ToListAsync();
            db.PhotoImages.RemoveRange(goneImages);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();

            await photos.DeleteAsync(goneImages);

            return Results.Ok(new PushResponse(newer, refused));
        })
        .RequireAuthorization(CollectionPolicies.Help);

        return app;
    }

    public static JsonElement Parse(string data) => JsonSerializer.Deserialize<JsonElement>(data);
}
